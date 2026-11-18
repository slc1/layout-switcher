using System.Runtime.InteropServices;
using System.Text;
using LayoutSwitcher.App.Clipboard;
using LayoutSwitcher.App.Engine;
using LayoutSwitcher.App.Input;
using LayoutSwitcher.App.Layouts;
using LayoutSwitcher.Core;
using LayoutSwitcher.Core.Config;
using LayoutSwitcher.Core.Input;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace LayoutSwitcher.App;

/// <summary>
/// End-to-end check with the real hook, engine, SendInput and clipboard, against a test window.
/// Run: LayoutSwitcher.exe --e2e &lt;report-file&gt;   (takes ~10 s; do not type meanwhile; CapsLock off).
/// It steals focus for its own window, types fixed sample text there, and restores the clipboard.
/// Not for use while the tray app is running: two hooks would both convert.
/// </summary>
internal static class E2eCheck
{
    private const ushort ScanPause = 0x45;

    public static int Run(string reportPath)
    {
        var report = new StringBuilder();
        var layouts = LayoutService.TryCreate();
        if (layouts is null)
        {
            File.WriteAllText(reportPath, "FAIL: UA or EN layout is not installed\n");
            return 1;
        }

        ApplicationConfiguration.Initialize();
        using var form = new Form
        {
            Text = "LayoutSwitcher e2e — do not type", TopMost = true, Width = 700, Height = 160,
            StartPosition = FormStartPosition.CenterScreen,
        };
        var box = new TextBox { Multiline = true, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 16) };
        form.Controls.Add(box);

        var ok = false;
        var userClipboard = new ClipboardGuard();
        form.Shown += async (_, _) =>
        {
            userClipboard.Backup();
            var host = new Host(form, layouts);
            using var services = AppServices.Start(layouts, host, new Settings());
            try
            {
                ok = await Task.Run(() => new Script(form, box, layouts, report).Run());
            }
            catch (Exception e)
            {
                report.AppendLine($"FAIL: exception {e.GetType().Name}: {e.Message}");
            }
            userClipboard.Restore();
            form.Close();
        };
        Application.Run(form);

