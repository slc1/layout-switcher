using System.Drawing.Drawing2D;
using System.Drawing.Text;
using LayoutSwitcher.Core;

namespace LayoutSwitcher.App.Ui;

/// <summary>
/// Tray icons with the layout name as text: EN on graphite, UA in blue and yellow,
/// grey when disabled, grey "!" when the app cannot work in the foreground (elevated) window.
/// </summary>
internal static class LayoutIcon
{
    public static Icon Create(LayoutId? layout, bool enabled, bool unavailable, Size size)
    {
        var (text, back, fore) = (layout, enabled, unavailable) switch
        {
            (_, false, _) => (Label(layout), Color.FromArgb(150, 150, 150), Color.White),
            (_, _, true) => ("!", Color.FromArgb(150, 150, 150), Color.White),
            (LayoutId.Ua, _, _) => ("UA", Color.FromArgb(0x00, 0x57, 0xB7), Color.FromArgb(0xFF, 0xD7, 0x00)),
            (LayoutId.En, _, _) => ("EN", Color.FromArgb(0x33, 0x33, 0x33), Color.White),
            _ => ("??", Color.FromArgb(150, 150, 150), Color.White),
        };

        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var radius = Math.Max(2, size.Width / 6);
            using (var path = RoundedRect(new Rectangle(0, 0, size.Width - 1, size.Height - 1), radius))
            using (var brush = new SolidBrush(back))
                g.FillPath(brush, path);

            using var font = new Font("Segoe UI", size.Height * 0.5f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(fore);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, textBrush, new RectangleF(0, 0, size.Width, size.Height), format);
        }

        // Icon.FromHandle does not own the handle: clone, then free the original.
        var handle = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            Windows.Win32.PInvoke.DestroyIcon(new Windows.Win32.UI.WindowsAndMessaging.HICON(handle));
        }
    }

    private static string Label(LayoutId? layout) => layout switch
    {
        LayoutId.Ua => "UA",
        LayoutId.En => "EN",
        _ => "??",
    };

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
