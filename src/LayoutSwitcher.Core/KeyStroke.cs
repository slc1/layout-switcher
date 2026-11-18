namespace LayoutSwitcher.Core;

/// <summary>A physical key press, independent of the active layout.</summary>
public readonly record struct KeyStroke(ushort ScanCode, bool Shift, bool CapsLock);
