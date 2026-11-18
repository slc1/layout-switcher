using LayoutSwitcher.Core;

namespace LayoutSwitcher.Core.Tests;

public class TextConverterTests
{
    private static string RenderUa(IEnumerable<KeyStroke> strokes) =>
        TextConverter.Render(strokes, TestLayouts.Ua, TestLayouts.En);

    [Fact]
    public void Renders_wrong_layout_word()
    {
        Assert.Equal("привіт", RenderUa(TestLayouts.Type("ghbdsn", LayoutId.En)));
    }

    [Fact]
    public void Keeps_shift_capital()
    {
        Assert.Equal("Привіт", RenderUa(TestLayouts.Type("Ghbdsn", LayoutId.En)));
    }

    [Fact]
    public void Keeps_capslock_capitals()
    {
        var strokes = TestLayouts.Type("ghbdsn", LayoutId.En).Select(s => s with { CapsLock = true });

        Assert.Equal("ПРИВІТ", RenderUa(strokes));
    }

    [Fact]
    public void Converts_punctuation_keys()
    {
        Assert.Equal("привіт.", RenderUa(TestLayouts.Type("ghbdsn/", LayoutId.En)));
    }

    [Fact]
    public void Renders_to_english()
    {
        var strokes = TestLayouts.Type("руддщ", LayoutId.Ua);

        Assert.Equal("hello", TextConverter.Render(strokes, TestLayouts.En, TestLayouts.Ua));
    }

    [Fact]
    public void Falls_back_then_skips()
    {
        var onlyInFallback = new LayoutTable(new Dictionary<KeyStroke, char> { [new(0x60, false, false)] = 'x' });
        KeyStroke[] strokes = [new(0x10, false, false), new(0x60, false, false), new(0x61, false, false)];

        Assert.Equal("йx", TextConverter.Render(strokes, TestLayouts.Ua, onlyInFallback));
    }
}
