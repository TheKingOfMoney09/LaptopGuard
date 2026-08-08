using System.Text.Json;
using System.Security.AccessControl; // FileSystemAclExtensions: GetAccessControl/SetAccessControl

namespace LaptopGuard;

/// <summary>
/// Writes newline-delimited JSON log events to disk. One JSON object per line,
/// so the log can be tailed/parsed without loading the whole file.
/// </summary>
public sealed class EventLogger
{
    // Real Windows locations only. Do NOT write into %WINDIR% (C:\Windows) —
    // AV/EDR heuristically flags unsigned processes creating files under the
    // system directory, and Windows File Protection can silently block it anyway.
    // ProgramData is where legitimate background services keep their data and
    // is hidden from casual Explorer browsing (it's marked as a system+hidden
    // folder), which is what you actually want here.
    public static readonly string RootDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WinDefragSvc", "data");

    public static readonly string PhotosDir = Path.Combine(RootDir, "captures");
    private static readonly string LogFilePath = Path.Combine(RootDir, "activity.log");
    private const long MaxLogBytes = 25 * 1024 * 1024; // rotate at 25MB

    private readonly object _lock = new();

    public EventLogger()
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(PhotosDir);
        TryHardenAcl(RootDir);
    }

    public void Log(string eventType, object? details = null)
    {
        var entry = new
        {
            ts = DateTimeOffset.Now.ToString("O"),
            evt = eventType,
            details
        };

        string line = JsonSerializer.Serialize(entry);

        lock (_lock)
        {
            RotateIfNeeded();
            File.AppendAllText(LogFilePath, line + Environment.NewLine);
        }
    }

    private void RotateIfNeeded()
    {
        var fi = new FileInfo(LogFilePath);
        if (!fi.Exists || fi.Length < MaxLogBytes) return;

        string archived = Path.Combine(RootDir, $"activity-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.Move(LogFilePath, archived);

        // Keep only the last 10 archived logs so this doesn't grow forever.
        var old = new DirectoryInfo(RootDir)
            .GetFiles("activity-*.log")
            .OrderByDescending(f => f.CreationTimeUtc)
            .Skip(10);
        foreach (var f in old) f.Delete();
    }

    /// <summary>
    /// Restricts the data folder to Administrators + SYSTEM only, so a
    /// non-admin user account on the machine can't read or tamper with the
    /// logs/photos even if they find the folder.
    /// </summary>
    private static void TryHardenAcl(string dir)
    {
        try
        {
            var dirInfo = new DirectoryInfo(dir);
            var security = dirInfo.GetAccessControl();
            security.SetAccessRuleProtection(true, false); // strip inherited rules

            var admins = new System.Security.Principal.SecurityIdentifier(
                System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null);
            var system = new System.Security.Principal.SecurityIdentifier(
                System.Security.Principal.WellKnownSidType.LocalSystemSid, null);

            security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                admins, System.Security.AccessControl.FileSystemRights.FullControl,
                System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                System.Security.AccessControl.PropagationFlags.None,
                System.Security.AccessControl.AccessControlType.Allow));

            security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                system, System.Security.AccessControl.FileSystemRights.FullControl,
                System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                System.Security.AccessControl.PropagationFlags.None,
                System.Security.AccessControl.AccessControlType.Allow));

            dirInfo.SetAccessControl(security);
        }
        catch
        {
            // ACL hardening is best-effort; don't crash the service over it.
        }
    }
}
