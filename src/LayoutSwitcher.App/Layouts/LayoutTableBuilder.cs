using LayoutSwitcher.Core;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace LayoutSwitcher.App.Layouts;

/// <summary>Reads what each key types in an installed layout, via ToUnicodeEx.</summary>
internal static class LayoutTableBuilder
{
    private const byte KeyDown = 0x80;
    private const byte Toggled = 0x01;

    // Do not touch the kernel's dead-key state (Windows 10 1607+); otherwise building a table would
    // swallow a dead key the user is in the middle of typing.
    private const uint DoNotChangeKeyboardState = 0x4;

    private static readonly ushort[] ScanCodes =
    [
        0x29, .. Range(0x02, 0x0D), .. Range(0x10, 0x1B), .. Range(0x1E, 0x28), 0x2B, .. Range(0x2C, 0x35), 0x39, 0x56,
    ];

    public static unsafe LayoutTable Build(HKL hkl)
    {
        var map = new Dictionary<KeyStroke, char>();
        var state = stackalloc byte[256];
        var buffer = stackalloc char[8];

        foreach (var scan in ScanCodes)
        {
            var vk = PInvoke.MapVirtualKeyEx(scan, MAP_VIRTUAL_KEY_TYPE.MAPVK_VSC_TO_VK_EX, hkl);
            if (vk == 0)
                continue;

            foreach (var shift in (bool[])[false, true])
            foreach (var caps in (bool[])[false, true])
            {
                new Span<byte>(state, 256).Clear();
                if (shift)
                    state[(int)VIRTUAL_KEY.VK_SHIFT] = KeyDown;
                if (caps)
                    state[(int)VIRTUAL_KEY.VK_CAPITAL] = Toggled;

                var count = PInvoke.ToUnicodeEx(vk, scan, state, buffer, 8, DoNotChangeKeyboardState, hkl);
                if (count == 1 && !char.IsControl(buffer[0]))
                    map[new KeyStroke(scan, shift, caps)] = buffer[0];
            }
        }
        return new LayoutTable(map);
    }

    private static IEnumerable<ushort> Range(int from, int to) =>
        Enumerable.Range(from, to - from + 1).Select(i => (ushort)i);
}
