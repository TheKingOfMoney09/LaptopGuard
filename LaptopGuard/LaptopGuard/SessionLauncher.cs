using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LaptopGuard;

[SupportedOSPlatform("windows")]
public static class SessionLauncher
{
    public static void LaunchAgentInActiveSessionIfAny()
    {
        uint sessionId = NativeMethods.WTSGetActiveConsoleSessionId();
        if (sessionId != 0xFFFFFFFF)
        {
            LaunchAgentInSession((int)sessionId);
        }
    }

    public static void LaunchAgentInSession(int sessionId)
    {
        IntPtr userToken = IntPtr.Zero;
        IntPtr primaryToken = IntPtr.Zero;
        IntPtr envBlock = IntPtr.Zero;

        try
        {
            if (!NativeMethods.WTSQueryUserToken((uint)sessionId, out userToken))
                return; // no interactive user in this session (e.g. lock screen only)

            var sa = new NativeMethods.SECURITY_ATTRIBUTES();
            sa.nLength = Marshal.SizeOf(sa);

            if (!NativeMethods.DuplicateTokenEx(
                    userToken,
                    NativeMethods.MAXIMUM_ALLOWED,
                    ref sa,
                    NativeMethods.SECURITY_IMPERSONATION_LEVEL.SecurityIdentification,
                    NativeMethods.TOKEN_TYPE.TokenPrimary,
                    out primaryToken))
                return;

            NativeMethods.CreateEnvironmentBlock(out envBlock, primaryToken, false);

            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
            string commandLine = $"\"{exePath}\" --agent";

            var startupInfo = new NativeMethods.STARTUPINFO();
            startupInfo.cb = Marshal.SizeOf(startupInfo);
            startupInfo.lpDesktop = "winsta0\\default"; // run on the visible desktop of that session

            var processInfo = new NativeMethods.PROCESS_INFORMATION();

            uint flags = NativeMethods.CREATE_UNICODE_ENVIRONMENT | NativeMethods.CREATE_NO_WINDOW;

            NativeMethods.CreateProcessAsUser(
                primaryToken,
                null,
                commandLine,
                ref sa,
                ref sa,
                false,
                flags,
                envBlock,
                null,
                ref startupInfo,
                out processInfo);

            if (processInfo.hProcess != IntPtr.Zero) NativeMethods.CloseHandle(processInfo.hProcess);
            if (processInfo.hThread != IntPtr.Zero) NativeMethods.CloseHandle(processInfo.hThread);
        }
        finally
        {
            if (envBlock != IntPtr.Zero) NativeMethods.DestroyEnvironmentBlock(envBlock);
            if (primaryToken != IntPtr.Zero) NativeMethods.CloseHandle(primaryToken);
            if (userToken != IntPtr.Zero) NativeMethods.CloseHandle(userToken);
        }
    }
}
