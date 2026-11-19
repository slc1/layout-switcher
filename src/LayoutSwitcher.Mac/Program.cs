using AppKit;

namespace LayoutSwitcher.Mac;

internal static class Program
{
    /// <summary>
    /// Run normally to start the menu bar app (one instance per user).
    /// <c>LayoutSwitcher --self-test &lt;report-file&gt;</c> checks layout tables without UI and exits 0 on success.
    /// </summary>
    private static int Main(string[] args)
    {
        NSApplication.Init();
        if (args is ["--self-test", var reportPath])
            return SelfTest.Run(reportPath);

        using var mutex = new Mutex(initiallyOwned: true, "LayoutSwitcher.SingleInstance", out var createdNew);
        if (!createdNew)
            return 0;

        var app = NSApplication.SharedApplication;
        app.ActivationPolicy = NSApplicationActivationPolicy.Accessory;
        app.Delegate = new AppHost();
        app.Run();
        return 0;
    }
}
