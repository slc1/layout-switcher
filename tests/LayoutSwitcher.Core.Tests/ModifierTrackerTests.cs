using LayoutSwitcher.Core.Input;

namespace LayoutSwitcher.Core.Tests;

public class ModifierTrackerTests
{
    [Fact]
    public void Tracks_down_and_up()
    {
        var tracker = new ModifierTracker();
        tracker.Track(VirtualKeys.LControl, down: true);
        tracker.Track(VirtualKeys.RShift, down: true);

        Assert.Equal(HeldModifiers.LCtrl | HeldModifiers.RShift, tracker.Held);
        Assert.Equal(Modifiers.Ctrl | Modifiers.Shift, tracker.Modifiers);

        tracker.Track(VirtualKeys.LControl, down: false);
        Assert.Equal(Modifiers.Shift, tracker.Modifiers);
    }

    [Fact]
    public void Non_modifier_keys_are_ignored()
    {
        var tracker = new ModifierTracker();
        tracker.Track('Q', down: true);

        Assert.Equal(HeldModifiers.None, tracker.Held);
    }

    [Fact]
    public void Reconcile_drops_a_modifier_whose_release_was_missed()
    {
        // Win+L: the hook saw LWin go down; the release happened on the secure desktop.
        var tracker = new ModifierTracker();
        tracker.Track(VirtualKeys.LWin, down: true);
        tracker.Track(VirtualKeys.LShift, down: true);

        tracker.Reconcile(vk => vk == VirtualKeys.LShift);

        Assert.Equal(HeldModifiers.LShift, tracker.Held);
    }

    [Fact]
    public void Reconcile_never_adds_modifiers()
    {
        var tracker = new ModifierTracker();

        tracker.Reconcile(_ => true);

        Assert.Equal(HeldModifiers.None, tracker.Held);
    }

    [Theory]
    [InlineData(HeldModifiers.LWin, true)]
    [InlineData(HeldModifiers.RAlt, true)]
    [InlineData(HeldModifiers.LCtrl | HeldModifiers.LAlt, true)]
    [InlineData(HeldModifiers.LCtrl, false)]
    [InlineData(HeldModifiers.LShift, false)]
    [InlineData(HeldModifiers.None, false)]
    public void Menu_suppression_needed_for_alt_and_win(HeldModifiers held, bool expected)
    {
        Assert.Equal(expected, ModifierTracker.NeedsMenuSuppression(held));
    }
}
