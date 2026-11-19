using System.Runtime.InteropServices;

namespace LayoutSwitcher.Mac.Native;

/// <summary>Quartz event services: the event tap and synthetic keyboard events.</summary>
internal static unsafe class CG
{
    private const string Lib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    public const int HidEventTap = 0;
    public const int SessionEventTap = 1;
    public const int HeadInsertEventTap = 0;
    public const int TapOptionDefault = 0;
    public const int SourceStatePrivate = -1;
    public const int SourceStateHidSystem = 1;

    public const int EventLeftMouseDown = 1;
    public const int EventRightMouseDown = 3;
    public const int EventKeyDown = 10;
    public const int EventKeyUp = 11;
    public const int EventOtherMouseDown = 25;
    public const int EventTapDisabledByTimeout = unchecked((int)0xFFFFFFFE);
    public const int EventTapDisabledByUserInput = unchecked((int)0xFFFFFFFF);

    public const int FieldKeyboardEventAutorepeat = 8;
    public const int FieldKeyboardEventKeycode = 9;
    public const int FieldEventSourceUserData = 42;

    public const ulong FlagAlphaShift = 0x10000;
    public const ulong FlagShift = 0x20000;
    public const ulong FlagControl = 0x40000;
    public const ulong FlagAlternate = 0x80000;
    public const ulong FlagCommand = 0x100000;
    public const ulong FlagNumericPad = 0x200000;

    public static ulong MaskFor(params int[] types) => types.Aggregate(0UL, (mask, type) => mask | (1UL << type));

    [DllImport(Lib)]
    public static extern nint CGEventTapCreate(int tap, int place, int options, ulong eventsOfInterest,
        delegate* unmanaged<nint, int, nint, nint, nint> callback, nint userInfo);

    [DllImport(Lib)]
    public static extern void CGEventTapEnable(nint tap, [MarshalAs(UnmanagedType.U1)] bool enable);

    [DllImport(Lib)]
    public static extern ulong CGEventGetFlags(nint ev);

    [DllImport(Lib)]
    public static extern void CGEventSetFlags(nint ev, ulong flags);

    [DllImport(Lib)]
    public static extern long CGEventGetIntegerValueField(nint ev, int field);

    [DllImport(Lib)]
    public static extern void CGEventSetIntegerValueField(nint ev, int field, long value);

    [DllImport(Lib)]
    public static extern nint CGEventSourceCreate(int stateId);

    [DllImport(Lib)]
    public static extern ulong CGEventSourceFlagsState(int stateId);

    [DllImport(Lib)]
    public static extern nint CGEventCreateKeyboardEvent(nint source, ushort keycode,
        [MarshalAs(UnmanagedType.U1)] bool keyDown);

    [DllImport(Lib)]
    public static extern void CGEventKeyboardSetUnicodeString(nint ev, nuint length, char* text);

    [DllImport(Lib)]
    public static extern void CGEventPost(int tap, nint ev);
}
