namespace LayoutSwitcher.Core;

public enum StrokeKind
{
    Letter,

    /// <summary>Part of a word only when it sits between two letters: apostrophes and configured extra chars.</summary>
    Joiner,

    Separator,
}

public static class StrokeClassifier
{
    private const string Apostrophes = "'\u02BC\u2019";

    /// <summary>
    /// A key is a letter if it types a letter in either layout: on the Ukrainian layout <c>, . ; [ ] '</c>
    /// are б ю ж х ї є, so a word typed in the wrong layout must not be split on them.
    /// </summary>
    public static StrokeKind Classify(KeyStroke stroke, LayoutPair pair, string extraWordChars)
    {
        var hasEn = pair.En.TryGetChar(stroke, out var en);
        var hasUa = pair.Ua.TryGetChar(stroke, out var ua);

        if ((hasEn && char.IsLetter(en)) || (hasUa && char.IsLetter(ua)))
            return StrokeKind.Letter;

        bool IsJoiner(char c) => Apostrophes.Contains(c) || extraWordChars.Contains(c);
        if ((hasEn && IsJoiner(en)) || (hasUa && IsJoiner(ua)))
            return StrokeKind.Joiner;

        return StrokeKind.Separator;
    }
}
