using LayoutSwitcher.Core;

namespace LayoutSwitcher.Core.Tests;

public class TypedBufferTests
{
    private readonly ManualTimeProvider _time = new();

    private static TypedKey Key(ushort scan) => new(new KeyStroke(scan, false, false), LayoutId.En);

    private TypedBuffer With(params ushort[] scans)
    {
        var buffer = new TypedBuffer(_time);
        foreach (var scan in scans)
            buffer.Add(Key(scan));
        return buffer;
    }

    [Fact]
    public void Keeps_keys_in_order()
    {
        Assert.Equal([Key(1), Key(2), Key(3)], With(1, 2, 3).Snapshot());
    }

    [Fact]
    public void Backspace_removes_last()
    {
        var buffer = With(1, 2);
        buffer.Backspace();

        Assert.Equal([Key(1)], buffer.Snapshot());
    }

    [Fact]
    public void Backspace_on_empty_is_noop()
    {
        var buffer = With();
        buffer.Backspace();

        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public void Clear_empties()
    {
        var buffer = With(1, 2);
        buffer.Clear();

        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public void Drops_oldest_over_capacity()
    {
        var buffer = With();
        for (var i = 0; i < 301; i++)
            buffer.Add(Key((ushort)i));

        var snapshot = buffer.Snapshot();
        Assert.Equal(300, snapshot.Count);
        Assert.Equal(Key(1), snapshot[0]);
        Assert.Equal(Key(300), snapshot[^1]);
    }

    [Fact]
    public void Expires_after_ten_idle_minutes()
    {
        var buffer = With(1);
        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public void Survives_just_under_ten_idle_minutes()
    {
        var buffer = With(1);
        _time.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));

        Assert.Equal([Key(1)], buffer.Snapshot());
    }

    [Fact]
    public void Add_after_idle_starts_fresh()
    {
        var buffer = With(1);
        _time.Advance(TimeSpan.FromMinutes(11));
        buffer.Add(Key(2));

        Assert.Equal([Key(2)], buffer.Snapshot());
    }

    [Fact]
    public void MarkDisplayed_changes_layout_from_start()
    {
        var buffer = With(1, 2, 3);
        buffer.MarkDisplayed(1, LayoutId.Ua);

        Assert.Equal([LayoutId.En, LayoutId.Ua, LayoutId.Ua], buffer.Snapshot().Select(k => k.Layout));
    }

    [Fact]
    public void Snapshot_is_a_copy()
    {
        var buffer = With(1);
        var snapshot = buffer.Snapshot();
        buffer.Add(Key(2));

        Assert.Single(snapshot);
    }

    [Fact]
    public void Backspace_corrections_are_reflected()
    {
        var buffer = new TypedBuffer(_time);
        foreach (var key in TestLayouts.Typed("ghbdq", LayoutId.En))
            buffer.Add(key);
        buffer.Backspace();
        foreach (var key in TestLayouts.Typed("sn", LayoutId.En))
            buffer.Add(key);

        Assert.Equal(TestLayouts.Typed("ghbdsn", LayoutId.En), buffer.Snapshot());
    }
}
