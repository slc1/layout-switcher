using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using LayoutSwitcher.Core.Input;
using LayoutSwitcher.Mac.Native;

namespace LayoutSwitcher.Mac.Input;

/// <summary>
/// A Quartz event tap for keys and mouse clicks, on its own thread and run loop. macOS disables a tap whose
/// callback is too slow, so the callback only matches hotkeys and posts events; the engine does the work.
/// Needs the Accessibility permission.
/// </summary>
internal sealed unsafe class EventTap : IDisposable
{
    private static readonly ulong Mask = CG.MaskFor(
        CG.EventKeyDown, CG.EventKeyUp,
        CG.EventLeftMouseDown, CG.EventRightMouseDown, CG.EventOtherMouseDown);

    private readonly ChannelWriter<InputEvent> _events;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();
    private GCHandle _self;
    private nint _tap;
    private nint _runLoop;
    private int _swallowedKeyCode = -1;

    public EventTap(ChannelWriter<InputEvent> events)
    {
        _events = events;
        _thread = new Thread(Run) { IsBackground = true, Name = "EventTap" };
    }

    public volatile HotkeyMap Hotkeys = HotkeyMap.Empty;
    public volatile bool Enabled = true;

    /// <summary>The frontmost app is on the exclusion list; set by the UI thread on every app switch.</summary>
    public volatile bool Excluded;

    /// <summary>Set while the settings window captures a hotkey, so our hotkeys reach it.</summary>
    public volatile bool Suspended;

    /// <summary>Throws when the tap cannot be created, which means the Accessibility permission is missing.</summary>
    public void Start()
    {
        _self = GCHandle.Alloc(this);
        _thread.Start();
        _started.Wait();
        if (_tap == 0)
        {
            _self.Free();
            throw new InvalidOperationException("Could not create the event tap");
        }
    }

    /// <summary>Lets the engine treat an app switch like a click: the caret is somewhere else now.</summary>
    public void PostReset() => _events.TryWrite(new InputEvent(InputKind.Reset));

    private void Run()
    {
        _tap = CG.CGEventTapCreate(CG.SessionEventTap, CG.HeadInsertEventTap,
            CG.TapOptionDefault, Mask, &Callback, GCHandle.ToIntPtr(_self));
        if (_tap == 0)
        {
            _started.Set();
            return;
        }

        _runLoop = CF.CFRunLoopGetCurrent();
        var source = CF.CFMachPortCreateRunLoopSource(0, _tap, 0);
        CF.CFRunLoopAddSource(_runLoop, source, CF.DefaultRunLoopMode);
        CG.CGEventTapEnable(_tap, true);
        _started.Set();

        CF.CFRunLoopRun();

        CF.CFMachPortInvalidate(_tap);
        CF.CFRelease(source);
        CF.CFRelease(_tap);
    }

    /// <summary>Returns the event to pass it on, or 0 to swallow it.</summary>
    [UnmanagedCallersOnly]
    private static nint Callback(nint proxy, int type, nint ev, nint userInfo)
    {
        var self = (EventTap)GCHandle.FromIntPtr(userInfo).Target!;
        return self.Handle(type, ev) ? ev : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Handle(int type, nint ev)
    {
        switch (type)
        {
            case CG.EventTapDisabledByTimeout or CG.EventTapDisabledByUserInput:
                CG.CGEventTapEnable(_tap, true);
                return true;
            case CG.EventLeftMouseDown or CG.EventRightMouseDown or CG.EventOtherMouseDown:
                _events.TryWrite(new InputEvent(InputKind.Reset));
                return true;
            case CG.EventKeyDown or CG.EventKeyUp:
                return HandleKey(type == CG.EventKeyDown, ev);
            default:
                return true;
        }
    }

    private bool HandleKey(bool down, nint ev)
    {
        if (CG.CGEventGetIntegerValueField(ev, CG.FieldEventSourceUserData) == InputSender.Tag)
            return true;
        if (Suspended || Excluded)
            return true;

        var keyCode = (ushort)CG.CGEventGetIntegerValueField(ev, CG.FieldKeyboardEventKeycode);
        if (keyCode == _swallowedKeyCode)
        {
            // The release and the auto-repeats of a swallowed hotkey are swallowed too; a fresh press is not.
            if (!down)
            {
                _swallowedKeyCode = -1;
                return false;
            }
            if (CG.CGEventGetIntegerValueField(ev, CG.FieldKeyboardEventAutorepeat) != 0)
                return false;
            _swallowedKeyCode = -1;
        }

        if (!down)
            return true;

        var flags = CG.CGEventGetFlags(ev);
        var modifiers = MacKeys.ModifiersFrom(flags);
        var vk = MacKeys.ToVk(keyCode);
        if (vk != 0 && Hotkeys.TryMatch(modifiers, vk, Enabled, out var action))
        {
            _swallowedKeyCode = keyCode;
            _events.TryWrite(new InputEvent(InputKind.Hotkey, Action: action));
            return false;
        }

        if (Enabled)
        {
            var keypad = (flags & CG.FlagNumericPad) != 0;
            var capsLock = (flags & CG.FlagAlphaShift) != 0;
            _events.TryWrite(new InputEvent(InputKind.Key, vk, keyCode, keypad, modifiers, capsLock));
        }
        return true;
    }

    public void Dispose()
    {
        if (_runLoop != 0)
        {
            CF.CFRunLoopStop(_runLoop);
            _thread.Join(TimeSpan.FromSeconds(2));
        }
        if (_self.IsAllocated)
            _self.Free();
        _started.Dispose();
    }
}
