using LayoutSwitcher.Core.Config;

namespace LayoutSwitcher.Core.Input;

public enum HotkeyAction : byte
{
    ConvertLastWord,
    ConvertSelection,
    ConvertAllTyped,
    ToggleEnabled,
}

/// <summary>Immutable; the hook reads it while the UI thread swaps in a new one.</summary>
public sealed class HotkeyMap
{
    public static readonly HotkeyMap Empty = new([]);

    private readonly (Hotkey Key, HotkeyAction Action)[] _entries;

    private HotkeyMap((Hotkey, HotkeyAction)[] entries) => _entries = entries;

    /// <summary>Unparseable hotkeys are skipped and returned in <paramref name="invalid"/>.</summary>
    public static HotkeyMap From(HotkeySettings settings, out List<string> invalid)
    {
        invalid = [];
        var entries = new List<(Hotkey, HotkeyAction)>();
        foreach (var (text, action) in new[]
                 {
                     (settings.ConvertLastWord, HotkeyAction.ConvertLastWord),
                     (settings.ConvertSelection, HotkeyAction.ConvertSelection),
                     (settings.ConvertAllTyped, HotkeyAction.ConvertAllTyped),
                     (settings.ToggleEnabled, HotkeyAction.ToggleEnabled),
                 })
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;
            if (Hotkey.TryParse(text, out var hotkey))
                entries.Add((hotkey, action));
            else
                invalid.Add(text);
        }
        return new HotkeyMap(entries.ToArray());
    }

    /// <summary>When disabled only the toggle hotkey matches, so the app can be switched back on.</summary>
    public bool TryMatch(Modifiers modifiers, ushort vk, bool enabled, out HotkeyAction action)
    {
        foreach (var (key, candidate) in _entries)
        {
            if (key.Vk == vk && key.Modifiers == modifiers && (enabled || candidate == HotkeyAction.ToggleEnabled))
            {
                action = candidate;
                return true;
            }
        }
        action = default;
        return false;
    }
}
