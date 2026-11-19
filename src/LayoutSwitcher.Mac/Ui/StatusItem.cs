using AppKit;
using LayoutSwitcher.Core;

namespace LayoutSwitcher.Mac.Ui;

/// <summary>The menu bar icon and its menu: the Mac counterpart of the Windows tray icon.</summary>
internal sealed class StatusItem : IDisposable
{
    private readonly NSStatusItem _item;
    private readonly NSMenuItem _enabledItem;
    private readonly NSMenuItem _accessItem;
    private LayoutId? _layout;
    private bool _enabled = true;
    private string? _unavailable;
    private bool _drawn;

    public event Action? ToggleEnabledRequested;
    public event Action? SettingsRequested;
    public event Action? AccessibilityRequested;
    public event Action? ExitRequested;

    public StatusItem()
    {
        _enabledItem = new NSMenuItem("Enabled", (_, _) => ToggleEnabledRequested?.Invoke());
        _accessItem = new NSMenuItem("Allow in Accessibility settings…", (_, _) => AccessibilityRequested?.Invoke())
        {
            Hidden = true,
        };

        var menu = new NSMenu { AutoEnablesItems = false };
        menu.AddItem(_accessItem);
        menu.AddItem(_enabledItem);
        menu.AddItem(new NSMenuItem("Settings…", ",", (_, _) => SettingsRequested?.Invoke()));
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem(new NSMenuItem("Quit LayoutSwitcher", "q", (_, _) => ExitRequested?.Invoke()));

        _item = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Variable);
        _item.Menu = menu;
        Redraw();
    }

    /// <param name="unavailable">Why the app cannot work right now, or null when it can.</param>
    public void Update(LayoutId? layout, bool enabled, string? unavailable = null)
    {
        if (_drawn && layout == _layout && enabled == _enabled && unavailable == _unavailable)
            return;
        (_layout, _enabled, _unavailable) = (layout, enabled, unavailable);
        Redraw();
    }

    /// <summary>Shows the menu item that opens the Accessibility pane.</summary>
    public void ShowAccessibilityItem(bool show) => _accessItem.Hidden = !show;

    /// <summary>The Mac has no tray balloons; an alert is the closest that is always seen.</summary>
    public static void ShowMessage(string text, NSAlertStyle style = NSAlertStyle.Informational)
    {
        Activate();
        using var alert = new NSAlert { MessageText = "LayoutSwitcher", InformativeText = text, AlertStyle = style };
        alert.RunModal();
    }

    /// <summary>A menu bar app is never active on its own; windows and alerts would open behind others.</summary>
    public static void Activate()
    {
        if (OperatingSystem.IsMacOSVersionAtLeast(14))
            NSApplication.SharedApplication.Activate();
        else
            NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
    }

    private void Redraw()
    {
        _drawn = true;
        _item.Button.Image = LayoutIcon.Create(_layout, _enabled, _unavailable is not null);
        _enabledItem.State = _enabled ? NSCellStateValue.On : NSCellStateValue.Off;
        _item.Button.ToolTip = (_enabled, _unavailable) switch
        {
            (false, _) => "LayoutSwitcher (disabled)",
            (_, { } reason) => $"LayoutSwitcher: {reason}",
            _ => "LayoutSwitcher",
        };
    }

    public void Dispose() => NSStatusBar.SystemStatusBar.RemoveStatusItem(_item);
}
