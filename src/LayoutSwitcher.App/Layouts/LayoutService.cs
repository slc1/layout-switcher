using LayoutSwitcher.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace LayoutSwitcher.App.Layouts;

/// <summary>The installed UA and EN layouts, and the layout of whatever window has keyboard focus.</summary>
internal sealed class LayoutService
{
    private const ushort LangUkrainian = 0x0422;
    private const ushort PrimaryLangEnglish = 0x09;

    private readonly HKL _en;
    private readonly HKL _ua;

    private LayoutService(HKL en, HKL ua)
    {
        (_en, _ua) = (en, ua);
        Pair = new LayoutPair(LayoutTableBuilder.Build(en), LayoutTableBuilder.Build(ua));
    }

    public LayoutPair Pair { get; }

    /// <summary>Null when the UA or EN layout is not installed.</summary>
    public static unsafe LayoutService? TryCreate()
    {
        var count = PInvoke.GetKeyboardLayoutList(0, null);
        var layouts = new HKL[count];
        fixed (HKL* p = layouts)
            count = PInvoke.GetKeyboardLayoutList(count, p);

        HKL? en = null, ua = null;
        foreach (var hkl in layouts.Take(count))
        {
            en ??= Identify(hkl) == LayoutId.En ? hkl : null;
            ua ??= Identify(hkl) == LayoutId.Ua ? hkl : null;
        }
        return en is { } e && ua is { } u ? new LayoutService(e, u) : null;
    }

    public LayoutId? GetFocusedLayout()
    {
        var target = FocusedWindow();
        if (target == HWND.Null)
            return null;
        var thread = GetThreadId(target);
        return Identify(PInvoke.GetKeyboardLayout(thread));
    }

    public unsafe void SetLayout(LayoutId layout)
    {
        var target = FocusedWindow();
        if (target == HWND.Null)
            return;
        var hkl = layout == LayoutId.En ? _en : _ua;
        PInvoke.PostMessage(target, PInvoke.WM_INPUTLANGCHANGEREQUEST, 0, (nint)hkl.Value);
    }

    public static unsafe LayoutId? Identify(HKL hkl)
    {
        var lang = (ushort)((nint)hkl.Value & 0xFFFF);
        if (lang == LangUkrainian)
            return LayoutId.Ua;
        if ((lang & 0x3FF) == PrimaryLangEnglish)
            return LayoutId.En;
        return null;
    }

    /// <summary>The control with keyboard focus; falls back to the foreground window.</summary>
    private static HWND FocusedWindow()
    {
        var foreground = PInvoke.GetForegroundWindow();
        if (foreground == HWND.Null)
            return HWND.Null;

        var info = new GUITHREADINFO { cbSize = (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<GUITHREADINFO>() };
        return PInvoke.GetGUIThreadInfo(GetThreadId(foreground), ref info) && info.hwndFocus != HWND.Null
            ? info.hwndFocus
            : foreground;
    }

    private static unsafe uint GetThreadId(HWND hwnd) => PInvoke.GetWindowThreadProcessId(hwnd, null);
}
