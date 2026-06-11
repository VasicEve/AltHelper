using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MouseToPad;

/// <summary>
/// Foreground-window and idle checks shared by the keyboard hook and the
/// anti-AFK timer. The focus result is cached briefly per window handle
/// because the hook consults it on mapped keystrokes and must stay fast.
/// </summary>
internal static class FocusWatch
{
    private const string MoonlightProcess = "Moonlight";

    private static IntPtr _cachedHwnd;
    private static bool _cachedResult;
    private static uint _cachedAt;

    public static bool IsMoonlightFocused()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        uint now = (uint)Environment.TickCount;
        if (hwnd == _cachedHwnd && now - _cachedAt < 1000)
            return _cachedResult;

        bool result = false;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid != 0)
        {
            try
            {
                using var p = Process.GetProcessById((int)pid);
                result = p.ProcessName.Equals(MoonlightProcess, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // process exited between the two calls
            }
        }

        _cachedHwnd = hwnd;
        _cachedResult = result;
        _cachedAt = now;
        return result;
    }

    /// <summary>True if there was keyboard/mouse input (physical or injected)
    /// within the given window — i.e. the user is actively at the controls.</summary>
    public static bool UserActiveWithin(TimeSpan window)
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info))
            return false;
        uint idleMs = (uint)Environment.TickCount - info.dwTime;   // unsigned math survives wrap
        return idleMs < window.TotalMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
