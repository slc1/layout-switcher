using System.Security;
using Foundation;

namespace LayoutSwitcher.Mac.Startup;

/// <summary>A per-user LaunchAgent that opens the app at login. Re-applied at every start so a moved app keeps starting.</summary>
internal static class Autostart
{
    public static void Apply(bool enabled)
    {
        if (!enabled || Command() is not { } command)
        {
            File.Delete(AppPaths.LaunchAgentFile);
            return;
        }

        var arguments = string.Concat(command.Select(a => $"\n        <string>{SecurityElement.Escape(a)}</string>"));
        var plist = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>com.slc.layoutswitcher</string>
                <key>ProgramArguments</key>
                <array>{arguments}
                </array>
                <key>RunAtLoad</key>
                <true/>
                <key>LimitLoadToSessionType</key>
                <string>Aqua</string>
            </dict>
            </plist>

            """;

        if (File.Exists(AppPaths.LaunchAgentFile) && File.ReadAllText(AppPaths.LaunchAgentFile) == plist)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.LaunchAgentFile)!);
        File.WriteAllText(AppPaths.LaunchAgentFile, plist);
    }

    /// <summary>
    /// <c>open</c> on the bundle, so macOS starts it like a double-click and its Accessibility permission applies.
    /// Null when not running from an app bundle (e.g. <c>dotnet run</c> of a bare build).
    /// </summary>
    private static string[]? Command()
    {
        var bundle = NSBundle.MainBundle.BundlePath;
        return bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase) ? ["/usr/bin/open", "-a", bundle] : null;
    }
}
