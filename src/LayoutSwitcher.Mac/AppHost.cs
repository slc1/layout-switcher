using AppKit;
using Foundation;
using LayoutSwitcher.Core;
using LayoutSwitcher.Core.Config;
using LayoutSwitcher.Mac.Engine;
using LayoutSwitcher.Mac.Layouts;
using LayoutSwitcher.Mac.Logging;
using LayoutSwitcher.Mac.Native;
using LayoutSwitcher.Mac.Startup;
using LayoutSwitcher.Mac.Ui;
using ObjCRuntime;

namespace LayoutSwitcher.Mac;

/// <summary>Owns the app's lifetime on the main thread.</summary>
[Register(nameof(AppHost))]
internal sealed class AppHost : NSApplicationDelegate, IEngineHost
{
    private const string AccessibilitySettingsUrl =
        "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility";

    private readonly SettingsStore _store = new(AppPaths.SettingsFile);
    private readonly List<NSObject> _observers = [];
    private StatusItem? _tray;
    private LayoutService? _layouts;
    private AppServices? _services;
    private SelectionConverter? _selection;
    private Settings _settings = new();
    private SettingsWindow? _settingsWindow;
    private NSTimer? _timer;

    public override void DidFinishLaunching(NSNotification notification)
    {
        _settings = _store.Load();
        AppLog.Configure(_settings.LogLevel);
        AppLog.Info("Started");
        ApplyAutostart();
        BuildMainMenu();

        _tray = new StatusItem();
        _tray.ToggleEnabledRequested += ToggleEnabled;
        _tray.SettingsRequested += ShowSettings;
        _tray.AccessibilityRequested += () => NSWorkspace.SharedWorkspace.OpenUrl(new NSUrl(AccessibilitySettingsUrl));
        _tray.ExitRequested += () => NSApplication.SharedApplication.Terminate(this);

        _layouts = LayoutService.TryCreate();
        if (_layouts is null)
        {
            AppLog.Error("UA or EN input source not enabled");
            _tray.Update(null, enabled: false);
            StatusItem.ShowMessage(
                "Add both the Ukrainian and an English input source in System Settings → Keyboard → Input Sources, then restart.",
                NSAlertStyle.Warning);
            return;
        }

        _selection = new SelectionConverter(OnMainThread, _layouts.Pair);
        _observers.Add(NSDistributedNotificationCenter.DefaultCenter.AddObserver(
            new NSString("com.apple.Carbon.TISNotifySelectedKeyboardInputSourceChanged"), _ => RefreshTray()));
        _observers.Add(NSWorkspace.SharedWorkspace.NotificationCenter.AddObserver(
            NSWorkspace.DidActivateApplicationNotification, _ => OnAppActivated()));

        TryStartServices(promptForAccess: true);
        _timer = NSTimer.CreateRepeatingScheduledTimer(0.3, _ => Tick());
    }

    public override void WillTerminate(NSNotification notification)
    {
        _timer?.Invalidate();
        _settingsWindow?.Dispose();
        _services?.Dispose();
        _tray?.Dispose();
        foreach (var observer in _observers)
            observer.Dispose();
    }

    /// <summary>
    /// The event tap needs the Accessibility permission. Until the user grants it the menu bar shows "!",
    /// and <see cref="Tick"/> retries; macOS applies the grant without a restart.
    /// </summary>
    private void TryStartServices(bool promptForAccess)
    {
        if (!IsTrusted(promptForAccess))
        {
            _tray!.ShowAccessibilityItem(true);
            return;
        }

        try
        {
            _services = AppServices.Start(_layouts!, this, _settings, out _);
        }
        catch (InvalidOperationException e)
        {
            AppLog.Error("Could not start the event tap", e);
            _tray!.ShowAccessibilityItem(true);
            return;
        }

        AppLog.Info("Event tap started");
        _tray!.ShowAccessibilityItem(false);
        OnAppActivated();
        ReportInvalidHotkeys();
    }

    private static bool IsTrusted(bool prompt)
    {
        if (!prompt)
            return Carbon.AXIsProcessTrustedWithOptions(0);
        using var options = NSDictionary.FromObjectAndKey(NSNumber.FromBoolean(true), new NSString("AXTrustedCheckOptionPrompt"));
        return Carbon.AXIsProcessTrustedWithOptions(options.Handle);
    }

