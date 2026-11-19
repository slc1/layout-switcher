using LayoutSwitcher.Core.Input;

namespace LayoutSwitcher.Mac.Input;

internal enum InputKind : byte
{
    Key,
    Hotkey,

    /// <summary>Mouse click or app switch: the caret may have moved.</summary>
    Reset,
}

/// <summary>A struct so the tap callback can post it without allocating.</summary>
/// <param name="KeyCode">The Mac key code; Core keys strokes by it.</param>
/// <param name="Keypad">Keypad keys type the same chars as main keys but would be retyped as those.</param>
internal readonly record struct InputEvent(
    InputKind Kind,
    ushort Vk = 0,
    ushort KeyCode = 0,
    bool Keypad = false,
    Modifiers Modifiers = Modifiers.None,
    bool CapsLock = false,
    HotkeyAction Action = default);
