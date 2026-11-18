using System.Text;

namespace LayoutSwitcher.Core;

public static class TextConverter
{
    public const int MaxSelectionLength = 2000;

    /// <summary>Retypes every char of <paramref name="text"/> as if its key had been pressed in <paramref name="to"/>.</summary>
    public static string Convert(string text, LayoutTable from, LayoutTable to)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(from.TryGetStroke(ch, out var stroke) && to.TryGetChar(stroke, out var converted)
                ? converted
                : ch);
        }
        return sb.ToString();
    }

    /// <summary>The layout the text was typed in: <see cref="LayoutId.Ua"/> when Cyrillic letters outnumber Latin ones.</summary>
    public static LayoutId DetectSource(string text)
    {
        int cyrillic = 0, latin = 0;
        foreach (var ch in text)
        {
            if (ch is >= 'Ѐ' and <= 'ӿ')
                cyrillic++;
            else if (char.IsAsciiLetter(ch))
                latin++;
        }
        return cyrillic > latin ? LayoutId.Ua : LayoutId.En;
    }

    public static bool TryConvertSelection(string text, LayoutPair pair, out string result, out LayoutId target)
    {
        var source = DetectSource(text);
        target = source.Other();
        result = "";

        if (text.Length is 0 or > MaxSelectionLength)
            return false;

        result = Convert(text, pair[source], pair[target]);
        return true;
    }

    /// <summary>Text the strokes type in <paramref name="layout"/>; a stroke it lacks comes from <paramref name="fallback"/> or is dropped.</summary>
    public static string Render(IEnumerable<KeyStroke> strokes, LayoutTable layout, LayoutTable fallback)
    {
        var sb = new StringBuilder();
        foreach (var stroke in strokes)
        {
            if (layout.TryGetChar(stroke, out var ch) || fallback.TryGetChar(stroke, out ch))
                sb.Append(ch);
        }
        return sb.ToString();
    }
}
