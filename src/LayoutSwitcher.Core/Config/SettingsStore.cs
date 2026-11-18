using System.Text.Encodings.Web;
using System.Text.Json;

namespace LayoutSwitcher.Core.Config;

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep '+' in hotkeys readable
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Never throws. Missing or unreadable file → defaults. Malformed file → renamed to <c>.bad</c>
    /// (replacing an older one), defaults. Null values → their defaults.
    /// </summary>
    public Settings Load()
    {
        if (!File.Exists(path))
            return new Settings();

        try
        {
            using var stream = File.OpenRead(path);
            return WithDefaultsForNulls(JsonSerializer.Deserialize<Settings>(stream, Options));
        }
        catch (JsonException)
        {
            try
            {
                File.Move(path, path + ".bad", overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Could not move it aside; the next save overwrites it.
            }
            return new Settings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new Settings();
        }
    }

    private static Settings WithDefaultsForNulls(Settings? loaded)
    {
        var defaults = new Settings();
        if (loaded is null)
            return defaults;

        loaded.Hotkeys ??= defaults.Hotkeys;
        loaded.Hotkeys.ConvertLastWord ??= defaults.Hotkeys.ConvertLastWord;
        loaded.Hotkeys.ConvertSelection ??= defaults.Hotkeys.ConvertSelection;
        loaded.Hotkeys.ConvertAllTyped ??= defaults.Hotkeys.ConvertAllTyped;
        loaded.Hotkeys.ToggleEnabled ??= defaults.Hotkeys.ToggleEnabled;
        loaded.ExtraWordChars ??= defaults.ExtraWordChars;
        loaded.LogLevel ??= defaults.LogLevel;
        loaded.ExcludedPrograms = loaded.ExcludedPrograms?.Where(p => p is not null).ToList() ?? defaults.ExcludedPrograms;
        return loaded;
    }

    /// <summary>Writes a temp file and moves it over the target, so a crash never leaves a half-written file.</summary>
    public void Save(Settings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
