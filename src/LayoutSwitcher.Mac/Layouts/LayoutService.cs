using AppKit;
using Foundation;
using LayoutSwitcher.Core;
using LayoutSwitcher.Mac.Native;
using ObjCRuntime;

namespace LayoutSwitcher.Mac.Layouts;

/// <summary>
/// The enabled UA and EN input sources, and the one currently selected. macOS asserts that Text Input Source
/// calls run on the main thread, so the current layout is cached for the engine and refreshed from there.
/// </summary>
internal sealed class LayoutService
{
    private readonly nint _en;
    private readonly nint _ua;
    private volatile int _current = -1;

    private LayoutService(nint en, nint ua)
    {
        (_en, _ua) = (en, ua);
        Pair = new LayoutPair(LayoutTableBuilder.Build(en), LayoutTableBuilder.Build(ua));
        Refresh();
    }

    public LayoutPair Pair { get; }

    /// <summary>Null when the UA or EN layout is not enabled in System Settings. Main thread only.</summary>
    public static LayoutService? TryCreate()
    {
        using var filter = NSDictionary.FromObjectsAndKeys(
            [Runtime.GetNSObject(Carbon.CategoryKeyboardInputSource)!, NSNumber.FromBoolean(true)],
            [Runtime.GetNSObject(Carbon.PropertyInputSourceCategory)!, Runtime.GetNSObject(Carbon.PropertyInputSourceIsSelectCapable)!]);
        var list = Carbon.TISCreateInputSourceList(filter.Handle, includeAllInstalled: false);
        if (list == 0)
            return null;

        try
        {
            nint en = 0, ua = 0;
            for (nint i = 0; i < CF.CFArrayGetCount(list); i++)
            {
                var source = CF.CFArrayGetValueAtIndex(list, i);

                // Input methods (Pinyin, Kotoeri) have no key layout of their own.
                if (Carbon.TISGetInputSourceProperty(source, Carbon.PropertyUnicodeKeyLayoutData) == 0)
                    continue;
                if (en == 0 && Identify(source) == LayoutId.En)
                    en = source;
                if (ua == 0 && Identify(source) == LayoutId.Ua)
                    ua = source;
            }
            if (en == 0 || ua == 0)
                return null;

            // The list owns the sources; keep ours past its release.
            CF.CFRetain(en);
            CF.CFRetain(ua);
            return new LayoutService(en, ua);
        }
        finally
        {
            CF.CFRelease(list);
        }
    }

    /// <summary>The selected input source as of the last <see cref="Refresh"/>. Any thread.</summary>
    public LayoutId? GetFocusedLayout() => _current < 0 ? null : (LayoutId)_current;

    /// <summary>Main thread only.</summary>
    public void Refresh()
    {
        var source = Carbon.TISCopyCurrentKeyboardInputSource();
        if (source == 0)
        {
            _current = -1;
            return;
        }
        _current = Identify(source) is { } id ? (int)id : -1;
        CF.CFRelease(source);
    }

    /// <summary>Any thread; blocks until the main thread has switched.</summary>
    public void SetLayout(LayoutId layout) => NSApplication.SharedApplication.InvokeOnMainThread(() =>
    {
        Carbon.TISSelectInputSource(layout == LayoutId.En ? _en : _ua);
        Refresh();
    });

    /// <summary>By the source's primary language, so ABC, U.S., British and Ukrainian-PC all count.</summary>
    private static LayoutId? Identify(nint source)
    {
        var languages = Carbon.TISGetInputSourceProperty(source, Carbon.PropertyInputSourceLanguages);
        if (languages == 0 || CF.CFArrayGetCount(languages) == 0)
            return null;

        var primary = Runtime.GetNSObject<NSString>(CF.CFArrayGetValueAtIndex(languages, 0))?.ToString();
        return primary switch
        {
            "uk" => LayoutId.Ua,
            "en" or ['e', 'n', '-', ..] => LayoutId.En,
            _ => null,
        };
    }
}
