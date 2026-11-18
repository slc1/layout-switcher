using LayoutSwitcher.Core;

namespace LayoutSwitcher.App.Ui;

internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enabledItem;
    private LayoutId? _layout;
    private bool _enabled = true;
    private bool _unavailable;
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

    /// <param name="unavailable">The foreground window is elevated; Windows keeps our hook from seeing its keys.</param>
    public void Update(LayoutId? layout, bool enabled, bool unavailable = false)
    {
        if (layout == _layout && enabled == _enabled && unavailable == _unavailable)
            return;
        (_layout, _enabled, _unavailable) = (layout, enabled, unavailable);
        Redraw();
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info) =>
        _notifyIcon.ShowBalloonTip(5000, title, text, icon);

    private void Redraw()
    {
        var old = _icon;
        _icon = LayoutIcon.Create(_layout, _enabled, _unavailable, SystemInformation.SmallIconSize);
        _notifyIcon.Icon = _icon;
        old?.Dispose();

        _enabledItem.Checked = _enabled;
        _notifyIcon.Text = (_enabled, _unavailable) switch
        {
            (false, _) => "LayoutSwitcher (disabled)",
            (_, true) => "LayoutSwitcher: not available in administrator windows",
            _ => "LayoutSwitcher",
        };
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon?.Dispose();
    }
}
