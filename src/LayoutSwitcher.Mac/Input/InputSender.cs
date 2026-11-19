using LayoutSwitcher.Mac.Native;

namespace LayoutSwitcher.Mac.Input;

/// <summary>
/// Synthetic input. Every event carries <see cref="Tag"/> so our own tap ignores it, and sets its modifier
/// flags explicitly, so the Ctrl or Option the user still holds for the hotkey does not turn it into a shortcut.
/// </summary>
internal static unsafe class InputSender
{
    public const long Tag = 0x4C53_5754; // "LSWT"

    // A private state source: our events do not inherit the modifiers that are physically down.
    private static readonly nint Source = CG.CGEventSourceCreate(CG.SourceStatePrivate);

    private const ulong HotkeyModifiers = CG.FlagShift | CG.FlagControl | CG.FlagAlternate | CG.FlagCommand;
    private static readonly TimeSpan ReleaseTimeout = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// Waits, briefly, for the user to let go of the hotkey's modifiers. Explicit flags keep most apps from
    /// seeing them, but some read the live keyboard state instead of the event's flags.
    /// </summary>
    public static void WaitForModifierRelease()
    {
        var deadline = Environment.TickCount64 + (long)ReleaseTimeout.TotalMilliseconds;
        while ((CG.CGEventSourceFlagsState(CG.SourceStateHidSystem) & HotkeyModifiers) != 0
               && Environment.TickCount64 < deadline)
            Thread.Sleep(10);
    }

    /// <summary>Backspaces, then the text, one char per event.</summary>
    public static void Replace(int eraseCount, string text)
    {
        for (var i = 0; i < eraseCount; i++)
        {
            Key(MacKeys.Delete, down: true);
            Key(MacKeys.Delete, down: false);
        }
        foreach (var ch in text)
        {
            Unicode(ch, down: true);
            Unicode(ch, down: false);
        }
    }

    /// <summary>Cmd+C / Cmd+V. Apps map Cmd shortcuts by ANSI key position, whatever the input source.</summary>
    public static void SendCommand(ushort keyCode)
    {
        Key(keyCode, down: true, CG.FlagCommand);
        Key(keyCode, down: false, CG.FlagCommand);
    }

    private static void Key(ushort keyCode, bool down, ulong flags = 0)
    {
        var ev = CG.CGEventCreateKeyboardEvent(Source, keyCode, down);
        Post(ev, flags);
    }

    /// <summary>The key code is a placeholder: apps read the attached string, as they do for IME input.</summary>
    private static void Unicode(char ch, bool down)
    {
        var ev = CG.CGEventCreateKeyboardEvent(Source, MacKeys.A, down);
        CG.CGEventKeyboardSetUnicodeString(ev, 1, &ch);
        Post(ev, 0);
    }

    private static void Post(nint ev, ulong flags)
    {
        if (ev == 0)
            return;
        CG.CGEventSetFlags(ev, flags);
        CG.CGEventSetIntegerValueField(ev, CG.FieldEventSourceUserData, Tag);
        CG.CGEventPost(CG.HidEventTap, ev);
        CF.CFRelease(ev);
    }
}
