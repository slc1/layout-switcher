using LayoutSwitcher.Core.Input;

namespace LayoutSwitcher.App.Input;

internal enum InputKind : byte
{
    Key,
    Hotkey,

    /// <summary>Mouse click or focus change: the caret may have moved.</summary>
    Reset,
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
