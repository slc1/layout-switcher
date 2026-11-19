using AppKit;
using CoreGraphics;
using Foundation;
using LayoutSwitcher.Core.Config;
using UniformTypeIdentifiers;

namespace LayoutSwitcher.Mac.Ui;

/// <summary>Edits a copy of the settings; <see cref="Saved"/> fires with the result.</summary>
internal sealed class SettingsWindow : IDisposable
{
    private const float FieldWidth = 280;

    private readonly NSWindow _window;
    private readonly HotkeyField _lastWord = new();
    private readonly HotkeyField _selection = new();
    private readonly HotkeyField _allTyped = new();
    private readonly HotkeyField _toggle = new();
    private readonly NSTextField _extraChars = new();
    private readonly NSTextView _excluded = new() { RichText = false, Font = NSFont.UserFixedPitchFontOfSize(12)! };
    private readonly NSButton _autostart = NSButton.CreateCheckbox("Start at login", () => { });
    private readonly NSTextField _status = NSTextField.CreateWrappingLabel("");
    private readonly Settings _settings;
    private readonly List<NSObject> _observers = [];
    private bool _capturing;

    public event Action<Settings>? Saved;

    /// <summary>True while a hotkey field has focus in the key window, so the app's own hotkeys reach it.</summary>
    public event Action<bool>? CapturingChanged;

    public event Action? Closed;

    public SettingsWindow(Settings settings)
    {
        _settings = settings;
        _window = new NSWindow(new CGRect(0, 0, 480, 400), NSWindowStyle.Titled | NSWindowStyle.Closable,
            NSBackingStore.Buffered, deferCreation: false)
        {
            Title = "LayoutSwitcher Settings",
        };
        _window.ReleaseWhenClosed(false);

        var scroll = new NSScrollView
        {
            DocumentView = _excluded,
            HasVerticalScroller = true,
            BorderType = NSBorderType.BezelBorder,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };
        scroll.HeightAnchor.ConstraintEqualTo(90).Active = true;
        _excluded.MinSize = new CGSize(0, 90);
        _excluded.MaxSize = new CGSize(float.MaxValue, float.MaxValue);
        _excluded.VerticallyResizable = true;
        _excluded.AutoresizingMask = NSViewResizingMask.WidthSizable;
        _excluded.TextContainer!.WidthTracksTextView = true;

        var browse = NSButton.CreateButton("Add App…", BrowseExclusion);
        var hint = NSTextField.CreateWrappingLabel("One per line: an app (Safari.app), a bundle id or a full path.");
        hint.TextColor = NSColor.SecondaryLabel;
        hint.Font = NSFont.SystemFontOfSize(NSFont.SmallSystemFontSize)!;
        _status.TextColor = NSColor.SystemRed;

        NSView Empty() => NSGridCell.EmptyContentView!;
        NSTextField Label(string text) => NSTextField.CreateLabel(text);
        var grid = NSGridView.Create(new NSView[][]
        {
            [Label("Convert last word"), _lastWord],
            [Label("Convert selection"), _selection],
            [Label("Convert all typed"), _allTyped],
            [Label("Enable / disable"), _toggle],
            [Label("Extra word characters"), _extraChars],
            [Label("Excluded apps"), scroll],
            [Empty(), hint],
            [Empty(), browse],
            [Empty(), _autostart],
            [Empty(), _status],
        });
        grid.RowSpacing = 8;
        grid.ColumnSpacing = 12;
        grid.GetColumn(1).Width = FieldWidth;

        // The bindings do not expose these two properties; key-value coding reaches them.
        grid.GetColumn(0).SetValueForKey(NSNumber.FromNInt((nint)NSGridCellPlacement.Trailing), new NSString("xPlacement"));
        grid.GetRow(5).SetValueForKey(NSNumber.FromNInt((nint)NSGridCellPlacement.Top), new NSString("yPlacement"));

        var cancel = NSButton.CreateButton("Cancel", () => _window.Close());
        cancel.KeyEquivalent = "\u001b";
        var save = NSButton.CreateButton("Save", Save);
        save.KeyEquivalent = "\r";
        var buttons = NSStackView.FromViews([cancel, save]);
        buttons.Spacing = 12;

        var content = NSStackView.FromViews([grid, buttons]);
        content.Orientation = NSUserInterfaceLayoutOrientation.Vertical;
        content.Alignment = NSLayoutAttribute.Trailing;
        content.Spacing = 16;
        content.EdgeInsets = new NSEdgeInsets(20, 20, 20, 20);
        _window.ContentView = content;

        foreach (var field in new[] { _lastWord, _selection, _allTyped, _toggle })
        {
            field.Rejected += message => _status.StringValue = message;
            field.FocusChanged += UpdateCapturing;
        }
        _observers.Add(NSWindow.Notifications.ObserveDidBecomeKey(_window, (_, _) => UpdateCapturing()));
        _observers.Add(NSWindow.Notifications.ObserveDidResignKey(_window, (_, _) => UpdateCapturing()));
        _observers.Add(NSWindow.Notifications.ObserveWillClose(_window, (_, _) =>
        {
            _window.MakeFirstResponder(null);
            SetCapturing(false);
            Closed?.Invoke();
        }));

        _lastWord.Value = settings.Hotkeys.ConvertLastWord;
        _selection.Value = settings.Hotkeys.ConvertSelection;
        _allTyped.Value = settings.Hotkeys.ConvertAllTyped;
        _toggle.Value = settings.Hotkeys.ToggleEnabled;
        _extraChars.StringValue = settings.ExtraWordChars;
        _excluded.Value = string.Join('\n', settings.ExcludedPrograms);
        _autostart.State = settings.Autostart ? NSCellStateValue.On : NSCellStateValue.Off;
    }

