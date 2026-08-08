namespace LaptopGuard;

public static class Program
{
    // IMPORTANT ARCHITECTURE NOTE (read this before changing how this is launched):
    //
    // Windows Services run in Session 0, isolated from the interactive desktop
    // session since Vista. That means a service process CANNOT reliably call
    // GetForegroundWindow(), install a low-level mouse hook (WH_MOUSE_LL), or
    // read per-user idle time via GetLastInputInfo() — those all need to run
    // inside the user's own logon session.
    //
    // So this exe has two modes:
    //   (no args)   -> runs as the Windows Service (SYSTEM, session 0).
    //                  Handles: power events (sleep/wake), session events
    //                  (logon/logoff/lock/unlock), and launching the agent
    //                  into the interactive session via CreateProcessAsUser.
    //   --agent     -> runs as a small process inside the logged-on user's
    //                  session. Handles: foreground window/process logging,
    //                  idle detection, mouse-hook-triggered photo capture.
    //
    // The service launches the agent automatically on logon/unlock; you
    // never start --agent by hand.
    public static void Main(string[] args)
    {
        if (args.Contains("--agent"))
        {
            AgentRunner.Run();
        }
        else
        {
            System.ServiceProcess.ServiceBase.Run(new GuardService());
        }
    }
}
