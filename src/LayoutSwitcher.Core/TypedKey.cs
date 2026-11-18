namespace LayoutSwitcher.Core;

/// <param name="Layout">The layout the character is currently displayed in on screen.</param>
public readonly record struct TypedKey(KeyStroke Stroke, LayoutId Layout);
