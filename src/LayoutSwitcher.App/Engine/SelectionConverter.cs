using LayoutSwitcher.App.Clipboard;
using LayoutSwitcher.App.Input;
using LayoutSwitcher.Core;
using LayoutSwitcher.Core.Input;
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
        // In a console Ctrl+C is "interrupt", not "copy".
        if (IsConsole(PInvoke.GetForegroundWindow()))
            return null;

        Ui(() => _clipboard.Backup());
        var afterBackup = PInvoke.GetClipboardSequenceNumber();
        try
        {
            InputSender.ReleaseModifiers(held);

            InputSender.SendCtrl(VkC);
            if (!WaitForClipboardChange(afterBackup))
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
            // Restoring rewrites the clipboard with only the formats we saved, so skip it when nothing changed.
            if (PInvoke.GetClipboardSequenceNumber() == afterBackup)
                Ui(() => _clipboard.Discard());
            else
                Ui(() => _clipboard.Restore());
        }
    }

    private static unsafe bool IsConsole(Windows.Win32.Foundation.HWND hwnd)
    {
        Span<char> name = stackalloc char[64];
        int length;
        fixed (char* p = name)
            length = PInvoke.GetClassName(hwnd, p, name.Length);
        var className = name[..Math.Max(0, length)];
        return className.SequenceEqual("ConsoleWindowClass") || className.SequenceEqual("CASCADIA_HOSTING_WINDOW_CLASS");
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
