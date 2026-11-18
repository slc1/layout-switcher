namespace LayoutSwitcher.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\LayoutSwitcher.SingleInstance", out var createdNew);
        if (!createdNew)
            return;

        ApplicationConfiguration.Initialize();
        Application.Run(new AppHost());
    }
}
