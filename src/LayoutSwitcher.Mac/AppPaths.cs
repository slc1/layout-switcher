namespace LayoutSwitcher.Mac;

internal static class AppPaths
{
    private static readonly string Library = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library");

    public static string SettingsFile { get; } = Path.Combine(Library, "Application Support", "LayoutSwitcher", "settings.json");

    public static string LogDirectory { get; } = Path.Combine(Library, "Logs", "LayoutSwitcher");

    public static string LaunchAgentFile { get; } = Path.Combine(Library, "LaunchAgents", "com.slc.layoutswitcher.plist");
}
