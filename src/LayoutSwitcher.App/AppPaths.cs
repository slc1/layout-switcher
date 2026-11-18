namespace LayoutSwitcher.App;

internal static class AppPaths
{
    public static string SettingsFile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LayoutSwitcher", "settings.json");

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LayoutSwitcher", "logs");
}