        report.AppendLine(ok ? "RESULT: PASS" : "RESULT: FAIL");
        File.WriteAllText(reportPath, report.ToString());
        return ok ? 0 : 1;
    }

    private sealed class Host(Form form, LayoutService layouts) : IEngineHost
    {
        private readonly SelectionConverter _selection = new(a => form.Invoke(a), layouts.Pair);

        public void ToggleEnabled() { }

        public void WarnElevated() { }

        public LayoutId? ConvertSelection(HeldModifiers held) => _selection.Convert(held);
    }

    private sealed class Script(Form form, TextBox box, LayoutService layouts, StringBuilder report)
    {
        private bool _ok = true;

        public bool Run()
        {
            if (!Focus())
            {
                report.AppendLine("FAIL: could not bring the test window to the foreground");
                return false;
            }

            Start(LayoutId.En);
            Type("ghbdsn", LayoutId.En);
            Press(VirtualKeys.Pause);
            Expect("Pause converts last word", "привіт");
            ExpectLayout(LayoutId.Ua);

            Press(VirtualKeys.Pause);
            Expect("second Pause undoes", "ghbdsn");

            Start(LayoutId.En);
            Type("ghbdsn cdsn", LayoutId.En);
            Press(VirtualKeys.Pause, VirtualKeys.LControl);
            Expect("Ctrl+Pause converts all typed", "привіт світ");

            Start(LayoutId.En);
            Type("Ghbdsn, ", LayoutId.En);
            Press(VirtualKeys.Pause);
            Expect("capital, punctuation key, trailing space", "Привітб ");

            Start(LayoutId.Ua);
            Type("руддщ", LayoutId.Ua);
            Press(VirtualKeys.Pause);
            Expect("Ukrainian typed by mistake", "hello");

            Start(LayoutId.En);
            form.Invoke(() =>
            {
                System.Windows.Forms.Clipboard.SetText("ORIGINAL");
                box.Text = "ghbdsn";
                box.SelectAll();
            });
            Press(VirtualKeys.Pause, VirtualKeys.LShift);
            Thread.Sleep(500);
            Expect("Shift+Pause converts selection", "привіт");
            var clip = (string?)form.Invoke(() => System.Windows.Forms.Clipboard.GetText());
            Check("clipboard restored after selection", clip == "ORIGINAL", clip ?? "<null>");

            return _ok;
        }

        private bool Focus()
        {
            for (var i = 0; i < 20; i++)
            {
                // Windows only lets the process that produced the last input take the foreground.
                Tap(0xE8, 0);
                form.Invoke(() =>
                {
                    PInvoke.SetForegroundWindow((Windows.Win32.Foundation.HWND)form.Handle);
                    form.Activate();
                    box.Focus();
                });
                Thread.Sleep(100);
                if (PInvoke.GetForegroundWindow() == (Windows.Win32.Foundation.HWND)form.Handle)
                    return true;
            }
            return false;
        }

        /// <summary>Empty box, reset buffer (Esc), switch layout.</summary>
        private void Start(LayoutId layout)
        {
            form.Invoke(() => box.Text = "");
            Tap(VirtualKeys.Escape, 0x01);
            layouts.SetLayout(layout);
            Thread.Sleep(250);
        }

        private void Type(string text, LayoutId layout)
        {
            foreach (var ch in text)
            {
                if (!layouts.Pair[layout].TryGetStroke(ch, out var stroke))
                    throw new InvalidOperationException($"'{ch}' not on the {layout} layout");
                var vk = (ushort)PInvoke.MapVirtualKeyEx(stroke.ScanCode, MAP_VIRTUAL_KEY_TYPE.MAPVK_VSC_TO_VK_EX, default);
                if (stroke.Shift)
                    Send(VirtualKeys.LShift, 0x2A, up: false);
                Tap(vk, stroke.ScanCode);
                if (stroke.Shift)
                    Send(VirtualKeys.LShift, 0x2A, up: true);
                Thread.Sleep(15);
            }
            Thread.Sleep(100);
        }

        private void Press(ushort vk, ushort modifierVk = 0)
        {
            var modifierScan = modifierVk == VirtualKeys.LControl ? (ushort)0x1D : (ushort)0x2A;
            if (modifierVk != 0)
                Send(modifierVk, modifierScan, up: false);
            Tap(vk, vk == VirtualKeys.Pause ? ScanPause : (ushort)0);
            if (modifierVk != 0)
                Send(modifierVk, modifierScan, up: true);
            Thread.Sleep(400);
        }

        private void Expect(string name, string expected)
        {
            var actual = (string)form.Invoke(() => box.Text);
            Check(name, actual == expected, actual);
            if (actual != expected)
                report.AppendLine($"      foreground now: {DescribeForeground()}");
        }

        /// <summary>Window class and process of the foreground window — never its text.</summary>
        private unsafe string DescribeForeground()
        {
            var hwnd = PInvoke.GetForegroundWindow();
            if (hwnd == (Windows.Win32.Foundation.HWND)form.Handle)
                return "test window";
            Span<char> name = stackalloc char[128];
            int length;
            fixed (char* p = name)
                length = PInvoke.GetClassName(hwnd, p, name.Length);
            var pid = Win32.ProcessInfo.ForegroundProcessId();
            var exe = Path.GetFileName(Win32.ProcessInfo.ImagePath(pid) ?? "?");
            return $"class={name[..Math.Max(0, length)]} exe={exe}";
        }

        private void ExpectLayout(LayoutId expected)
        {
            var actual = layouts.GetFocusedLayout();
            Check($"layout switched to {expected}", actual == expected, actual?.ToString() ?? "unknown");
        }

        private void Check(string name, bool pass, string actual)
        {
            _ok &= pass;
            report.AppendLine($"{(pass ? "PASS" : "FAIL")}: {name} -> '{actual}'");
        }

        private static void Tap(ushort vk, ushort scan)
        {
            Send(vk, scan, up: false);
            Send(vk, scan, up: true);
        }

        /// <summary>Untagged, so our hook treats it as the user typing.</summary>
        private static void Send(ushort vk, ushort scan, bool up)
        {
            INPUT[] input =
            [
                new()
                {
                    type = INPUT_TYPE.INPUT_KEYBOARD,
                    Anonymous =
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = (VIRTUAL_KEY)vk, wScan = scan, dwFlags = up ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0,
                        },
                    },
                },
            ];
            PInvoke.SendInput(input, Marshal.SizeOf<INPUT>());
        }
    }
}
