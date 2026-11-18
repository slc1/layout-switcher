using LayoutSwitcher.Core;

namespace LayoutSwitcher.Core.Tests;

public class LayoutTableTests
{
    [Theory]
    [InlineData(false, false, 'q', 'й')]
    [InlineData(true, false, 'Q', 'Й')]
    [InlineData(false, true, 'Q', 'Й')]
    [InlineData(true, true, 'q', 'й')]
    public void TryGetChar_honours_shift_and_capslock(bool shift, bool caps, char en, char ua)
    {
        var stroke = new KeyStroke(0x10, shift, caps);

        Assert.True(TestLayouts.En.TryGetChar(stroke, out var enCh));
        Assert.True(TestLayouts.Ua.TryGetChar(stroke, out var uaCh));
        Assert.Equal(en, enCh);
        Assert.Equal(ua, uaCh);
    }

    [Fact]
    public void Comma_key_is_be_in_ukrainian()
    {
        var stroke = new KeyStroke(0x33, false, false);

        Assert.True(TestLayouts.En.TryGetChar(stroke, out var en));
        Assert.True(TestLayouts.Ua.TryGetChar(stroke, out var ua));
        Assert.Equal(',', en);
        Assert.Equal('б', ua);
    }

    [Fact]
    public void TryGetStroke_prefers_shift_over_capslock()
    {
        Assert.True(TestLayouts.En.TryGetStroke('Q', out var stroke));
        Assert.Equal(new KeyStroke(0x10, Shift: true, CapsLock: false), stroke);
    }

    [Fact]
    public void TryGetStroke_prefers_plain_key_for_lowercase()
    {
        Assert.True(TestLayouts.Ua.TryGetStroke('й', out var stroke));
        Assert.Equal(new KeyStroke(0x10, Shift: false, CapsLock: false), stroke);
    }

    [Fact]
    public void TryGetStroke_fails_for_unmapped_char()
    {
        Assert.False(TestLayouts.En.TryGetStroke('€', out _));
    }

    [Fact]
    public void TryGetChar_fails_for_unknown_scan_code()
    {
        Assert.False(TestLayouts.En.TryGetChar(new KeyStroke(0x47, false, false), out _));
    }

    [Fact]
    public void Pair_indexer_and_Other()
    {
        Assert.Same(TestLayouts.Ua, TestLayouts.Pair[LayoutId.Ua]);
        Assert.Same(TestLayouts.En, TestLayouts.Pair[LayoutId.En]);
        Assert.Equal(LayoutId.En, LayoutId.Ua.Other());
        Assert.Equal(LayoutId.Ua, LayoutId.En.Other());
    }
}