    private void Tick()
    {
        if (_services is null)
            TryStartServices(promptForAccess: false);
        RefreshTray();
    }

    /// <summary>
    /// While secure input is on (a password field, or an app that asks for it) macOS keeps keys away from
    /// every event tap, so the app cannot see what is typed or react to its hotkeys.
    /// </summary>
    private void RefreshTray()
    {
        if (_layouts is null || _tray is null)
            return;
        _layouts.Refresh();

        var unavailable = _services is null ? "needs the Accessibility permission"
            : Carbon.IsSecureEventInputEnabled() ? "paused while a password field or secure input is active"
            : null;
        _tray.Update(_layouts.GetFocusedLayout(), _settings.Enabled, unavailable);
    }

    private void OnAppActivated()
    {
        if (_services is null)
            return;

        var app = NSWorkspace.SharedWorkspace.FrontmostApplication;
        var exclusions = new ExclusionList(_settings.ExcludedPrograms);
        _services.Tap.Excluded = app is not null
            && new[] { app.BundleIdentifier, app.BundleUrl?.Path, app.ExecutableUrl?.Path }
                .Any(id => id is not null && exclusions.IsExcluded(id));
        _services.Tap.PostReset();
    }

    public void ToggleEnabled()
    {
        if (!NSThread.IsMain)
        {
            NSApplication.SharedApplication.BeginInvokeOnMainThread(ToggleEnabled);
            return;
        }

        _settings.Enabled = !_settings.Enabled;
        _store.Save(_settings);
        if (_services is not null)
            _services.Tap.Enabled = _settings.Enabled;
        RefreshTray();
    }

    public LayoutId? ConvertSelection() => _selection?.Convert();

    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        // The window edits a fresh copy; the running settings change only on Save.
        _settingsWindow = new SettingsWindow(_store.Load());
        _settingsWindow.CapturingChanged += capturing =>
        {
            if (_services is not null)
                _services.Tap.Suspended = capturing;
        };
        _settingsWindow.Saved += saved =>
        {
            saved.Enabled = _settings.Enabled;
            _settings = saved;
            _store.Save(_settings);
            AppLog.Configure(_settings.LogLevel);
            ApplyAutostart();
            if (_services is not null)
            {
                OnAppActivated();
                ReportInvalidHotkeys();
            }
        };
        _settingsWindow.Closed += () =>
        {
            // Disposing inside the close notification would free the window under AppKit's feet.
            var closed = _settingsWindow;
            _settingsWindow = null;
            NSApplication.SharedApplication.BeginInvokeOnMainThread(() => closed?.Dispose());
        };
        _settingsWindow.Show();
    }

    private void ReportInvalidHotkeys()
    {
        _services!.Apply(_settings, out var invalid);
        if (invalid.Count == 0)
            return;
        AppLog.Error($"{invalid.Count} hotkey(s) could not be parsed");
        StatusItem.ShowMessage($"Unknown hotkey: {string.Join(", ", invalid)}. Fix it in Settings.", NSAlertStyle.Warning);
    }

    private void ApplyAutostart()
    {
        try
        {
            Autostart.Apply(_settings.Autostart);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            AppLog.Error("Could not update autostart", e);
        }
    }

    private static void OnMainThread(Action action) => NSApplication.SharedApplication.InvokeOnMainThread(action);

    /// <summary>
    /// Never shown (the app has no Dock icon), but text fields only get Cmd+C, Cmd+V and friends through
    /// menu key equivalents.
    /// </summary>
    private static void BuildMainMenu()
    {
        var main = new NSMenu();
        foreach (var (title, items) in new (string, (string Title, string Action, string Key)[])[]
                 {
                     ("LayoutSwitcher", [("Quit LayoutSwitcher", "terminate:", "q")]),
                     ("Edit",
                     [
                         ("Undo", "undo:", "z"), ("Redo", "redo:", "Z"), ("Cut", "cut:", "x"), ("Copy", "copy:", "c"),
                         ("Paste", "paste:", "v"), ("Select All", "selectAll:", "a"), ("Close", "performClose:", "w"),
                     ]),
                 })
        {
            var menu = new NSMenu(title);
            foreach (var item in items)
                menu.AddItem(new NSMenuItem(item.Title, new Selector(item.Action), item.Key));
            main.AddItem(new NSMenuItem(title) { Submenu = menu });
        }
        NSApplication.SharedApplication.MainMenu = main;
    }
}