    public void Show()
    {
        _window.Center();
        _window.MakeKeyAndOrderFront(null);
        _window.MakeFirstResponder(null); // opening the window must not start capturing
        StatusItem.Activate();
    }

    /// <summary>Brings an already open window to the front.</summary>
    public void Activate()
    {
        _window.MakeKeyAndOrderFront(null);
        StatusItem.Activate();
    }

    /// <summary>
    /// Our hotkeys are off only while this window is key and a hotkey field has focus; a field keeps focus
    /// when the user switches to another app, so focus alone would leave the app disabled.
    /// </summary>
    private void UpdateCapturing() => SetCapturing(_window.IsKeyWindow && _window.FirstResponder is HotkeyField);

    private void SetCapturing(bool capturing)
    {
        if (capturing == _capturing)
            return;
        _capturing = capturing;
        CapturingChanged?.Invoke(capturing);
    }

    private void BrowseExclusion()
    {
        using var panel = NSOpenPanel.OpenPanel;
        panel.Title = "Exclude an App";
        panel.AllowedContentTypes = [UTTypes.Application];
        panel.DirectoryUrl = NSUrl.FromFilename("/Applications");
        if (panel.RunModal() != (nint)NSModalResponse.OK || panel.Url?.Path is not { } path)
            return;

        var entry = NSBundle.FromPath(path)?.BundleIdentifier ?? Path.GetFileName(path);
        var entries = Entries();
        if (entries.Contains(entry, StringComparer.OrdinalIgnoreCase))
            return;
        _excluded.Value = string.Join('\n', entries.Append(entry));
    }

    private List<string> Entries() => _excluded.Value
        .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(e => e.Trim('"'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private void Save()
    {
        var hotkeys = new[] { _lastWord.Value, _selection.Value, _allTyped.Value, _toggle.Value }
            .Where(h => h.Length > 0).ToList();
        if (hotkeys.Count != hotkeys.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            _status.StringValue = "Two actions use the same hotkey.";
            return;
        }

        _settings.Hotkeys.ConvertLastWord = _lastWord.Value;
        _settings.Hotkeys.ConvertSelection = _selection.Value;
        _settings.Hotkeys.ConvertAllTyped = _allTyped.Value;
        _settings.Hotkeys.ToggleEnabled = _toggle.Value;
        _settings.ExtraWordChars = _extraChars.StringValue;
        _settings.ExcludedPrograms = Entries();
        _settings.Autostart = _autostart.State == NSCellStateValue.On;

        Saved?.Invoke(_settings);
        _window.Close();
    }

    public void Dispose()
    {
        foreach (var observer in _observers)
            observer.Dispose();
        _window.Dispose();
    }
}
