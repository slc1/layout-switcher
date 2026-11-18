using System.Runtime.InteropServices;
using LayoutSwitcher.Core.Input;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace LayoutSwitcher.App.Input;

/// <summary>Synthetic input. Every event carries <see cref="Tag"/> so our own hook ignores it.</summary>
internal static class InputSender
{
    public const nuint Tag = 0x4C53_5754; // "LSWT"

    // Unassigned VK. Tapping it before releasing Alt/Win stops Windows opening the Start menu or menu bar.
    private const ushort MenuSuppressorVk = 0xE8;

    private static readonly (HeldModifiers Flag, ushort Vk, bool Extended)[] ModifierKeys =
    [
        (HeldModifiers.LShift, VirtualKeys.LShift, false), (HeldModifiers.RShift, VirtualKeys.RShift, false),
        (HeldModifiers.LCtrl, VirtualKeys.LControl, false), (HeldModifiers.RCtrl, VirtualKeys.RControl, true),
        (HeldModifiers.LAlt, VirtualKeys.LMenu, false), (HeldModifiers.RAlt, VirtualKeys.RMenu, true),
        (HeldModifiers.LWin, VirtualKeys.LWin, true), (HeldModifiers.RWin, VirtualKeys.RWin, true),
    ];

    /// <summary>Lifts the modifiers the user is holding for the hotkey, so our input is not read as shortcuts.</summary>
    public static void ReleaseModifiers(HeldModifiers held)
    {
        if (held == HeldModifiers.None)
            return;

        var inputs = new List<INPUT>();
        if ((held & (HeldModifiers.LAlt | HeldModifiers.RAlt | HeldModifiers.LWin | HeldModifiers.RWin)) != 0)
        {
            inputs.Add(Key(MenuSuppressorVk, up: false));
            inputs.Add(Key(MenuSuppressorVk, up: true));
        }
        foreach (var (flag, vk, extended) in ModifierKeys)
        {
            if (held.HasFlag(flag))
                inputs.Add(Key(vk, up: true, extended));
        }
        Send(inputs);
    }

    /// <summary>Backspaces, then the text as Unicode, in one atomic batch.</summary>
    public static void Replace(int eraseCount, string text)
    {
        var inputs = new List<INPUT>(eraseCount * 2 + text.Length * 2);
        for (var i = 0; i < eraseCount; i++)
        {
            inputs.Add(Key(VirtualKeys.Back, up: false));
            inputs.Add(Key(VirtualKeys.Back, up: true));
        }
        foreach (var ch in text)
        {
            inputs.Add(Unicode(ch, up: false));
            inputs.Add(Unicode(ch, up: true));
        }
        Send(inputs);
    }

    /// <summary>Ctrl+C / Ctrl+V.</summary>
    public static void SendCtrl(ushort vk) =>
        Send([
            Key(VirtualKeys.LControl, up: false), Key(vk, up: false),
            Key(vk, up: true), Key(VirtualKeys.LControl, up: true),
        ]);

    private static INPUT Key(ushort vk, bool up, bool extended = false)
    {
        var flags = up ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0;
        if (extended)
            flags |= KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY;
        return new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = { ki = new KEYBDINPUT { wVk = (VIRTUAL_KEY)vk, dwFlags = flags, dwExtraInfo = Tag } },
        };
    }

    private static INPUT Unicode(char ch, bool up) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous =
        {
            ki = new KEYBDINPUT
            {
                wScan = ch,
                dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | (up ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0),
                dwExtraInfo = Tag,
            },
        },
    };

    private static void Send(List<INPUT> inputs)
    {
        if (inputs.Count > 0)
            PInvoke.SendInput(CollectionsMarshal.AsSpan(inputs), Marshal.SizeOf<INPUT>());
    }
}
