using LayoutSwitcher.Core.Config;

namespace LayoutSwitcher.App.Ui;

/// <summary>Edits a copy of the settings; <see cref="Saved"/> fires with the result.</summary>
internal sealed class SettingsForm : Form
{
    private readonly HotkeyBox _lastWord = new();
    private readonly HotkeyBox _selection = new();
    private readonly HotkeyBox _allTyped = new();
    private readonly HotkeyBox _toggle = new();
    private readonly TextBox _extraChars = new();
    private readonly ListBox _excluded = new() { Height = 110, IntegralHeight = false };
    private readonly TextBox _newExclusion = new() { PlaceholderText = "game.exe or C:\\path\\app.exe" };
    private readonly CheckBox _autostart = new() { Text = "Start with Windows", AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(440, 0) };
    private readonly Settings _settings;

    public event Action<Settings>? Saved;

    /// <summary>True while a hotkey box has focus, so the app's own hotkeys reach it.</summary>
    public event Action<bool>? CapturingChanged;

    public SettingsForm(Settings settings)
    {
        _settings = settings;
        Text = "LayoutSwitcher settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        Font = SystemFonts.MessageBoxFont ?? Font;

        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));

        void Row(string label, Control control)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) });
            control.Dock = DockStyle.Fill;
            layout.Controls.Add(control);
        }

        Row("Convert last word", _lastWord);
        Row("Convert selection", _selection);
        Row("Convert all typed", _allTyped);
        Row("Enable / disable", _toggle);
        Row("Extra word characters", _extraChars);

        var exclusionPanel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        exclusionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        exclusionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var add = new Button { Text = "Add", AutoSize = true };
        var browse = new Button { Text = "Browse…", AutoSize = true };
        var remove = new Button { Text = "Remove", AutoSize = true };
        _excluded.Dock = _newExclusion.Dock = DockStyle.Fill;
        exclusionPanel.Controls.Add(_excluded);
        var listButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        listButtons.Controls.AddRange([browse, remove]);
        exclusionPanel.Controls.Add(listButtons);
        exclusionPanel.Controls.Add(_newExclusion);
        exclusionPanel.Controls.Add(add);
        Row("Excluded programs", exclusionPanel);

        layout.Controls.Add(new Label());
        layout.Controls.Add(_autostart);
        layout.Controls.Add(new Label());
        layout.Controls.Add(_status);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var save = new Button { Text = "Save", AutoSize = true };
        buttons.Controls.AddRange([cancel, save]);
        layout.Controls.Add(buttons);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;

        foreach (var box in new[] { _lastWord, _selection, _allTyped, _toggle })
        {
            box.Rejected += message => _status.Text = message;
            box.Enter += (_, _) => CapturingChanged?.Invoke(true);
            box.Leave += (_, _) => CapturingChanged?.Invoke(false);
        }
        FormClosed += (_, _) => CapturingChanged?.Invoke(false);

        add.Click += (_, _) => AddExclusion(_newExclusion.Text);
        browse.Click += (_, _) => BrowseExclusion();
        remove.Click += (_, _) =>
        {
            if (_excluded.SelectedIndex >= 0)
                _excluded.Items.RemoveAt(_excluded.SelectedIndex);
        };
        save.Click += (_, _) => Save();

        _lastWord.Value = settings.Hotkeys.ConvertLastWord;
        _selection.Value = settings.Hotkeys.ConvertSelection;
        _allTyped.Value = settings.Hotkeys.ConvertAllTyped;
        _toggle.Value = settings.Hotkeys.ToggleEnabled;
        _extraChars.Text = settings.ExtraWordChars;
        _excluded.Items.AddRange([.. settings.ExcludedPrograms]);
        _autostart.Checked = settings.Autostart;
    }

    private void AddExclusion(string entry)
    {
        entry = entry.Trim().Trim('"');
        if (entry.Length == 0 || _excluded.Items.Contains(entry))
            return;
        _excluded.Items.Add(entry);
        _newExclusion.Clear();
    }

    private void BrowseExclusion()
    {
        using var dialog = new OpenFileDialog { Filter = "Programs (*.exe)|*.exe", Title = "Exclude a program" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddExclusion(Path.GetFileName(dialog.FileName));
    }

    private void Save()
    {
        var hotkeys = new[] { _lastWord.Value, _selection.Value, _allTyped.Value, _toggle.Value }
            .Where(h => h.Length > 0).ToList();
        if (hotkeys.Count != hotkeys.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            _status.Text = "Two actions use the same hotkey.";
            return;
        }

        _settings.Hotkeys.ConvertLastWord = _lastWord.Value;
        _settings.Hotkeys.ConvertSelection = _selection.Value;
        _settings.Hotkeys.ConvertAllTyped = _allTyped.Value;
        _settings.Hotkeys.ToggleEnabled = _toggle.Value;
        _settings.ExtraWordChars = _extraChars.Text;
        _settings.ExcludedPrograms = _excluded.Items.Cast<string>().ToList();
        _settings.Autostart = _autostart.Checked;

        Saved?.Invoke(_settings);
        Close();
    }
}
