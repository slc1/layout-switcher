using AppKit;
using CoreGraphics;
using Foundation;
using LayoutSwitcher.Core;

namespace LayoutSwitcher.Mac.Ui;

/// <summary>
/// Menu bar icons with the layout name as text: EN on graphite, UA in blue and yellow,
/// grey when disabled, grey "!" when the app cannot work right now.
/// </summary>
internal static class LayoutIcon
{
    private static readonly CGSize Size = new(26, 16);

    public static NSImage Create(LayoutId? layout, bool enabled, bool unavailable)
    {
        var grey = NSColor.FromRgb(150, 150, 150);
        var (text, back, fore) = (layout, enabled, unavailable) switch
        {
            (_, false, _) => (Label(layout), grey, NSColor.White),
            (_, _, true) => ("!", grey, NSColor.White),
            (LayoutId.Ua, _, _) => ("UA", NSColor.FromRgb(0x00, 0x57, 0xB7), NSColor.FromRgb(0xFF, 0xD7, 0x00)),
            (LayoutId.En, _, _) => ("EN", NSColor.FromRgb(0x33, 0x33, 0x33), NSColor.White),
            _ => ("??", grey, NSColor.White),
        };

        // Drawn on demand, so the image stays sharp on Retina and non-Retina screens alike.
        return NSImage.ImageWithSize(Size, false, rect =>
        {
            back.SetFill();
            NSBezierPath.FromRoundedRect(rect, 4, 4).Fill();

            var label = new NSAttributedString(text, new NSStringAttributes
            {
                Font = NSFont.BoldSystemFontOfSize(10.5f),
                ForegroundColor = fore,
            });
            var size = label.Size;
            label.DrawAtPoint(new CGPoint((rect.Width - size.Width) / 2, (rect.Height - size.Height) / 2));
            return true;
        });
    }

    private static string Label(LayoutId? layout) => layout switch
    {
        LayoutId.Ua => "UA",
        LayoutId.En => "EN",
        _ => "??",
    };
}
