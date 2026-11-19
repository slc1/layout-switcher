using LayoutSwitcher.Core.Input;

namespace LayoutSwitcher.Core.Tests;

public class HotkeyTests
{
    [Theory]
    [InlineData("Pause", Modifiers.None, VirtualKeys.Pause)]
    [InlineData("shift + pause", Modifiers.Shift, VirtualKeys.Pause)]
    [InlineData("Ctrl+Pause", Modifiers.Ctrl, VirtualKeys.Pause)]
    [InlineData("Win+F8", Modifiers.Win, (ushort)0x77)]
    [InlineData("Ctrl+Alt+K", Modifiers.Ctrl | Modifiers.Alt, (ushort)'K')]
    [InlineData("Alt+1", Modifiers.Alt, (ushort)'1')]
    [InlineData("CapsLock", Modifiers.None, VirtualKeys.CapsLock)]
    [InlineData("F24", Modifiers.None, (ushort)0x87)]
    [InlineData("Ctrl+Option+W", Modifiers.Ctrl | Modifiers.Alt, (ushort)'W')]
    [InlineData("cmd+shift+space", Modifiers.Win | Modifiers.Shift, VirtualKeys.Space)]
    public void Parses(string text, Modifiers modifiers, ushort vk)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(new Hotkey(modifiers, vk), hotkey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Foo")]
    [InlineData("Ctrl+Shift")]
    [InlineData("A+B")]
    [InlineData("F25")]
    public void Rejects(string text)
    {
        Assert.False(Hotkey.TryParse(text, out _));
    }

    [Fact]
    public void ToString_is_canonical()
    {
        Assert.True(Hotkey.TryParse("shift+win+ctrl+alt+pause", out var hotkey));
        Assert.Equal("Ctrl+Alt+Shift+Win+Pause", hotkey.ToString());
    }

    [Theory]
    [InlineData("Win+F8")]
    [InlineData("Ctrl+Space")]
    [InlineData("ScrollLock")]
    [InlineData("Shift+Z")]
    public void ToString_round_trips(string text)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(text, hotkey.ToString());
    }

    [Theory]
    [InlineData("Ctrl+Alt+W", "Ctrl+Option+W")]
    [InlineData("Win+Shift+F8", "Shift+Cmd+F8")]
    [InlineData("Space", "Space")]
    public void ToMacString_uses_mac_modifier_names(string text, string expected)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(expected, hotkey.ToMacString());
        Assert.True(Hotkey.TryParse(expected, out var parsed));
        Assert.Equal(hotkey, parsed);
    }

    [Fact]
    public void Ctrl_pause_reported_as_cancel_is_normalized()
    {
        Assert.Equal(VirtualKeys.Pause, Hotkey.NormalizeVk(VirtualKeys.Cancel, 0x46));
        Assert.Equal(VirtualKeys.Cancel, Hotkey.NormalizeVk(VirtualKeys.Cancel, 0x00));
        Assert.Equal((ushort)'Q', Hotkey.NormalizeVk('Q', 0x10));
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("5", true)]
    [InlineData("Shift+Z", true)]
    [InlineData("Ctrl+A", false)]
    [InlineData("Alt+5", false)]
    [InlineData("Win+Z", false)]
    [InlineData("F1", false)]
    [InlineData("F11", false)]
    [InlineData("Pause", false)]
    [InlineData("Space", false)]
    public void BlocksTyping_only_for_letters_and_digits_without_ctrl_alt_win(string text, bool expected)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(expected, hotkey.BlocksTyping);
    }
}
