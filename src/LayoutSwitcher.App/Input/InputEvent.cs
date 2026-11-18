using LayoutSwitcher.Core.Input;

namespace LayoutSwitcher.App.Input;

internal enum InputKind : byte
{
    Key,
    Hotkey,

    /// <summary>Mouse click or focus change: the caret may have moved.</summary>
    Reset,
}

internal enum HotkeyAction : byte
{
    ConvertLastWord,
    ConvertSelection,
    ConvertAllTyped,
    ToggleEnabled,
}

/// <summary>Physical modifier keys currently down, as tracked by the hook.</summary>
[Flags]
internal enum HeldModifiers : byte
{
    None = 0,
    LShift = 1,
    RShift = 2,
    LCtrl = 4,
    RCtrl = 8,
    LAlt = 16,
    RAlt = 32,
    LWin = 64,
    RWin = 128,
}

/// <summary>A struct so the hook callback can post it without allocating.</summary>
internal readonly record struct InputEvent(
    InputKind Kind,
    ushort Vk = 0,
    ushort Scan = 0,
    bool Extended = false,
    Modifiers Modifiers = Modifiers.None,
    bool CapsLock = false,
    HotkeyAction Action = default,
    HeldModifiers Held = HeldModifiers.None);
