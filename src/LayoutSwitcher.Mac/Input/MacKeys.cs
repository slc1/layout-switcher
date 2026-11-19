using LayoutSwitcher.Core.Input;
using LayoutSwitcher.Mac.Native;

namespace LayoutSwitcher.Mac.Input;

/// <summary>
/// Mac virtual key codes (<c>kVK_*</c>) translated to the Win32 virtual keys that Core's hotkeys and key
/// classifier speak. Letters and digits follow the ANSI key labels, whatever the active input source.
/// </summary>
internal static class MacKeys
{
    public const ushort A = 0x00;
    public const ushort C = 0x08;
    public const ushort V = 0x09;
    public const ushort Delete = 0x33;

    private static readonly Dictionary<ushort, ushort> ToVirtualKey = Build();

    /// <summary>0 for keys Core has no name for, such as punctuation; the classifier records those.</summary>
    public static ushort ToVk(ushort keyCode) => ToVirtualKey.GetValueOrDefault(keyCode);

    public static Modifiers ModifiersFrom(ulong flags)
    {
        var result = Modifiers.None;
        if ((flags & CG.FlagControl) != 0)
            result |= Modifiers.Ctrl;
        if ((flags & CG.FlagAlternate) != 0)
            result |= Modifiers.Alt;
        if ((flags & CG.FlagShift) != 0)
            result |= Modifiers.Shift;
        if ((flags & CG.FlagCommand) != 0)
            result |= Modifiers.Win;
        return result;
    }

    private static Dictionary<ushort, ushort> Build()
    {
        var map = new Dictionary<ushort, ushort>();

        const string letters = "ASDFHGZXCV\0BQWERYT";
        for (var i = 0; i < letters.Length; i++)
        {
            if (letters[i] != '\0')
                map[(ushort)i] = letters[i];
        }
        foreach (var (code, ch) in new (ushort, char)[]
                 {
                     (0x1F, 'O'), (0x20, 'U'), (0x22, 'I'), (0x23, 'P'), (0x25, 'L'), (0x26, 'J'), (0x28, 'K'),
                     (0x2D, 'N'), (0x2E, 'M'),
                     (0x12, '1'), (0x13, '2'), (0x14, '3'), (0x15, '4'), (0x17, '5'), (0x16, '6'), (0x1A, '7'),
                     (0x1C, '8'), (0x19, '9'), (0x1D, '0'),
                 })
            map[code] = ch;

        ushort[] functionKeys =
        [
            0x7A, 0x78, 0x63, 0x76, 0x60, 0x61, 0x62, 0x64, 0x65, 0x6D, 0x67, 0x6F, // F1–F12
            0x69, 0x6B, 0x71, 0x6A, 0x40, 0x4F, 0x50, 0x5A, // F13–F20
        ];
        for (var i = 0; i < functionKeys.Length; i++)
            map[functionKeys[i]] = (ushort)(VirtualKeys.F1 + i);

        foreach (var (code, vk) in new (ushort, ushort)[]
                 {
                     (0x24, VirtualKeys.Return), (0x4C, VirtualKeys.Return), (0x30, VirtualKeys.Tab),
                     (0x31, VirtualKeys.Space), (Delete, VirtualKeys.Back), (0x35, VirtualKeys.Escape),
                     (0x75, VirtualKeys.Delete), (0x72, VirtualKeys.Insert), // forward delete, Help
                     (0x73, VirtualKeys.Home), (0x77, VirtualKeys.End), (0x74, VirtualKeys.Prior), (0x79, VirtualKeys.Next),
                     (0x7B, VirtualKeys.Left), (0x7C, VirtualKeys.Right), (0x7D, VirtualKeys.Down), (0x7E, VirtualKeys.Up),
                     (0x39, VirtualKeys.CapsLock),
                     (0x38, VirtualKeys.LShift), (0x3C, VirtualKeys.RShift), (0x3B, VirtualKeys.LControl),
                     (0x3E, VirtualKeys.RControl), (0x3A, VirtualKeys.LMenu), (0x3D, VirtualKeys.RMenu),
                     (0x37, VirtualKeys.LWin), (0x36, VirtualKeys.RWin),
                 })
            map[code] = vk;

        return map;
    }
}
