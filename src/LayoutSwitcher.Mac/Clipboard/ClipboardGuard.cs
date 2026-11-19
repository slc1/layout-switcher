using AppKit;
using Foundation;

namespace LayoutSwitcher.Mac.Clipboard;

/// <summary>
/// Snapshot and restore of the user's pasteboard around our own Cmd+C / Cmd+V.
/// Every method must run on the main thread.
/// </summary>
internal sealed class ClipboardGuard
{
    // nspasteboard.org convention: clipboard managers skip items marked transient.
    private const string TransientType = "org.nspasteboard.TransientType";

    private static NSPasteboard Board => NSPasteboard.GeneralPasteboard;

    private List<List<(string Type, NSData Data)>>? _snapshot;

    public static nint ChangeCount => Board.ChangeCount;

    /// <summary>Copies every type of every item, so files, images and rich text all come back.</summary>
    public void Backup()
    {
        _snapshot = [];
        foreach (var item in Board.PasteboardItems ?? [])
        {
            var saved = new List<(string, NSData)>();
            foreach (var type in item.Types ?? [])
            {
                if (item.GetDataForType(type) is { } data)
                    saved.Add((type, data));
            }
            _snapshot.Add(saved);
        }
    }

    /// <summary>Text the app put on the pasteboard after our Cmd+C, or null.</summary>
    public string? ReadText() => Board.GetStringForType(NSPasteboardType.String.GetConstant()!);

    public void SetText(string text)
    {
        var item = new NSPasteboardItem();
        item.SetStringForType(text, NSPasteboardType.String.GetConstant()!);
        Write([item]);
    }

    /// <summary>The pasteboard was never touched: keep the original as it is.</summary>
    public void Discard() => _snapshot = null;

    /// <summary>Puts the snapshot back and drops our reference to it.</summary>
    public void Restore()
    {
        if (_snapshot is null)
            return;

        var items = _snapshot.Select(saved =>
        {
            var item = new NSPasteboardItem();
            foreach (var (type, data) in saved)
                item.SetDataForType(data, type);
            return item;
        }).ToArray();

        if (items.Length == 0)
            Board.ClearContents();
        else
            Write(items);
        _snapshot = null;
    }

    /// <summary>Our writes, the restore included, stay out of clipboard-manager history.</summary>
    private static void Write(NSPasteboardItem[] items)
    {
        items[0].SetDataForType(new NSData(), TransientType);
        Board.ClearContents();
        Board.WriteObjects(items);
    }
}
