namespace LayoutSwitcher.Core.Input;

/// <summary>Win32 virtual-key codes used by the app.</summary>
public static class VirtualKeys
{
    public const ushort Cancel = 0x03;
    public const ushort Back = 0x08;
    public const ushort Tab = 0x09;
    public const ushort Return = 0x0D;
    public const ushort Shift = 0x10;
    public const ushort Control = 0x11;
    public const ushort Menu = 0x12;
    public const ushort Pause = 0x13;
    public const ushort CapsLock = 0x14;
    public const ushort Escape = 0x1B;
    public const ushort Space = 0x20;
    public const ushort Prior = 0x21;
    public const ushort Next = 0x22;
    public const ushort End = 0x23;
    public const ushort Home = 0x24;
    public const ushort Left = 0x25;
    public const ushort Up = 0x26;
    public const ushort Right = 0x27;
    public const ushort Down = 0x28;
    public const ushort Snapshot = 0x2C;
    public const ushort Insert = 0x2D;
    public const ushort Delete = 0x2E;
    public const ushort LWin = 0x5B;
    public const ushort RWin = 0x5C;
    public const ushort F1 = 0x70;
    public const ushort F24 = 0x87;
    public const ushort NumLock = 0x90;
    public const ushort ScrollLock = 0x91;
    public const ushort LShift = 0xA0;
    public const ushort RShift = 0xA1;
    public const ushort LControl = 0xA2;
    public const ushort RControl = 0xA3;
    public const ushort LMenu = 0xA4;
    public const ushort RMenu = 0xA5;

    public static bool IsModifier(ushort vk) =>
        vk is Shift or Control or Menu or LWin or RWin or (>= LShift and <= RMenu);

    public static bool IsFunctionKey(ushort vk) => vk is >= F1 and <= F24;
}
