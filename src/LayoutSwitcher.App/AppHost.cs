using LayoutSwitcher.App.Engine;
using LayoutSwitcher.App.Input;
using LayoutSwitcher.App.Layouts;
using LayoutSwitcher.App.Ui;
using LayoutSwitcher.Core;
using LayoutSwitcher.Core.Config;

namespace LayoutSwitcher.App;

/// <summary>Owns the app's lifetime on the UI thread.</summary>
internal sealed class AppHost : ApplicationContext, IEngineHost
{
    private readonly SettingsStore _store = new(AppPaths.SettingsFile);
    private readonly TrayIcon _tray = new();
    private readonly Control _ui = new();
    private readonly System.Windows.Forms.Timer _layoutTimer = new() { Interval = 300 };
    private readonly Settings _settings;
    private readonly AppServices? _services;
    private readonly SelectionConverter? _selection;
    private bool _warnedElevated;

    public AppHost()
    {
        _ui.CreateControl();
        _settings = _store.Load();
        _tray.ToggleEnabledRequested += ToggleEnabled;
        _tray.ExitRequested += ExitThread;

        var layouts = LayoutService.TryCreate();
        if (layouts is null)
        {
            _tray.Update(null, enabled: false);
            _tray.ShowBalloon("LayoutSwitcher", "Install both the Ukrainian and an English keyboard layout, then restart.",
                ToolTipIcon.Warning);
            return;
        }

        _selection = new SelectionConverter(OnUiThread, layouts.Pair);
        _services = AppServices.Start(layouts, this, _settings);
        _layoutTimer.Tick += (_, _) => _tray.Update(_services.Layouts.GetFocusedLayout(), _settings.Enabled);
        _layoutTimer.Start();
    }

    public void ToggleEnabled()
    {
        if (_ui.InvokeRequired)
        {
            _ui.BeginInvoke(ToggleEnabled);
            return;
        }

        _settings.Enabled = !_settings.Enabled;
        _store.Save(_settings);
        if (_services is not null)
            _services.Hook.Enabled = _settings.Enabled;
        _tray.Update(_services?.Layouts.GetFocusedLayout(), _settings.Enabled);
    }

    public void WarnElevated()
    {
        if (_warnedElevated)
            return;
        _warnedElevated = true;
        _ui.BeginInvoke(() => _tray.ShowBalloon("LayoutSwitcher",
            "This window runs as administrator. Windows does not let LayoutSwitcher type into it.",
            ToolTipIcon.Info));
    }

    public LayoutId? ConvertSelection(HeldModifiers held) => _selection?.Convert(held);

    private object? OnUiThread(Func<object?> action) => _ui.InvokeRequired ? _ui.Invoke(action) : action();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _layoutTimer.Dispose();
            _services?.Dispose();
            _tray.Dispose();
            _ui.Dispose();
        }
        base.Dispose(disposing);
    }
}
