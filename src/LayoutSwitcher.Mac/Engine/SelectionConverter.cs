using LayoutSwitcher.Core;
using LayoutSwitcher.Mac.Clipboard;
using LayoutSwitcher.Mac.Input;

namespace LayoutSwitcher.Mac.Engine;

/// <summary>
/// Copy the selection, convert it, paste it back, restore the user's pasteboard (design §7.2).
/// Runs on the engine thread; pasteboard calls are marshalled to the main thread.
/// </summary>
internal sealed class SelectionConverter(Action<Action> onMainThread, LayoutPair pair)
{
    private static readonly TimeSpan CopyTimeout = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan PasteSettle = TimeSpan.FromMilliseconds(300);

    private readonly ClipboardGuard _clipboard = new();

    public LayoutId? Convert()
    {
        InputSender.WaitForModifierRelease();
        var afterBackup = Main(() =>
        {
            _clipboard.Backup();
            return ClipboardGuard.ChangeCount;
        });
        try
        {
            InputSender.SendCommand(MacKeys.C);
            if (!WaitForClipboardChange(afterBackup))
                return null; // nothing selected

            var text = Main(_clipboard.ReadText);
            if (text is null || !TextConverter.TryConvertSelection(text, pair, out var converted, out var target))
                return null;

            onMainThread(() => _clipboard.SetText(converted));
            InputSender.SendCommand(MacKeys.V);
            Thread.Sleep(PasteSettle); // the app reads the pasteboard asynchronously after Cmd+V
            return target;
        }
        finally
        {
            // Restoring rewrites the pasteboard, so skip it when nothing changed.
            onMainThread(() =>
            {
                if (ClipboardGuard.ChangeCount == afterBackup)
                    _clipboard.Discard();
                else
                    _clipboard.Restore();
            });
        }
    }

    private bool WaitForClipboardChange(nint before)
    {
        var deadline = Environment.TickCount64 + (long)CopyTimeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (Main(() => ClipboardGuard.ChangeCount) != before)
                return true;
            Thread.Sleep(10);
        }
        return false;
    }

    private T Main<T>(Func<T> func)
    {
        T result = default!;
        onMainThread(() => result = func());
        return result;
    }
}
