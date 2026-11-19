using System.Runtime.InteropServices;

namespace LayoutSwitcher.Mac.Native;

internal static class CF
{
    private const string Lib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(Lib)]
    public static extern nint CFMachPortCreateRunLoopSource(nint allocator, nint port, nint order);

    [DllImport(Lib)]
    public static extern nint CFRunLoopGetCurrent();

    [DllImport(Lib)]
    public static extern void CFRunLoopAddSource(nint runLoop, nint source, nint mode);

    [DllImport(Lib)]
    public static extern void CFRunLoopRun();

    [DllImport(Lib)]
    public static extern void CFRunLoopStop(nint runLoop);

    [DllImport(Lib)]
    public static extern void CFMachPortInvalidate(nint port);

    [DllImport(Lib)]
    public static extern nint CFRetain(nint obj);

    [DllImport(Lib)]
    public static extern void CFRelease(nint obj);

    [DllImport(Lib)]
    public static extern nint CFArrayGetCount(nint array);

    [DllImport(Lib)]
    public static extern nint CFArrayGetValueAtIndex(nint array, nint index);

    [DllImport(Lib)]
    public static extern nint CFDataGetBytePtr(nint data);

    public static nint DefaultRunLoopMode { get; } = Symbol(Lib, "kCFRunLoopDefaultMode");

    /// <summary>Reads an exported <c>CFStringRef</c> constant.</summary>
    public static nint Symbol(string library, string name) =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(NativeLibrary.Load(library), name));
}
