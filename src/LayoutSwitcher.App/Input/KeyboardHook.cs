using System.Threading.Channels;
using LayoutSwitcher.App.Win32;
using LayoutSwitcher.Core.Config;
using LayoutSwitcher.Core.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace LayoutSwitcher.App.Input;

/// <summary>What the hook thread knows about the foreground process.</summary>
internal sealed record ForegroundState(bool Excluded, bool Elevated)
{
    public static readonly ForegroundState Unknown = new(false, false);
}

/// <summary>
/// Low-level keyboard and mouse hooks plus a foreground-change hook, on one dedicated thread.
/// Windows silently removes a low-level hook whose callback is too slow, so the callbacks only match
/// hotkeys and post events; the engine does the work.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private static readonly (ushort Vk, HeldModifiers Flag)[] ModifierVks =
    [
        (VirtualKeys.LShift, HeldModifiers.LShift), (VirtualKeys.RShift, HeldModifiers.RShift),
        (VirtualKeys.LControl, HeldModifiers.LCtrl), (VirtualKeys.RControl, HeldModifiers.RCtrl),
        (VirtualKeys.LMenu, HeldModifiers.LAlt), (VirtualKeys.RMenu, HeldModifiers.RAlt),
        (VirtualKeys.LWin, HeldModifiers.LWin), (VirtualKeys.RWin, HeldModifiers.RWin),
    ];

    private readonly ChannelWriter<InputEvent> _events;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();

    // Delegates must stay referenced for as long as the hooks are installed.
    private readonly HOOKPROC _keyboardProc;
    private readonly HOOKPROC _mouseProc;
    private readonly WINEVENTPROC _foregroundProc;

    private uint _threadId;
    private HeldModifiers _held;
    private ushort _swallowedVk;
    private Exception? _startError;

    public KeyboardHook(ChannelWriter<InputEvent> events)
    {
        _events = events;
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
        _foregroundProc = ForegroundProc;
        _thread = new Thread(Run) { IsBackground = true, Name = "KeyboardHook" };
    }

    public volatile HotkeyMap Hotkeys = HotkeyMap.Empty;
    public volatile ExclusionList Exclusions = new([]);
    public volatile bool Enabled = true;

    /// <summary>Set while the settings window captures a hotkey, so our hotkeys reach it.</summary>
    public volatile bool Suspended = false;

    public ForegroundState Foreground { get; private set; } = ForegroundState.Unknown;

    public void Start()
    {
        _thread.Start();
        _started.Wait();
        if (_startError is not null)
            throw new InvalidOperationException("Could not install keyboard hooks", _startError);
    }

    /// <summary>Re-reads the foreground process, e.g. after the exclusion list changed.</summary>
    public void RefreshForeground() => Foreground = ReadForeground();

    private void Run()
    {
        _threadId = PInvoke.GetCurrentThreadId();
        using var module = PInvoke.GetModuleHandle((string?)null);
        using var keyboard = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _keyboardProc, module, 0);
        using var mouse = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, _mouseProc, module, 0);
        using var foreground = PInvoke.SetWinEventHook(PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND,
            null, _foregroundProc, 0, 0, 0 /* WINEVENT_OUTOFCONTEXT */);

        if (keyboard.IsInvalid || mouse.IsInvalid)
        {
            _startError = new System.ComponentModel.Win32Exception();
            _started.Set();
            return;
        }

        Foreground = ReadForeground();
        _started.Set();

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
            PInvoke.DispatchMessage(msg);
    }

    private unsafe LRESULT KeyboardProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code < 0)
            return PInvoke.CallNextHookEx(null, code, wParam, lParam);

        var data = (KBDLLHOOKSTRUCT*)lParam.Value;
        if (data->dwExtraInfo == InputSender.Tag)
            return PInvoke.CallNextHookEx(null, code, wParam, lParam);

        var down = wParam.Value is PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN;
        var scan = (ushort)data->scanCode;
        var vk = Hotkey.NormalizeVk((ushort)data->vkCode, scan);
        TrackModifier(vk, down);

        if (Suspended || Foreground.Excluded)
            return PInvoke.CallNextHookEx(null, code, wParam, lParam);

        if (vk == _swallowedVk && _swallowedVk != 0)
        {
            // Key-up or auto-repeat of a hotkey we already handled: keep it from the app.
            if (!down)
                _swallowedVk = 0;
            return (LRESULT)1;
        }

        if (!down)
            return PInvoke.CallNextHookEx(null, code, wParam, lParam);

        var modifiers = ToModifiers(_held);
        if (Hotkeys.TryMatch(modifiers, vk, Enabled, out var action))
        {
            _swallowedVk = vk;
            _events.TryWrite(new InputEvent(InputKind.Hotkey, Action: action, Held: _held));
            return (LRESULT)1;
        }

        if (Enabled)
        {
            var extended = (data->flags & KBDLLHOOKSTRUCT_FLAGS.LLKHF_EXTENDED) != 0;
            var capsLock = (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CAPITAL) & 1) != 0;
            _events.TryWrite(new InputEvent(InputKind.Key, vk, scan, extended, modifiers, capsLock));
        }
        return PInvoke.CallNextHookEx(null, code, wParam, lParam);
    }

    private LRESULT MouseProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && wParam.Value is PInvoke.WM_LBUTTONDOWN or PInvoke.WM_RBUTTONDOWN or PInvoke.WM_MBUTTONDOWN)
            _events.TryWrite(new InputEvent(InputKind.Reset));
        return PInvoke.CallNextHookEx(null, code, wParam, lParam);
    }

    private void ForegroundProc(HWINEVENTHOOK hook, uint @event, HWND hwnd, int idObject, int idChild,
        uint eventThread, uint eventTime)
    {
        Foreground = ReadForeground();
        _events.TryWrite(new InputEvent(InputKind.Reset));
    }

    private ForegroundState ReadForeground()
    {
        var pid = ProcessInfo.ForegroundProcessId();
        if (pid == 0)
            return ForegroundState.Unknown;
        var path = ProcessInfo.ImagePath(pid);
        return new ForegroundState(
            Excluded: path is not null && Exclusions.IsExcluded(path),
            Elevated: !Environment.IsPrivilegedProcess && ProcessInfo.IsElevated(pid));
    }

    private void TrackModifier(ushort vk, bool down)
    {
        foreach (var (modifierVk, flag) in ModifierVks)
        {
            if (modifierVk == vk)
            {
                _held = down ? _held | flag : _held & ~flag;
                return;
            }
        }
    }

    private static Modifiers ToModifiers(HeldModifiers held)
    {
        var result = Modifiers.None;
        if ((held & (HeldModifiers.LShift | HeldModifiers.RShift)) != 0)
            result |= Modifiers.Shift;
        if ((held & (HeldModifiers.LCtrl | HeldModifiers.RCtrl)) != 0)
            result |= Modifiers.Ctrl;
        if ((held & (HeldModifiers.LAlt | HeldModifiers.RAlt)) != 0)
            result |= Modifiers.Alt;
        if ((held & (HeldModifiers.LWin | HeldModifiers.RWin)) != 0)
            result |= Modifiers.Win;
        return result;
    }

    public void Dispose()
    {
        if (_threadId != 0)
        {
            PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, 0, 0);
            _thread.Join(TimeSpan.FromSeconds(2));
        }
        _started.Dispose();
    }
}
