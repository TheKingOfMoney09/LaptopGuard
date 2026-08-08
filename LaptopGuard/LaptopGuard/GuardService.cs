using System.ServiceProcess;

namespace LaptopGuard;

public sealed class GuardService : ServiceBase
{
    private readonly EventLogger _logger = new();
    private readonly WebcamCapture _webcam;

    public GuardService()
    {
        ServiceName = "WinDefragSvcHelper";
        CanHandlePowerEvent = true;
        CanHandleSessionChangeEvent = true;
        CanShutdown = true;
        _webcam = new WebcamCapture(_logger);
    }

    protected override void OnStart(string[] args)
    {
        _logger.Log("service_start");
        // Boot capture: fires whenever the service starts, which for an
        // auto-start service means every machine boot.
        _ = Task.Run(() => _webcam.CaptureStill("boot"));

        // If a user is already logged on when the service starts (e.g. the
        // service was just installed, or restarted itself), launch the
        // agent into that session immediately instead of waiting for the
        // next logon event.
        _ = Task.Run(SessionLauncher.LaunchAgentInActiveSessionIfAny);
    }

    protected override void OnStop()
    {
        _logger.Log("service_stop");
    }

    protected override void OnShutdown()
    {
        _logger.Log("system_shutdown");
    }

    protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
    {
        _logger.Log("power_event", new { status = powerStatus.ToString() });

        switch (powerStatus)
        {
            case PowerBroadcastStatus.ResumeAutomatic:
            case PowerBroadcastStatus.ResumeSuspend:
            case PowerBroadcastStatus.ResumeCritical:
                _ = Task.Run(() => _webcam.CaptureStill("resume"));
                _ = Task.Run(SessionLauncher.LaunchAgentInActiveSessionIfAny);
                break;
            case PowerBroadcastStatus.Suspend:
                _logger.Log("suspend_pending");
                break;
        }

        // true tells Windows this service accepts the power event; returning
        // false for QuerySuspend would veto the sleep request, which we never want.
        return true;
    }

    protected override void OnSessionChange(SessionChangeDescription changeDescription)
    {
        _logger.Log("session_change", new
        {
            reason = changeDescription.Reason.ToString(),
            sessionId = changeDescription.SessionId
        });

        switch (changeDescription.Reason)
        {
            case SessionChangeReason.SessionLogon:
            case SessionChangeReason.SessionUnlock:
            case SessionChangeReason.ConsoleConnect:
                _ = Task.Run(() => _webcam.CaptureStill(changeDescription.Reason.ToString()));
                _ = Task.Run(() => SessionLauncher.LaunchAgentInSession(changeDescription.SessionId));
                break;

            case SessionChangeReason.SessionLogoff:
            case SessionChangeReason.SessionLock:
            case SessionChangeReason.ConsoleDisconnect:
                // Nothing to launch; the agent process exits on its own
                // when its session ends.
                break;
        }

        base.OnSessionChange(changeDescription);
    }
}
