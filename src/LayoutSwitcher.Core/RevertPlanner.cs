namespace LayoutSwitcher.Core;

public enum RevertScope
{
    LastWord,
    AllTyped,
}

/// <summary>Erase <see cref="EraseCount"/> chars, type <see cref="Text"/>, switch to <see cref="Target"/>.</summary>
public sealed record RevertPlan(int Start, int EraseCount, string Text, LayoutId Target);

public static class RevertPlanner
{
    /// <summary>
    /// The target is the opposite of the layout the range's first letter is displayed in. After applying a plan the
    /// caller marks the range as displayed in the target, so the next plan over the same range undoes it.
    /// </summary>
    public static RevertPlan? Plan(RevertScope scope, IReadOnlyList<TypedKey> keys, LayoutPair pair, string extraWordChars)
    {
        var strokes = keys.Select(k => k.Stroke).ToArray();
        var start = scope == RevertScope.LastWord ? WordSegmenter.LastWordStart(strokes, pair, extraWordChars) : 0;

        var range = keys.Skip(start).ToArray();
        var firstLetter = Array.FindIndex(range,
            k => StrokeClassifier.Classify(k.Stroke, pair, extraWordChars) == StrokeKind.Letter);
        if (firstLetter < 0)
            return null;

        // Leading digits and spaces look the same in both layouts, so they say nothing about the source.
        var target = range[firstLetter].Layout.Other();
        var text = string.Concat(range.Select(k => TextConverter.Render([k.Stroke], pair[target], pair[k.Layout])));

        return new RevertPlan(start, range.Length, text, target);
    }
}
