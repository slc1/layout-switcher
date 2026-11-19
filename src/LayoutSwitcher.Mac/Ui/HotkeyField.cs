using AppKit;
using Foundation;
using LayoutSwitcher.Core.Input;
using LayoutSwitcher.Mac.Input;

namespace LayoutSwitcher.Mac.Ui;

/// <summary>Click, then press a combination to set it; Delete clears it.</summary>
[Register(nameof(HotkeyField))]
internal sealed class HotkeyField : NSTextField
{
    private const ushort Tab = 0x30;
    private const ushort Escape = 0x35;
    private const ushort ForwardDelete = 0x75;

    public HotkeyField()
    {
        Editable = false;
        Selectable = false;
        Bezeled = true;
        BezelStyle = NSTextFieldBezelStyle.Rounded;
        PlaceholderString = "None";
        FocusRingType = NSFocusRingType.Exterior;
    }

    /// <summary>Empty string means "no hotkey". Shown with Mac modifier names, e.g. Alt as Option.</summary>
    public string Value
    {
        get => StringValue;
        set => StringValue = Hotkey.TryParse(value, out var hotkey) ? hotkey.ToMacString() : value;
    }

    public event Action<string>? Rejected;

    /// <summary>Gained or lost keyboard focus.</summary>
    public event Action? FocusChanged;

    public override bool AcceptsFirstResponder() => true;

    public override bool BecomeFirstResponder()
    {
        var accepted = base.BecomeFirstResponder();
        TextColor = NSColor.ControlAccent;
        FocusChanged?.Invoke();
        return accepted;
    }

    public override bool ResignFirstResponder()
    {
        var resigned = base.ResignFirstResponder();
        TextColor = NSColor.ControlText;
        FocusChanged?.Invoke();
        return resigned;
    }

    public override void MouseDown(NSEvent theEvent) => Window?.MakeFirstResponder(this);

    /// <summary>Combinations with Cmd or Ctrl arrive here first; they must not reach menus or buttons.</summary>
    public override bool PerformKeyEquivalent(NSEvent theEvent)
    {
        if (Window?.FirstResponder != this)
            return base.PerformKeyEquivalent(theEvent);
        Capture(theEvent);
        return true;
    }

    public override void KeyDown(NSEvent theEvent)
    {
        // Tab and Esc keep their window meaning (next field, cancel); they cannot be hotkeys anyway.
        if (theEvent.KeyCode is Tab or Escape)
            base.KeyDown(theEvent);
        else
            Capture(theEvent);
    }

    private void Capture(NSEvent e)
    {
        // NSEventModifierFlags and CGEventFlags share their bit values.
        var modifiers = MacKeys.ModifiersFrom((ulong)e.ModifierFlags);
        if (e.KeyCode is MacKeys.Delete or ForwardDelete && modifiers == Modifiers.None)
        {
            Value = "";
            return;
        }

        var hotkey = new Hotkey(modifiers, MacKeys.ToVk(e.KeyCode));
        var text = hotkey.ToMacString();
        if (hotkey.Vk == 0 || !Hotkey.TryParse(text, out var parsed) || parsed != hotkey)
        {
            Rejected?.Invoke("That key is not supported. Use F1–F20, a letter, a digit or Space.");
            return;
        }

        if (parsed.BlocksTyping)
        {
            Rejected?.Invoke("A letter or digit needs Ctrl, Option or Cmd, or it would block normal typing.");
            return;
        }

        Value = text;
    }
}
