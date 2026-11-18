using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;

namespace LayoutSwitcher.App.Win32;

internal static class ProcessInfo
{
    public static uint ForegroundProcessId()
    {
        var hwnd = PInvoke.GetForegroundWindow();
        if (hwnd == HWND.Null)
            return 0;
        PInvoke.GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    public static string? ImagePath(uint pid)
    {
        using var process = OpenLimited(pid);
        if (process is null)
            return null;

        Span<char> buffer = stackalloc char[1024];
        var size = (uint)buffer.Length;
        return PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref size)
            ? buffer[..(int)size].ToString()
            : null;
    }

    /// <summary>
    /// True when the process runs elevated. A token we may not even open is treated as elevated:
    /// from a non-elevated process that is what an elevated target looks like.
    /// </summary>
    public static unsafe bool IsElevated(uint pid)
    {
        using var process = OpenLimited(pid);
        if (process is null)
            return false;
        if (!PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
            return true;
        using (token)
        {
            TOKEN_ELEVATION elevation;
            return PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenElevation, &elevation,
                       (uint)sizeof(TOKEN_ELEVATION), out _)
                   && elevation.TokenIsElevated != 0;
        }
    }

    private static SafeFileHandle? OpenLimited(uint pid)
    {
        if (pid == 0)
            return null;
        var handle = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        return handle == HANDLE.Null ? null : new SafeFileHandle(handle, ownsHandle: true);
    }
}
