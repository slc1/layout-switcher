using System.Drawing.Imaging;
using LayoutSwitcher.App.Ui;
using LayoutSwitcher.Core;
using LayoutSwitcher.Core.Config;

namespace LayoutSwitcher.App;

/// <summary>
/// Regenerates the README images. No hooks are installed; the settings window shows defaults, never the user's settings.
/// Run: LayoutSwitcher.exe --docs-images docs/images
/// </summary>
internal static class DocsImages
{
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        ApplicationConfiguration.Initialize();

        SaveTrayIcons(Path.Combine(directory, "tray-icons.png"));

        using var form = new SettingsForm(new Settings()) { TopMost = true, ShowInTaskbar = false };
        form.Shown += async (_, _) =>
        {
            await Task.Delay(500); // let the window finish painting

            // PrintWindow renders only this window. Never copy screen pixels: whatever covers or sits under
            // the window (other apps, personal data) would end up in a published image.
            using var bitmap = new Bitmap(form.Width, form.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                var hdc = g.GetHdc();
                try
                {
                    Windows.Win32.PInvoke.PrintWindow((Windows.Win32.Foundation.HWND)form.Handle,
                        (Windows.Win32.Graphics.Gdi.HDC)hdc, (Windows.Win32.Storage.Xps.PRINT_WINDOW_FLAGS)2 /* PW_RENDERFULLCONTENT */);
                }
                finally
                {
                    g.ReleaseHdc(hdc);
                }
            }
            bitmap.Save(Path.Combine(directory, "settings.png"), ImageFormat.Png);
            form.Close();
        };
        Application.Run(form);
        return 0;
    }

    private static void SaveTrayIcons(string path)
    {
        const int size = 48;
        const int gap = 16;
        (LayoutId? Layout, bool Enabled, bool Unavailable)[] states =
        [
            (LayoutId.En, true, false), (LayoutId.Ua, true, false), (LayoutId.Ua, false, false), (LayoutId.Ua, true, true),
        ];

        using var strip = new Bitmap(states.Length * size + (states.Length - 1) * gap, size);
        using (var g = Graphics.FromImage(strip))
        {
            g.Clear(Color.Transparent);
            for (var i = 0; i < states.Length; i++)
            {
                var (layout, enabled, unavailable) = states[i];
                using var icon = LayoutIcon.Create(layout, enabled, unavailable, new Size(size, size));
                using var image = icon.ToBitmap();
                g.DrawImage(image, i * (size + gap), 0, size, size);
            }
        }
        strip.Save(path, ImageFormat.Png);
    }
}
