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

    /// <summary>Exe file names, or full paths, where the app does nothing.</summary>
    public List<string> ExcludedPrograms { get; set; } = ["mstsc.exe"];

    public string LogLevel { get; set; } = "None";

    /// <summary>Keys this version does not know; kept so a save does not drop them.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class HotkeySettings
{
    public string ConvertLastWord { get; set; } = "Pause";
    public string ConvertSelection { get; set; } = "Shift+Pause";
    public string ConvertAllTyped { get; set; } = "Ctrl+Pause";
    public string ToggleEnabled { get; set; } = "Win+F8";
}
