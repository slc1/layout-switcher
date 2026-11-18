using LayoutSwitcher.Core;

namespace LayoutSwitcher.Core.Tests;

public class StrokeClassifierTests
{
    private static StrokeKind Classify(ushort scan, string extra = "-_") =>
        StrokeClassifier.Classify(new KeyStroke(scan, false, false), TestLayouts.Pair, extra);

    [Theory]
    [InlineData(0x10, StrokeKind.Letter)]    // q / й
    [InlineData(0x33, StrokeKind.Letter)]    // , / б
    [InlineData(0x1A, StrokeKind.Letter)]    // [ / х
    [InlineData(0x35, StrokeKind.Separator)] // / / .
    [InlineData(0x03, StrokeKind.Separator)] // 2
    [InlineData(0x39, StrokeKind.Separator)] // space
    [InlineData(0x29, StrokeKind.Joiner)]    // ` / '
    [InlineData(0x0C, StrokeKind.Joiner)]    // - with extra "-_"
    public void Classifies_by_both_layouts(ushort scan, StrokeKind expected)
    {
        Assert.Equal(expected, Classify(scan));
    }

    [Fact]
    public void Minus_is_separator_without_extra_chars()
    {
        Assert.Equal(StrokeKind.Separator, Classify(0x0C, extra: ""));
    }

    [Fact]
    public void Unknown_stroke_is_separator()
    {
        Assert.Equal(StrokeKind.Separator, Classify(0x47));
    }
}
