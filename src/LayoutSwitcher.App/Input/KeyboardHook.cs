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
    private static readonly Func<ushort, bool> IsPhysicallyDown = IsAsyncDown;

    private readonly ChannelWriter<InputEvent> _events;
    private readonly ModifierTracker _modifiers = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();

    // Delegates must stay referenced for as long as the hooks are installed.
    private readonly HOOKPROC _keyboardProc;
    private readonly HOOKPROC _mouseProc;
    private readonly WINEVENTPROC _foregroundProc;

    private uint _threadId;
    private ushort _swallowedVk;
    private volatile ForegroundState _foreground = ForegroundState.Unknown;
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

    public ForegroundState Foreground => _foreground;

    public void Start()
    {
        _thread.Start();
        _started.Wait();
        if (_startError is not null)
            throw new InvalidOperationException("Could not install keyboard hooks", _startError);
    }

    /// <summary>Re-reads the foreground process, e.g. after the exclusion list changed.</summary>
    public void RefreshForeground() => _foreground = ReadForeground();

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

        _foreground = ReadForeground();
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
        var rawVk = (ushort)data->vkCode;
        var scan = (ushort)data->scanCode;
        var vk = Hotkey.NormalizeVk(rawVk, scan);

        if (VirtualKeys.IsModifier(vk))
        {
            _modifiers.Track(vk, down);
            return PInvoke.CallNextHookEx(null, code, wParam, lParam);
        }

        // Releases on the secure desktop (Win+L, Ctrl+Alt+Del, UAC) never reach us. The async state does not
        // yet include the event being processed, which is why this runs only for non-modifier keys.
        _modifiers.Reconcile(IsPhysicallyDown);

        if (Suspended || _foreground.Excluded)
            return PInvoke.CallNextHookEx(null, code, wParam, lParam);

        if (vk == _swallowedVk && _swallowedVk != 0)
        {
            // Async state "down" on a key-down means auto-repeat; "up" means a fresh press whose
            // previous release we never saw, so it is handled as a new press below.
            if (!down)
            {
                _swallowedVk = 0;
                return (LRESULT)1;
            }
            if (IsAsyncDown(rawVk))
                return (LRESULT)1;
            _swallowedVk = 0;
        }

        if (!down)
            return PInvoke.CallNextHookEx(null, code, wParam, lParam);

        var modifiers = _modifiers.Modifiers;
        if (Hotkeys.TryMatch(modifiers, vk, Enabled, out var action))
        {
            _swallowedVk = vk;
            _events.TryWrite(new InputEvent(InputKind.Hotkey, Action: action, Held: _modifiers.Held));
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
        _foreground = ReadForeground();
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

    private static bool IsAsyncDown(ushort vk) => (PInvoke.GetAsyncKeyState(vk) & 0x8000) != 0;

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
