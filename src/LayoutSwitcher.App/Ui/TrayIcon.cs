using LayoutSwitcher.Core;

namespace LayoutSwitcher.App.Ui;

internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enabledItem;
    private LayoutId? _layout;
    private bool _enabled = true;
    private Icon? _icon;

    public event Action? ToggleEnabledRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        _enabledItem = new ToolStripMenuItem("Enabled", null, (_, _) => ToggleEnabledRequested?.Invoke());
        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add("Settings…", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new NotifyIcon { ContextMenuStrip = menu, Text = "LayoutSwitcher" };
        _notifyIcon.DoubleClick += (_, _) => SettingsRequested?.Invoke();
        Redraw();
        _notifyIcon.Visible = true;
    }

    public void Update(LayoutId? layout, bool enabled)
    {
        if (layout == _layout && enabled == _enabled)
            return;
        (_layout, _enabled) = (layout, enabled);
        Redraw();
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info) =>
        _notifyIcon.ShowBalloonTip(5000, title, text, icon);

    private void Redraw()
    {
        var old = _icon;
        _icon = LayoutIcon.Create(_layout, _enabled, SystemInformation.SmallIconSize);
        _notifyIcon.Icon = _icon;
        old?.Dispose();

        _enabledItem.Checked = _enabled;
        _notifyIcon.Text = _enabled ? "LayoutSwitcher" : "LayoutSwitcher (disabled)";
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon?.Dispose();
    }
}
