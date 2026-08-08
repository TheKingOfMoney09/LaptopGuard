using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LaptopGuard;

/// <summary>
/// Runs inside the interactive user's own logon session (launched by the
/// service via CreateProcessAsUser). No console window, no tray icon —
/// just a hidden message-loop thread so the low-level mouse hook works
/// (hooks require a running Windows message pump on the installing thread).
/// </summary>
public static class AgentRunner
{
    private static readonly EventLogger Logger = new();
    private static readonly WebcamCapture Webcam = new(Logger);

    private static readonly TimeSpan IdleThreshold = TimeSpan.FromMinutes(1);
    private static bool _wasIdle = false;

    private static string? _lastLoggedWindow;

    public static void Run()
    {
        Logger.Log("agent_start", new { sessionUser = Environment.UserName });

        // Foreground window/process polling on a background timer.
        var windowTimer = new System.Threading.Timer(_ => PollForegroundWindow(), null,
            TimeSpan.Zero, TimeSpan.FromSeconds(2));

        // Idle-state polling — cheap, so a short interval is fine.
        var idleTimer = new System.Threading.Timer(_ => PollIdleState(), null,
            TimeSpan.Zero, TimeSpan.FromSeconds(2));

        // Low-level mouse hook needs a real Win32 message loop on this thread.
        MouseHook.Install(OnMouseActivity);
        NativeMessageLoop.Run(); // blocks until WM_QUIT (session ending)

        windowTimer.Dispose();
        idleTimer.Dispose();
        MouseHook.Uninstall();
        Logger.Log("agent_stop");
    }

    private static void PollForegroundWindow()
    {
        try
        {
            IntPtr hwnd = Win32.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            Win32.GetWindowThreadProcessId(hwnd, out uint pid);
            string processName;
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                processName = proc.ProcessName;
            }
            catch
            {
                processName = "unknown";
            }

            var sb = new StringBuilder(512);
            Win32.GetWindowText(hwnd, sb, sb.Capacity);
            string title = sb.ToString();

            string key = $"{processName}|{title}";
            if (key == _lastLoggedWindow) return; // only log on change

            _lastLoggedWindow = key;
            Logger.Log("foreground_window", new { process = processName, title });
        }
        catch (Exception ex)
        {
            Logger.Log("poll_error", new { error = ex.Message });
        }
    }

    private static void PollIdleState()
    {
        bool isIdle = IdleDetector.GetIdleTime() >= IdleThreshold;

        if (isIdle && !_wasIdle)
        {
            Logger.Log("idle_start");
        }

        _wasIdle = isIdle;
    }

    private static void OnMouseActivity()
    {
        // Fires on every mouse move; we only care about the transition
        // out of an idle period — i.e. "someone just touched the mouse
        // after it sat untouched for 1+ minute".
        if (_wasIdle)
        {
            _wasIdle = false;
            Logger.Log("resume_from_idle_mouse");
            _ = Task.Run(() => Webcam.CaptureStill("idle_resume"));
        }
    }
}

internal static class Win32
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
