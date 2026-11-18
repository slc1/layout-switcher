using LayoutSwitcher.Core.Config;

namespace LayoutSwitcher.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ls-tests-" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_dir, "sub", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Missing_file_gives_defaults_without_writing()
    {
        var settings = new SettingsStore(SettingsPath).Load();

        Assert.True(settings.Enabled);
        Assert.True(settings.Autostart);
        Assert.Equal("Pause", settings.Hotkeys.ConvertLastWord);
        Assert.Equal("Shift+Pause", settings.Hotkeys.ConvertSelection);
        Assert.Equal("Ctrl+Pause", settings.Hotkeys.ConvertAllTyped);
        Assert.Equal("Win+F8", settings.Hotkeys.ToggleEnabled);
        Assert.Equal("-_", settings.ExtraWordChars);
        Assert.Equal(["mstsc.exe"], settings.ExcludedPrograms);
        Assert.Equal("None", settings.LogLevel);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void Round_trips()
    {
        var store = new SettingsStore(SettingsPath);
        var settings = store.Load();
        settings.Enabled = false;
        settings.Hotkeys.ConvertLastWord = "ScrollLock";
        settings.ExcludedPrograms.Add("game.exe");

        store.Save(settings);
        var loaded = store.Load();

        Assert.False(loaded.Enabled);
        Assert.Equal("ScrollLock", loaded.Hotkeys.ConvertLastWord);
        Assert.Equal(["mstsc.exe", "game.exe"], loaded.ExcludedPrograms);
    }

    [Fact]
    public void Writes_camel_case()
    {
        var store = new SettingsStore(SettingsPath);
        store.Save(store.Load());

        var json = File.ReadAllText(SettingsPath);
        Assert.Contains("\"convertLastWord\"", json);
        Assert.Contains("\"excludedPrograms\"", json);
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void Unknown_keys_survive_a_save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "enabled": false, "future": 1 }""");
        var store = new SettingsStore(SettingsPath);

        store.Save(store.Load());

        Assert.Contains("\"future\": 1", File.ReadAllText(SettingsPath));
        Assert.False(store.Load().Enabled);
    }

    [Fact]
    public void Partial_file_keeps_defaults_for_missing_keys()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "hotkeys": { "toggleEnabled": "Win+F9" } }""");

        var settings = new SettingsStore(SettingsPath).Load();

        Assert.Equal("Win+F9", settings.Hotkeys.ToggleEnabled);
        Assert.Equal("Pause", settings.Hotkeys.ConvertLastWord);
    }

    [Fact]
    public void Malformed_file_is_moved_aside_and_defaults_used()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath + ".bad", "older");
        File.WriteAllText(SettingsPath, "{ not json");

        var settings = new SettingsStore(SettingsPath).Load();

        Assert.True(settings.Enabled);
        Assert.False(File.Exists(SettingsPath));
        Assert.Equal("{ not json", File.ReadAllText(SettingsPath + ".bad"));
    }

    [Fact]
    public void Locked_file_gives_defaults_and_is_left_alone()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "enabled": false }""");
        using var lockHandle = new FileStream(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var settings = new SettingsStore(SettingsPath).Load();

        Assert.True(settings.Enabled);
        Assert.False(File.Exists(SettingsPath + ".bad"));
    }

    [Fact]
    public void Null_values_fall_back_to_defaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath,
            """{ "hotkeys": null, "extraWordChars": null, "excludedPrograms": [null, "a.exe"], "logLevel": null }""");

        var settings = new SettingsStore(SettingsPath).Load();

        Assert.Equal("Pause", settings.Hotkeys.ConvertLastWord);
        Assert.Equal("-_", settings.ExtraWordChars);
        Assert.Equal(["a.exe"], settings.ExcludedPrograms);
        Assert.Equal("None", settings.LogLevel);
    }

    [Fact]
    public void Null_hotkey_falls_back_to_default()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "hotkeys": { "convertSelection": null } }""");

        Assert.Equal("Shift+Pause", new SettingsStore(SettingsPath).Load().Hotkeys.ConvertSelection);
    }

    [Fact]
    public void Plus_is_written_unescaped()
    {
        var store = new SettingsStore(SettingsPath);
        store.Save(store.Load());

        Assert.Contains("\"Shift+Pause\"", File.ReadAllText(SettingsPath));
    }
}
