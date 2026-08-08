using System.Runtime.InteropServices;

namespace LaptopGuard;

/// <summary>
/// Reads system-wide idle time (time since last keyboard/mouse input) via
/// the Win32 GetLastInputInfo API. Note: this only works cleanly when called
/// from a process running in the interactive user session — a service
/// running purely under SYSTEM with no session attachment will see stale
/// values. See README for the "Interact with desktop" / session-0 caveat.
/// </summary>
public static class IdleDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static TimeSpan GetIdleTime()
    {
        var lii = new LASTINPUTINFO();
        lii.cbSize = (uint)Marshal.SizeOf(lii);

        if (!GetLastInputInfo(ref lii))
            return TimeSpan.Zero;

        uint idleTicks = (uint)Environment.TickCount - lii.dwTime;
        return TimeSpan.FromMilliseconds(idleTicks);
    }
}
