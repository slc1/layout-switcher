using LayoutSwitcher.App.Ui;
using LayoutSwitcher.Core.Config;

namespace LayoutSwitcher.App;

/// <summary>Owns the app's lifetime on the UI thread.</summary>
internal sealed class AppHost : ApplicationContext
{
    private readonly SettingsStore _store = new(AppPaths.SettingsFile);
    private readonly TrayIcon _tray = new();
    private Settings _settings;

    public AppHost()
    {
        _settings = _store.Load();
        _tray.ToggleEnabledRequested += ToggleEnabled;
        _tray.ExitRequested += ExitThread;
        _tray.Update(null, _settings.Enabled);
    }

    private void ToggleEnabled()
    {
        _settings.Enabled = !_settings.Enabled;
        _store.Save(_settings);
        _tray.Update(null, _settings.Enabled);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _tray.Dispose();
        base.Dispose(disposing);
    }
}
