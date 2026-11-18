using LayoutSwitcher.Core;

namespace LayoutSwitcher.Core.Tests;

/// <summary>
/// Hand-written US and Ukrainian (Enhanced) approximations. Only self-consistency matters here;
/// the app builds the real tables from ToUnicodeEx.
/// </summary>
public static class TestLayouts
{
    private static readonly ushort[] Scans =
    [
        0x29, .. Range(0x02, 0x0D), .. Range(0x10, 0x1B), .. Range(0x1E, 0x28), 0x2B, .. Range(0x2C, 0x35), 0x39,
    ];

    public static readonly LayoutTable En = Build(
        "`1234567890-=" + "qwertyuiop[]" + "asdfghjkl;'" + "\\" + "zxcvbnm,./" + " ",
        "~!@#$%^&*()_+" + "QWERTYUIOP{}" + "ASDFGHJKL:\"" + "|" + "ZXCVBNM<>?" + " ");

    public static readonly LayoutTable Ua = Build(
        "'1234567890-=" + "йцукенгшщзхї" + "фівапролджє" + "ґ" + "ячсмитьбю." + " ",
        "₴!\"№;%:?*()_+" + "ЙЦУКЕНГШЩЗХЇ" + "ФІВАПРОЛДЖЄ" + "Ґ" + "ЯЧСМИТЬБЮ," + " ");

    public static readonly LayoutPair Pair = new(En, Ua);

    /// <summary>The keystrokes (CapsLock off) that type <paramref name="text"/> in <paramref name="layout"/>.</summary>
    public static IReadOnlyList<KeyStroke> Type(string text, LayoutId layout) =>
        text.Select(ch => Pair[layout].TryGetStroke(ch, out var stroke)
                ? stroke
                : throw new ArgumentException($"'{ch}' is not on the {layout} fixture"))
            .ToList();

    public static IReadOnlyList<TypedKey> Typed(string text, LayoutId layout) =>
        Type(text, layout).Select(s => new TypedKey(s, layout)).ToList();

    private static IEnumerable<ushort> Range(int from, int to) =>
        Enumerable.Range(from, to - from + 1).Select(i => (ushort)i);

    private static LayoutTable Build(string plain, string shifted)
    {
        if (plain.Length != Scans.Length || shifted.Length != Scans.Length)
            throw new InvalidOperationException("Fixture rows do not match the scan code list");

        var map = new Dictionary<KeyStroke, char>();
        for (var i = 0; i < Scans.Length; i++)
        {
            var (lo, hi) = (plain[i], shifted[i]);
            var isLetter = char.IsLetter(lo);
            map[new KeyStroke(Scans[i], Shift: false, CapsLock: false)] = lo;
            map[new KeyStroke(Scans[i], Shift: true, CapsLock: false)] = hi;
            map[new KeyStroke(Scans[i], Shift: false, CapsLock: true)] = isLetter ? hi : lo;
            map[new KeyStroke(Scans[i], Shift: true, CapsLock: true)] = isLetter ? lo : hi;
        }
        return new LayoutTable(map);
    }
}
