namespace LayoutSwitcher.Core.Input;

public readonly record struct Hotkey(Modifiers Modifiers, ushort Vk)
{
    private static readonly Dictionary<string, ushort> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pause"] = VirtualKeys.Pause,
        ["Space"] = VirtualKeys.Space,
        ["CapsLock"] = VirtualKeys.CapsLock,
        ["ScrollLock"] = VirtualKeys.ScrollLock,
        ["Insert"] = VirtualKeys.Insert,
    };

    private static readonly (string Name, Modifiers Flag)[] ModifierNames =
        [("Ctrl", Modifiers.Ctrl), ("Alt", Modifiers.Alt), ("Shift", Modifiers.Shift), ("Win", Modifiers.Win)];

    /// <summary>Parses <c>Ctrl+Shift+Pause</c>-style text: any modifiers, then exactly one key, last.</summary>
    public static bool TryParse(string text, out Hotkey hotkey)
    {
        hotkey = default;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = Modifiers.None;

        foreach (var part in parts[..^1])
        {
            var match = ModifierNames.FirstOrDefault(m => m.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (match.Name is null)
                return false;
            modifiers |= match.Flag;
        }

        if (!TryParseKey(parts[^1], out var vk))
            return false;

        hotkey = new Hotkey(modifiers, vk);
        return true;
    }

    /// <summary>A letter or digit without Ctrl/Alt/Win would swallow normal typing.</summary>
    public bool BlocksTyping =>
        Vk is >= '0' and <= '9' or >= 'A' and <= 'Z'
        && (Modifiers & (Modifiers.Ctrl | Modifiers.Alt | Modifiers.Win)) == 0;

    /// <summary>With Ctrl held, Windows reports Pause as VK_CANCEL (scan 0x46); map it back.</summary>
    public static ushort NormalizeVk(ushort vk, ushort scanCode) =>
        vk == VirtualKeys.Cancel && scanCode == 0x46 ? VirtualKeys.Pause : vk;

    public override string ToString()
    {
        var held = Modifiers;
        var mods = ModifierNames.Where(m => held.HasFlag(m.Flag)).Select(m => m.Name);
        return string.Join('+', mods.Append(KeyName(Vk)));
    }

    private static bool TryParseKey(string name, out ushort vk)
    {
        vk = 0;
        if (NamedKeys.TryGetValue(name, out vk))
            return true;

        if (name.Length == 1 && (char.IsAsciiLetter(name[0]) || char.IsAsciiDigit(name[0])))
        {
            vk = char.ToUpperInvariant(name[0]);
            return true;
        }

        if (name.Length >= 2 && name[0] is 'F' or 'f' && int.TryParse(name.AsSpan(1), out var n) && n is >= 1 and <= 24)
        {
            vk = (ushort)(VirtualKeys.F1 + n - 1);
            return true;
        }

        return false;
    }

    private static string KeyName(ushort vk)
    {
        foreach (var (name, code) in NamedKeys)
        {
            if (code == vk)
                return name;
        }
        if (VirtualKeys.IsFunctionKey(vk))
            return $"F{vk - VirtualKeys.F1 + 1}";
        return ((char)vk).ToString();
    }
}
