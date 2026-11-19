using System.Text.Json;
using System.Text.Json.Serialization;

namespace LayoutSwitcher.Core.Config;

public sealed class Settings
{
    public bool Enabled { get; set; } = true;
    public bool Autostart { get; set; } = true;
    public HotkeySettings Hotkeys { get; set; } = new();

    /// <summary>Chars that join two letters into one word, besides apostrophes.</summary>
    public string ExtraWordChars { get; set; } = "-_";

    /// <summary>
    /// Where the app does nothing. Windows: exe file names or full paths. macOS: bundle ids, app or executable
    /// names (<c>Safari.app</c>, <c>Safari</c>) or full paths.
    /// </summary>
    public List<string> ExcludedPrograms { get; set; } = [.. PlatformDefaults.Current.ExcludedPrograms];

    public string LogLevel { get; set; } = "None";

    /// <summary>Keys this version does not know; kept so a save does not drop them.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class HotkeySettings
{
    public string ConvertLastWord { get; set; } = PlatformDefaults.Current.ConvertLastWord;
    public string ConvertSelection { get; set; } = PlatformDefaults.Current.ConvertSelection;
    public string ConvertAllTyped { get; set; } = PlatformDefaults.Current.ConvertAllTyped;
    public string ToggleEnabled { get; set; } = PlatformDefaults.Current.ToggleEnabled;
}

/// <summary>
/// Defaults that differ by OS. Macs have no Pause key, and Ctrl+Space / Ctrl+Option+Space already switch
/// input sources there, so the Mac hotkeys use Ctrl+Option with a letter.
/// </summary>
public sealed record PlatformDefaults(
    string ConvertLastWord,
    string ConvertSelection,
    string ConvertAllTyped,
    string ToggleEnabled,
    IReadOnlyList<string> ExcludedPrograms)
{
    public static readonly PlatformDefaults Windows = new("Pause", "Shift+Pause", "Ctrl+Pause", "Win+F8", ["mstsc.exe"]);

    public static readonly PlatformDefaults Mac = new(
        "Ctrl+Option+W", "Ctrl+Option+S", "Ctrl+Option+A", "Ctrl+Option+T", ["com.microsoft.rdc.macos"]);

    public static PlatformDefaults Current => OperatingSystem.IsMacOS() ? Mac : Windows;
}
