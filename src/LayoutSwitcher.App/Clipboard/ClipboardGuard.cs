using System.Runtime.InteropServices;

namespace LayoutSwitcher.App.Clipboard;

/// <summary>
/// Snapshot and restore of the user's clipboard around our own Ctrl+C / Ctrl+V.
/// Every method must run on the STA UI thread.
/// </summary>
internal sealed class ClipboardGuard
{
    // Formats a user is likely to have copied. Anything else is lost for the ~1 s the conversion takes.
    private static readonly string[] SavedFormats =
    [
        DataFormats.UnicodeText, DataFormats.Text, DataFormats.Rtf, DataFormats.Html, DataFormats.FileDrop,
        DataFormats.Dib, DataFormats.Bitmap, "Preferred DropEffect", "Shell IDList Array",
    ];

    private List<(string Format, object Data)>? _snapshot;

    public void Backup()
    {
        _snapshot = [];
        var current = Retry(System.Windows.Forms.Clipboard.GetDataObject);
        if (current is null)
            return;

        foreach (var format in SavedFormats)
        {
            try
            {
                if (current.GetDataPresent(format, autoConvert: false)
                    && current.GetData(format, autoConvert: false) is { } data)
                    _snapshot.Add((format, data));
            }
            catch (Exception e) when (e is ExternalException or NotSupportedException or InvalidOperationException)
            {
                // An app put something we cannot read; skip that format.
            }
        }
    }

    /// <summary>Text the app put on the clipboard after our Ctrl+C, or null.</summary>
    public string? ReadText() =>
        Retry(() => System.Windows.Forms.Clipboard.ContainsText() ? System.Windows.Forms.Clipboard.GetText() : null);

    public void SetText(string text)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, text);
        Write(data);
    }

    /// <summary>Puts the snapshot back and drops our reference to it.</summary>
    public void Restore()
    {
        if (_snapshot is null)
            return;

        if (_snapshot.Count == 0)
        {
            Retry(() =>
            {
                System.Windows.Forms.Clipboard.Clear();
                return true;
            });
        }
        else
        {
            var data = new DataObject();
            foreach (var (format, value) in _snapshot)
                data.SetData(format, value);
            Write(data);
        }
        _snapshot = null;
    }

    /// <summary>Our writes stay out of Win+V history and cloud clipboard sync.</summary>
    private static void Write(DataObject data)
    {
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream([0, 0, 0, 0]));
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream([0, 0, 0, 0]));
        System.Windows.Forms.Clipboard.SetDataObject(data, copy: true, retryTimes: 10, retryDelay: 20);
    }

    /// <summary>Another app may hold the clipboard open for a moment.</summary>
    private static T? Retry<T>(Func<T?> action)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (ExternalException) when (attempt < 10)
            {
                Thread.Sleep(20);
            }
            catch (ExternalException)
            {
                return default;
            }
        }
    }
}
