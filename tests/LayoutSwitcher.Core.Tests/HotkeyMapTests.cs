using LayoutSwitcher.Core.Config;
using LayoutSwitcher.Core.Input;

namespace LayoutSwitcher.Core.Tests;

public class HotkeyMapTests
{
    private static HotkeyMap Map(out List<string> invalid) => HotkeyMap.From(new HotkeySettings
    {
        ConvertLastWord = "Ctrl+Option+W",
        ConvertSelection = "Shift+Pause",
        ConvertAllTyped = "",
        ToggleEnabled = "Bogus+Key",
    }, out invalid);

    [Fact]
    public void Matches_exact_modifiers_and_key()
    {
        var map = Map(out _);

        Assert.True(map.TryMatch(Modifiers.Ctrl | Modifiers.Alt, 'W', enabled: true, out var action));
        Assert.Equal(HotkeyAction.ConvertLastWord, action);
        Assert.False(map.TryMatch(Modifiers.Ctrl, 'W', enabled: true, out _));
        Assert.False(map.TryMatch(Modifiers.Ctrl | Modifiers.Alt | Modifiers.Shift, 'W', enabled: true, out _));
    }

    [Fact]
    public void Skips_blank_and_reports_unparseable()
    {
        Map(out var invalid);

        Assert.Equal(["Bogus+Key"], invalid);
    }

    [Fact]
    public void Only_the_toggle_matches_while_disabled()
    {
        var map = HotkeyMap.From(new HotkeySettings { ConvertLastWord = "Ctrl+Option+W", ToggleEnabled = "Ctrl+Option+T" }, out _);

        Assert.False(map.TryMatch(Modifiers.Ctrl | Modifiers.Alt, 'W', enabled: false, out _));
        Assert.True(map.TryMatch(Modifiers.Ctrl | Modifiers.Alt, 'T', enabled: false, out var action));
        Assert.Equal(HotkeyAction.ToggleEnabled, action);
    }
}
