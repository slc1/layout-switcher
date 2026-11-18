namespace LayoutSwitcher.App;

internal static class Program
{
    /// <summary>
    /// Run normally to start the tray app (one instance per user session).
    /// <c>LayoutSwitcher.exe --self-test &lt;report-file&gt;</c> checks layout tables without UI and exits 0 on success.
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--self-test", var reportPath])
            return SelfTest.Run(reportPath);
        if (args is ["--e2e", var e2eReportPath])
            return E2eCheck.Run(e2eReportPath);

        using var mutex = new Mutex(initiallyOwned: true, @"Local\LayoutSwitcher.SingleInstance", out var createdNew);
        if (!createdNew)
            return 0;

        ApplicationConfiguration.Initialize();
        Application.Run(new AppHost());
        return 0;
    }
}
