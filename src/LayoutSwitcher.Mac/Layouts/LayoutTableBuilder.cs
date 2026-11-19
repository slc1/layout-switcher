using LayoutSwitcher.Core;
using LayoutSwitcher.Mac.Native;

namespace LayoutSwitcher.Mac.Layouts;

/// <summary>Reads what each key types in an input source, via UCKeyTranslate. Main thread only.</summary>
internal static class LayoutTableBuilder
{
    // Every key that types a character on ANSI and ISO keyboards (10 is the ISO § key), plus Space.
    // Not Return (36) or Tab (48).
    private static readonly ushort[] KeyCodes = [.. Range(0, 35), .. Range(37, 47), 49, 50];

    public static unsafe LayoutTable Build(nint source)
    {
        var map = new Dictionary<KeyStroke, char>();
        var data = Carbon.TISGetInputSourceProperty(source, Carbon.PropertyUnicodeKeyLayoutData);
        if (data == 0)
            return new LayoutTable(map);

        var layout = CF.CFDataGetBytePtr(data);
        var keyboardType = (uint)Carbon.LMGetKbdType();
        var buffer = stackalloc char[4];

        foreach (var key in KeyCodes)
        foreach (var shift in (bool[])[false, true])
        foreach (var caps in (bool[])[false, true])
        {
            var modifiers = (shift ? Carbon.ShiftKeyState : 0) | (caps ? Carbon.AlphaLockState : 0);
            uint deadKeyState = 0;
            nuint length = 0;
            var status = Carbon.UCKeyTranslate(layout, key, Carbon.KeyActionDown, modifiers, keyboardType, 0,
                &deadKeyState, 4, &length, buffer);

            // A dead key types nothing by itself, so it cannot be retyped as one character.
            if (status == 0 && deadKeyState == 0 && length == 1 && !char.IsControl(buffer[0]))
                map[new KeyStroke(key, shift, caps)] = buffer[0];
        }
        return new LayoutTable(map);
    }

    private static IEnumerable<ushort> Range(int from, int to) =>
        Enumerable.Range(from, to - from + 1).Select(i => (ushort)i);
}
