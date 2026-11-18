namespace LayoutSwitcher.Core;

public static class WordSegmenter
{
    /// <summary>
    /// Index of the first stroke of the last word. The range runs to the end, so separators typed after
    /// the word belong to it. Returns <c>strokes.Count</c> when there is no letter.
    /// </summary>
    public static int LastWordStart(IReadOnlyList<KeyStroke> strokes, LayoutPair pair, string extraWordChars)
    {
        StrokeKind Kind(int i) => StrokeClassifier.Classify(strokes[i], pair, extraWordChars);

        var end = strokes.Count - 1;
        while (end >= 0 && Kind(end) != StrokeKind.Letter)
            end--;
        if (end < 0)
            return strokes.Count;

        var start = end;
        while (start > 0)
        {
            if (Kind(start - 1) == StrokeKind.Letter)
                start--;
            else if (Kind(start - 1) == StrokeKind.Joiner && start > 1 && Kind(start - 2) == StrokeKind.Letter)
                start -= 2;
            else
                break;
        }
        return start;
    }
}
