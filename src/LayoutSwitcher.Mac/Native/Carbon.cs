using System.Runtime.InteropServices;

namespace LayoutSwitcher.Mac.Native;

/// <summary>Text Input Sources, key translation, secure input and the accessibility check.</summary>
internal static unsafe class Carbon
{
    private const string Lib = "/System/Library/Frameworks/Carbon.framework/Carbon";
    private const string HiServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    public const ushort KeyActionDown = 0;
    public const uint ShiftKeyState = 0x200 >> 8;
    public const uint AlphaLockState = 0x400 >> 8;

    public static nint PropertyInputSourceCategory { get; } = CF.Symbol(Lib, "kTISPropertyInputSourceCategory");
    public static nint CategoryKeyboardInputSource { get; } = CF.Symbol(Lib, "kTISCategoryKeyboardInputSource");
    public static nint PropertyInputSourceIsSelectCapable { get; } = CF.Symbol(Lib, "kTISPropertyInputSourceIsSelectCapable");
    public static nint PropertyInputSourceLanguages { get; } = CF.Symbol(Lib, "kTISPropertyInputSourceLanguages");
    public static nint PropertyUnicodeKeyLayoutData { get; } = CF.Symbol(Lib, "kTISPropertyUnicodeKeyLayoutData");

    [DllImport(Lib)]
    public static extern nint TISCreateInputSourceList(nint properties, [MarshalAs(UnmanagedType.U1)] bool includeAllInstalled);

    [DllImport(Lib)]
    public static extern nint TISCopyCurrentKeyboardInputSource();

    [DllImport(Lib)]
    public static extern nint TISGetInputSourceProperty(nint source, nint key);

    [DllImport(Lib)]
    public static extern int TISSelectInputSource(nint source);

    [DllImport(Lib)]
    public static extern byte LMGetKbdType();

    [DllImport(Lib)]
    public static extern int UCKeyTranslate(nint layout, ushort keyCode, ushort keyAction, uint modifierKeyState,
        uint keyboardType, uint options, uint* deadKeyState, nuint maxLength, nuint* actualLength, char* text);

    /// <summary>True while a password field (or any app) holds secure input: the event tap then sees no keys.</summary>
    [DllImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool IsSecureEventInputEnabled();

    [DllImport(HiServices)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool AXIsProcessTrustedWithOptions(nint options);
}
