using LayoutSwitcher.Core;

namespace LayoutSwitcher.Core.Tests;

public class WordSegmenterTests
{
    [Theory]
    [InlineData("ghbdsn", 0)]
    [InlineData("ghbdsn ", 0)]
    [InlineData("hello ghbdsn", 6)]
    [InlineData("ghbdsn, ", 0)]   // ',' types 'б' in Ukrainian, so it is a letter key
    [InlineData("vjz v`znf", 4)]  // '`' types the apostrophe in Ukrainian: м'ята stays one word
    [InlineData("ab -cd", 4)]     // a joiner without a letter on its left is not part of the word
    [InlineData("ab-", 0)]
    [InlineData("ab 123 ", 0)]    // digits are separators
    [InlineData("  ", 2)]
    [InlineData("", 0)]
    public void Finds_start_of_last_word(string typed, int expected)
    {
        var strokes = TestLayouts.Type(typed, LayoutId.En);

        Assert.Equal(expected, WordSegmenter.LastWordStart(strokes, TestLayouts.Pair, "-_"));
    }
}
