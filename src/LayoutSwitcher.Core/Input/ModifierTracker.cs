namespace LayoutSwitcher.Core.Input;

/// <summary>Physical modifier keys currently down.</summary>
[Flags]
public enum HeldModifiers : byte
{
    None = 0,
    LShift = 1,
    RShift = 2,
    LCtrl = 4,
    RCtrl = 8,
    LAlt = 16,
    RAlt = 32,
    LWin = 64,
    RWin = 128,
}

/// <summary>
/// Modifier state as seen by a low-level hook. The hook misses key-ups that happen on the secure desktop
/// (Win+L, Ctrl+Alt+Del, UAC) or in elevated windows, so <see cref="Reconcile"/> must be called before the
/// state is trusted.
/// </summary>
public sealed class ModifierTracker
{
    public static readonly (ushort Vk, HeldModifiers Flag)[] Keys =
    [
        (VirtualKeys.LShift, HeldModifiers.LShift), (VirtualKeys.RShift, HeldModifiers.RShift),
        (VirtualKeys.LControl, HeldModifiers.LCtrl), (VirtualKeys.RControl, HeldModifiers.RCtrl),
        (VirtualKeys.LMenu, HeldModifiers.LAlt), (VirtualKeys.RMenu, HeldModifiers.RAlt),
        (VirtualKeys.LWin, HeldModifiers.LWin), (VirtualKeys.RWin, HeldModifiers.RWin),
    ];

    public HeldModifiers Held { get; private set; }

    public Modifiers Modifiers
    {
        get
        {
            var result = Input.Modifiers.None;
            if ((Held & (HeldModifiers.LShift | HeldModifiers.RShift)) != 0)
                result |= Input.Modifiers.Shift;
            if ((Held & (HeldModifiers.LCtrl | HeldModifiers.RCtrl)) != 0)
                result |= Input.Modifiers.Ctrl;
            if ((Held & (HeldModifiers.LAlt | HeldModifiers.RAlt)) != 0)
                result |= Input.Modifiers.Alt;
            if ((Held & (HeldModifiers.LWin | HeldModifiers.RWin)) != 0)
                result |= Input.Modifiers.Win;
            return result;
        }
    }

    public void Track(ushort vk, bool down)
    {
        foreach (var (keyVk, flag) in Keys)
        {
            if (keyVk == vk)
            {
                Held = down ? Held | flag : Held & ~flag;
                return;
            }
        }
    }

    /// <summary>Drops every held flag whose key is physically up. Never adds flags.</summary>
    public void Reconcile(Func<ushort, bool> isPhysicallyDown)
    {
        foreach (var (vk, flag) in Keys)
        {
            if ((Held & flag) != 0 && !isPhysicallyDown(vk))
                Held &= ~flag;
        }
    }

    /// <summary>
    /// A swallowed hotkey leaves Windows seeing a bare Alt or Win press, which opens the menu bar or Start.
    /// </summary>
    public static bool NeedsMenuSuppression(HeldModifiers held) =>
        (held & (HeldModifiers.LAlt | HeldModifiers.RAlt | HeldModifiers.LWin | HeldModifiers.RWin)) != 0;
}
