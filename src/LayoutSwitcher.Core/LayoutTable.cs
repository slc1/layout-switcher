namespace LayoutSwitcher.Core;

/// <summary>What each keystroke types in one keyboard layout, and the reverse.</summary>
public sealed class LayoutTable
{
    private readonly Dictionary<KeyStroke, char> _chars;
    private readonly Dictionary<char, KeyStroke> _strokes = new();

    public LayoutTable(IReadOnlyDictionary<KeyStroke, char> chars)
    {
        _chars = new Dictionary<KeyStroke, char>(chars);

        // Simplest stroke wins: CapsLock off first, then Shift off.
        foreach (var (stroke, ch) in _chars.OrderBy(p => p.Key.CapsLock).ThenBy(p => p.Key.Shift))
            _strokes.TryAdd(ch, stroke);
    }

    public bool TryGetChar(KeyStroke stroke, out char ch) => _chars.TryGetValue(stroke, out ch);

    public bool TryGetStroke(char ch, out KeyStroke stroke) => _strokes.TryGetValue(ch, out stroke);
}
