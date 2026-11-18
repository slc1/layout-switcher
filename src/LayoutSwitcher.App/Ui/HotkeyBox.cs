using LayoutSwitcher.Core.Input;

namespace LayoutSwitcher.App.Ui;

/// <summary>Press a combination to set it; Backspace or Delete clears it.</summary>
internal sealed class HotkeyBox : TextBox
{
    public HotkeyBox()
    {
        ReadOnly = true;
        BackColor = SystemColors.Window;
        ShortcutsEnabled = false;
    }

    /// <summary>Empty string means "no hotkey".</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Value
    {
        get => Text;
        set => Text = value;
    }

    public event Action<string>? Rejected;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Tab and Esc keep their dialog meaning (next field, cancel); they cannot be hotkeys anyway.
        if (keyData is Keys.Tab or (Keys.Tab | Keys.Shift) or Keys.Escape)
            return base.ProcessCmdKey(ref msg, keyData);

        // Arrows and Enter would otherwise move focus or press Save before OnKeyDown sees them.
        OnKeyDown(new KeyEventArgs(keyData));
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        var key = e.KeyCode;
        if (key is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin)
            return;

        if (key is Keys.Back or Keys.Delete && e.Modifiers == Keys.None)
        {
            Value = "";
            return;
        }

        var modifiers = Modifiers.None;
        if (e.Control)
            modifiers |= Modifiers.Ctrl;
        if (e.Alt)
            modifiers |= Modifiers.Alt;
        if (e.Shift)
            modifiers |= Modifiers.Shift;
        if (IsWinDown())
            modifiers |= Modifiers.Win;

        // Ctrl+Pause arrives as Cancel.
        var vk = key == Keys.Cancel ? VirtualKeys.Pause : (ushort)key;
        var text = new Hotkey(modifiers, vk).ToString();

        if (!Hotkey.TryParse(text, out var parsed) || parsed != new Hotkey(modifiers, vk))
        {
            Rejected?.Invoke($"{key} is not supported. Use Pause, F1–F24, a letter, a digit, Space, CapsLock, ScrollLock or Insert.");
            return;
        }

        if (parsed.BlocksTyping)
        {
            Rejected?.Invoke("A letter or digit needs Ctrl, Alt or Win, or it would block normal typing.");
            return;
        }

        Value = text;
    }

    // Control.ModifierKeys does not report Win.
    private static bool IsWinDown() =>
        (Windows.Win32.PInvoke.GetKeyState((int)Keys.LWin) & 0x8000) != 0
        || (Windows.Win32.PInvoke.GetKeyState((int)Keys.RWin) & 0x8000) != 0;
}
