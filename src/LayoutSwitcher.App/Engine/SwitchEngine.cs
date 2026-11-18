using System.Threading.Channels;
using LayoutSwitcher.App.Input;
using LayoutSwitcher.App.Layouts;
using LayoutSwitcher.Core;
using LayoutSwitcher.Core.Input;

namespace LayoutSwitcher.App.Engine;

/// <summary>Things the engine asks of the UI thread. Implementations marshal to it themselves.</summary>
internal interface IEngineHost
{
    void ToggleEnabled();

    /// <summary>The foreground app is elevated; our input would be blocked there.</summary>
    void WarnElevated();

    /// <summary>Converts the selection in the focused app; returns the target layout, or null if nothing was converted.</summary>
    LayoutId? ConvertSelection(HeldModifiers held);
}

/// <summary>Single reader of the hook's events. All buffer state lives on this loop.</summary>
internal sealed class SwitchEngine(
    ChannelReader<InputEvent> events,
    KeyboardHook hook,
    LayoutService layouts,
    IEngineHost host,
    TimeProvider time)
{
    private readonly TypedBuffer _buffer = new(time);

    public volatile string ExtraWordChars = "-_";

    public async Task RunAsync(CancellationToken cancel)
    {
        while (await events.WaitToReadAsync(cancel).ConfigureAwait(false))
        {
            while (events.TryRead(out var e))
                Handle(e);
        }
    }

    private void Handle(InputEvent e)
    {
        switch (e.Kind)
        {
            case InputKind.Reset:
                _buffer.Clear();
                break;
            case InputKind.Key:
                HandleKey(e);
                break;
            case InputKind.Hotkey:
                HandleHotkey(e);
                break;
        }
    }

    private void HandleKey(InputEvent e)
    {
        switch (KeyClassifier.Classify(e.Vk, e.Modifiers))
        {
            case KeyAction.Ignore:
                return;
            case KeyAction.Backspace:
                _buffer.Backspace();
                return;
            case KeyAction.Reset:
                _buffer.Clear();
                return;
        }

        // Core keys strokes by scan code only; numpad '/' (extended) would be retyped as the main '/' key.
        if (e.Extended)
        {
            _buffer.Clear();
            return;
        }

        var stroke = new KeyStroke(e.Scan, e.Modifiers.HasFlag(Modifiers.Shift), e.CapsLock);
        var layout = layouts.GetFocusedLayout();

        // Required, not an optimisation: a revert erases every buffered stroke, so a stroke we
        // cannot retype must never be in the buffer.
        if (layout is not { } id || !layouts.Pair[id].TryGetChar(stroke, out _))
        {
            _buffer.Clear();
            return;
        }
        _buffer.Add(new TypedKey(stroke, id));
    }

    private void HandleHotkey(InputEvent e)
    {
        if (e.Action == HotkeyAction.ToggleEnabled)
        {
            _buffer.Clear();
            host.ToggleEnabled();
            return;
        }

        if (hook.Foreground.Elevated)
        {
            host.WarnElevated();
            return;
        }

        switch (e.Action)
        {
            case HotkeyAction.ConvertLastWord:
                ConvertTyped(RevertScope.LastWord, e.Held);
                break;
            case HotkeyAction.ConvertAllTyped:
                ConvertTyped(RevertScope.AllTyped, e.Held);
                break;
            case HotkeyAction.ConvertSelection:
                _buffer.Clear();
                if (host.ConvertSelection(e.Held) is { } target)
                    layouts.SetLayout(target);
                break;
        }
    }

    private void ConvertTyped(RevertScope scope, HeldModifiers held)
    {
        var plan = RevertPlanner.Plan(scope, _buffer.Snapshot(), layouts.Pair, ExtraWordChars);
        if (plan is null)
            return;

        InputSender.ReleaseModifiers(held);
        InputSender.Replace(plan.EraseCount, plan.Text);
        layouts.SetLayout(plan.Target);
        _buffer.MarkDisplayed(plan.Start, plan.Target);
    }
}
