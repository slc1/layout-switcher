using System.Text;
using LayoutSwitcher.Core;
using LayoutSwitcher.Core.Config;
using LayoutSwitcher.Mac.Layouts;

namespace LayoutSwitcher.Mac;

/// <summary>
/// Diagnostics without UI or event tap, so no permissions are needed. Writes only fixed sample strings, never
/// user input. Run: LayoutSwitcher.app/Contents/MacOS/LayoutSwitcher --self-test &lt;report-file&gt;
/// </summary>
internal static class SelfTest
{
    public static int Run(string reportPath)
    {
        var report = new StringBuilder();
        var layouts = LayoutService.TryCreate();
        if (layouts is null)
        {
            report.AppendLine("FAIL: UA or EN input source is not enabled");
            File.WriteAllText(reportPath, report.ToString());
            return 1;
        }

        var ok = true;
        void Check(string input, LayoutId from, string expected)
        {
            var actual = TextConverter.Convert(input, layouts.Pair[from], layouts.Pair[from.Other()]);
            var pass = actual == expected;
            ok &= pass;
            report.AppendLine($"{(pass ? "PASS" : "FAIL")}: {input} ({from}) -> {actual} (expected {expected})");
        }

        Check("ghbdsn cdsn", LayoutId.En, "привіт світ");
        Check("Ghbdsn, 2024!", LayoutId.En, "Привітб 2024!");
        Check("e;t", LayoutId.En, "уже");
        Check("руддщ цщкдв", LayoutId.Ua, "hello world");

        // Release builds are trimmed; settings must still load and save.
        var settingsPath = Path.Combine(Path.GetTempPath(), $"layoutswitcher-selftest-{Environment.ProcessId}.json");
        try
        {
            var store = new SettingsStore(settingsPath);
            var settings = store.Load();
            settings.Hotkeys.ConvertLastWord = "Ctrl+Option+Z";
            store.Save(settings);
            var loaded = store.Load().Hotkeys.ConvertLastWord;
            var pass = loaded == "Ctrl+Option+Z";
            ok &= pass;
            report.AppendLine($"{(pass ? "PASS" : "FAIL")}: settings round trip -> {loaded}");
        }
        finally
        {
            File.Delete(settingsPath);
        }

        // Depends on the enabled Ukrainian variant, so informational only.
        report.AppendLine($"INFO: v`znf (En) -> {TextConverter.Convert("v`znf", layouts.Pair.En, layouts.Pair.Ua)}");
        report.AppendLine($"INFO: table sizes En={Count(layouts.Pair.En)} Ua={Count(layouts.Pair.Ua)}");
        report.AppendLine($"Focused layout now: {layouts.GetFocusedLayout()?.ToString() ?? "unknown"}");
        report.AppendLine(ok ? "RESULT: PASS" : "RESULT: FAIL");
        File.WriteAllText(reportPath, report.ToString());
        return ok ? 0 : 1;
    }

    private static int Count(LayoutTable table)
    {
        var n = 0;
        for (ushort key = 0; key < 0x80; key++)
        foreach (var shift in (bool[])[false, true])
        foreach (var caps in (bool[])[false, true])
            n += table.TryGetChar(new KeyStroke(key, shift, caps), out _) ? 1 : 0;
        return n;
    }
}
