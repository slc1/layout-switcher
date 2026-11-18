using LayoutSwitcher.App.Clipboard;
using LayoutSwitcher.App.Input;
using LayoutSwitcher.Core;
using Windows.Win32;

namespace LayoutSwitcher.App.Engine;

/// <summary>
/// Copy the selection, convert it, paste it back, restore the user's clipboard (design §7.2).
/// Runs on the engine thread; clipboard calls are marshalled to the UI thread.
/// </summary>
internal sealed class SelectionConverter(Func<Func<object?>, object?> onUiThread, LayoutPair pair)
{
    private static readonly TimeSpan CopyTimeout = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan PasteSettle = TimeSpan.FromMilliseconds(300);
    private const ushort VkC = 'C';
    private const ushort VkV = 'V';

    private readonly ClipboardGuard _clipboard = new();

    public LayoutId? Convert(HeldModifiers held)
    {
        Ui(() => _clipboard.Backup());
        try
        {
            InputSender.ReleaseModifiers(held);

            var before = PInvoke.GetClipboardSequenceNumber();
            InputSender.SendCtrl(VkC);
            if (!WaitForClipboardChange(before))
                return null; // nothing selected

            var text = (string?)onUiThread(() => _clipboard.ReadText());
            if (text is null || !TextConverter.TryConvertSelection(text, pair, out var converted, out var target))
                return null;

            Ui(() => _clipboard.SetText(converted));
            InputSender.SendCtrl(VkV);
            Thread.Sleep(PasteSettle); // the app reads the clipboard asynchronously after Ctrl+V
            return target;
        }
        finally
        {
            Ui(() => _clipboard.Restore());
        }
    }

    private static bool WaitForClipboardChange(uint before)
    {
        var deadline = Environment.TickCount64 + (long)CopyTimeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (PInvoke.GetClipboardSequenceNumber() != before)
                return true;
            Thread.Sleep(10);
        }
        return false;
    }

    private void Ui(Action action) => onUiThread(() =>
    {
        action();
        return null;
    });
}
