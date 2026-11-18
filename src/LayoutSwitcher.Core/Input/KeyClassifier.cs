namespace LayoutSwitcher.Core.Input;

public enum KeyAction
{
    /// <summary>Candidate for the typed buffer (the engine still checks the key types a char).</summary>
    Record,
    Backspace,

    /// <summary>The caret may have moved or text changed in a way we cannot track.</summary>
    Reset,
    Ignore,
}

public static class KeyClassifier
{
    public static KeyAction Classify(ushort vk, Modifiers held)
    {
        if (VirtualKeys.IsModifier(vk)
            || vk is VirtualKeys.CapsLock or VirtualKeys.NumLock or VirtualKeys.ScrollLock
                or VirtualKeys.Snapshot or VirtualKeys.Pause)
            return KeyAction.Ignore;

        // AltGr arrives as Ctrl+Alt, so AltGr-typed chars also reset; acceptable for UA/EN.
        if ((held & (Modifiers.Ctrl | Modifiers.Alt | Modifiers.Win)) != 0)
            return KeyAction.Reset;

        if (vk == VirtualKeys.Back)
            return KeyAction.Backspace;

        // Insert: Shift+Insert pastes, plain Insert toggles overwrite mode; both break the erase count.
        if (vk is VirtualKeys.Return or VirtualKeys.Escape or VirtualKeys.Delete or VirtualKeys.Insert
                or (>= VirtualKeys.Prior and <= VirtualKeys.Down)
            || VirtualKeys.IsFunctionKey(vk))
            return KeyAction.Reset;

        return KeyAction.Record;
    }
}
