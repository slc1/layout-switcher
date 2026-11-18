namespace LayoutSwitcher.Core;

/// <summary>Keystrokes typed since the last reset. Not thread-safe: owned by the engine's worker thread.</summary>
public sealed class TypedBuffer(TimeProvider time, int capacity = TypedBuffer.DefaultCapacity, TimeSpan? idleTimeout = null)
{
    public const int DefaultCapacity = 300;
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(10);

    private readonly List<TypedKey> _keys = new(capacity);
    private readonly TimeSpan _idleTimeout = idleTimeout ?? DefaultIdleTimeout;
    private DateTimeOffset _lastAdd = time.GetUtcNow();

    public void Add(TypedKey key)
    {
        ExpireIfIdle();
        if (_keys.Count >= capacity)
            _keys.RemoveAt(0);
        _keys.Add(key);
        _lastAdd = time.GetUtcNow();
    }

    public void Backspace()
    {
        if (_keys.Count > 0)
            _keys.RemoveAt(_keys.Count - 1);
    }

    public void Clear() => _keys.Clear();

    public IReadOnlyList<TypedKey> Snapshot()
    {
        ExpireIfIdle();
        return _keys.ToArray();
    }

    /// <summary>Records that keys from <paramref name="start"/> to the end are now shown in <paramref name="layout"/>.</summary>
    public void MarkDisplayed(int start, LayoutId layout)
    {
        for (var i = start; i < _keys.Count; i++)
            _keys[i] = _keys[i] with { Layout = layout };
    }

    private void ExpireIfIdle()
    {
        if (time.GetUtcNow() - _lastAdd >= _idleTimeout)
            _keys.Clear();
    }
}
