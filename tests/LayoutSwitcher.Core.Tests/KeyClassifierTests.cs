using LayoutSwitcher.Core.Input;

namespace LayoutSwitcher.Core.Tests;

public class KeyClassifierTests
{
    [Theory]
    [InlineData((ushort)'Q', Modifiers.None, KeyAction.Record)]
    [InlineData((ushort)'Q', Modifiers.Shift, KeyAction.Record)]
    [InlineData((ushort)'Q', Modifiers.Ctrl, KeyAction.Reset)]
    [InlineData((ushort)'Q', Modifiers.Alt, KeyAction.Reset)]
    [InlineData((ushort)'Q', Modifiers.Win, KeyAction.Reset)]
    [InlineData((ushort)0xBC, Modifiers.None, KeyAction.Record)] // OEM comma
    [InlineData(VirtualKeys.Space, Modifiers.None, KeyAction.Record)]
    [InlineData(VirtualKeys.Tab, Modifiers.None, KeyAction.Record)]
    [InlineData(VirtualKeys.Back, Modifiers.None, KeyAction.Backspace)]
    [InlineData(VirtualKeys.Back, Modifiers.Ctrl, KeyAction.Reset)]
    [InlineData(VirtualKeys.Return, Modifiers.None, KeyAction.Reset)]
    [InlineData(VirtualKeys.Escape, Modifiers.None, KeyAction.Reset)]
    [InlineData(VirtualKeys.Left, Modifiers.None, KeyAction.Reset)]
    [InlineData(VirtualKeys.Home, Modifiers.Shift, KeyAction.Reset)]
    [InlineData(VirtualKeys.Delete, Modifiers.None, KeyAction.Reset)]
    [InlineData((ushort)0x70, Modifiers.None, KeyAction.Reset)] // F1
    [InlineData((ushort)0x87, Modifiers.None, KeyAction.Reset)] // F24
    [InlineData(VirtualKeys.LShift, Modifiers.Shift, KeyAction.Ignore)]
    [InlineData(VirtualKeys.RControl, Modifiers.Ctrl, KeyAction.Ignore)]
    [InlineData(VirtualKeys.LWin, Modifiers.Win, KeyAction.Ignore)]
    [InlineData(VirtualKeys.CapsLock, Modifiers.None, KeyAction.Ignore)]
    [InlineData(VirtualKeys.NumLock, Modifiers.None, KeyAction.Ignore)]
    public void Classifies(ushort vk, Modifiers held, KeyAction expected)
    {
        Assert.Equal(expected, KeyClassifier.Classify(vk, held));
    }

    [Theory]
    [InlineData(Modifiers.None)]
    [InlineData(Modifiers.Shift)]
    public void Insert_resets_because_paste_and_overwrite_change_text(Modifiers held)
    {
        Assert.Equal(KeyAction.Reset, KeyClassifier.Classify(VirtualKeys.Insert, held));
    }
}
