using System.Text;
using LayoutSwitcher.App.Layouts;
using LayoutSwitcher.Core;

namespace LayoutSwitcher.App;

/// <summary>
/// Diagnostics without UI or hooks. Writes only fixed sample strings, never user input.
/// Run: LayoutSwitcher.exe --self-test &lt;report-file&gt;
/// </summary>
internal static class SelfTest
{
    public static int Run(string reportPath)
    {
        var report = new StringBuilder();
        var layouts = LayoutService.TryCreate();
        if (layouts is null)
        {
            report.AppendLine("FAIL: UA or EN layout is not installed");
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

        // Depends on the installed Ukrainian variant, so informational only.
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
        for (ushort scan = 0; scan < 0x80; scan++)
        foreach (var shift in (bool[])[false, true])
        foreach (var caps in (bool[])[false, true])
            n += table.TryGetChar(new KeyStroke(scan, shift, caps), out _) ? 1 : 0;
        return n;
    }
}
