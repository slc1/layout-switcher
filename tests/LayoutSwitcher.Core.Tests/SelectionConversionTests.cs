using LayoutSwitcher.Core;

namespace LayoutSwitcher.Core.Tests;

public class SelectionConversionTests
{
    private static (bool ok, string result, LayoutId target) Convert(string text)
    {
        var ok = TextConverter.TryConvertSelection(text, TestLayouts.Pair, out var result, out var target);
        return (ok, result, target);
    }

    [Fact]
    public void English_typed_text_becomes_ukrainian()
    {
        Assert.Equal((true, "привіт світ", LayoutId.Ua), Convert("ghbdsn cdsn"));
    }

    [Fact]
    public void Ukrainian_typed_text_becomes_english()
    {
        Assert.Equal((true, "hello world", LayoutId.En), Convert("руддщ цщкдв"));
    }

    [Fact]
    public void Unmappable_chars_pass_through()
    {
        Assert.Equal((true, "Привітб 2024!\r\n😀", LayoutId.Ua), Convert("Ghbdsn, 2024!\r\n😀"));
    }

    [Fact]
    public void Majority_script_decides_direction_and_other_script_passes_through()
    {
        Assert.Equal((true, "ghbdsn hello", LayoutId.En), Convert("привіт hello"));
    }

    [Fact]
    public void Text_without_letters_converts_from_english()
    {
        Assert.Equal(LayoutId.En, TextConverter.DetectSource("2024 /"));
    }

    [Fact]
    public void Empty_text_is_rejected()
    {
        Assert.False(Convert("").ok);
    }

    [Fact]
    public void Length_limit_is_inclusive()
    {
        Assert.True(Convert(new string('a', TextConverter.MaxSelectionLength)).ok);
        Assert.False(Convert(new string('a', TextConverter.MaxSelectionLength + 1)).ok);
        Assert.Equal(2000, TextConverter.MaxSelectionLength);
    }
}
