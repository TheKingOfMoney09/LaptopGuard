# LaptopGuard
A lightweight Windows Service that logs activity and power state on your own laptop, and snaps a webcam still at key moments — boot, resume from sleep, unlock, and the first mouse movement after a period of idle. Built to answer one question: did someone touch my laptop while I wasn't around?
# LaptopGuard

> **Scope:** this is designed for monitoring a device you own or administer,
> installed with your own knowledge. It is not designed or intended for
> covertly monitoring someone else's account or activity without their
> knowledge or consent.

---

## What it does

- **Power event logging** — boot, sleep, resume, shutdown, all timestamped.
- **Session event logging** — logon, logoff, lock, unlock, console
  connect/disconnect.
- **Active window/process logging** — what's in the foreground and when it
  changed.
- **Idle-triggered photo capture** — takes a webcam still on boot, on resume
  from sleep, on unlock, and on the first mouse movement after 60+ seconds
  of inactivity.
- **Survives reboots** — runs as an auto-starting Windows Service under
  `LocalSystem`, with automatic restart on crash.

## How it works

Windows Services run isolated from the interactive desktop (Session 0) and
can't see the foreground window, read the mouse, or use the webcam on their
own. So the same executable runs in two modes:

| Mode | Runs as | Responsible for |
|---|---|---|
| **Service** (default) | SYSTEM, Session 0 | Power/session events, boot & resume photos, launching the agent into the logged-in user's session |
| **Agent** (`--agent`) | The logged-in user, launched automatically | Foreground window polling, idle detection, mouse-hook-triggered photos |

```
┌─────────────────────┐        CreateProcessAsUser        ┌─────────────────────┐
│   GuardService       │ ─────────────────────────────────▶│   AgentRunner        │
│   (SYSTEM, Session 0)│      on logon / unlock / resume    │   (user session)     │
│                       │                                    │                       │
│ • power events        │                                    │ • foreground window   │
│ • session events       │                                    │ • idle detection       │
│ • boot/resume photo     │                                    │ • mouse hook → photo    │
└─────────────────────┘                                    └─────────────────────┘
                 \                                                    /
                  \                                                  /
                   ▼                                                ▼
                          C:\ProgramData\WinDefragSvc\data\
                          activity.log (JSON lines) + captures\*.jpg
```

## Requirements

- Windows 10 (build 14393+) or Windows 11
- Admin rights to install (self-contained, no separate .NET install needed
  on the target machine — the runtime is bundled into the published exe)
- A webcam

## Install

```powershell
# from an elevated PowerShell prompt
cd LaptopGuard
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\install.ps1
```

This publishes a self-contained single-file exe, installs it to
`C:\ProgramData\WinDefragSvc\bin\` (ACL-locked to Administrators + SYSTEM),
and registers + starts the `WinDefragSvcHelper` service.

```powershell
# to remove
.\uninstall.ps1
```

## Where data goes

```
C:\ProgramData\WinDefragSvc\data\
├── activity.log          # newline-delimited JSON, one event per line
└── captures\
    ├── 20260810-091203-441_boot.jpg
    ├── 20260810-134501-002_idle_resume.jpg
    └── ...
```

Folder permissions are restricted to Administrators and SYSTEM at install
time, so other accounts on the machine can't read or tamper with the data.

## Project layout

```
LaptopGuard/
├── LaptopGuard/
│   ├── Program.cs           # entry point — dispatches service vs. agent mode
│   ├── GuardService.cs      # ServiceBase — power/session event handlers
│   ├── SessionLauncher.cs   # breaks out of Session 0 via CreateProcessAsUser
│   ├── NativeMethods.cs     # P/Invoke declarations for SessionLauncher
│   ├── AgentRunner.cs       # foreground window + idle polling, mouse hook wiring
│   ├── MouseHook.cs         # WH_MOUSE_LL hook + Win32 message loop
│   ├── IdleDetector.cs      # GetLastInputInfo wrapper
│   ├── WebcamCapture.cs     # single-frame JPEG capture via OpenCvSharp
│   └── EventLogger.cs       # JSON-lines writer, rotation, folder ACL hardening
├── install.ps1
├── uninstall.ps1
└── README.md
```

## Known limitations

- **Camera indicator light is visible.** Windows shows a green light and
  notification whenever the camera opens, on any account. This code doesn't
  and can't suppress that.
- **Console session only.** Targets `WTSGetActiveConsoleSessionId()` — fast
  user switching and RDP sessions beyond the console aren't handled.
- **Expect AV/EDR flags.** The behavior pattern (hooking input, opening the
  webcam, running as a hidden service) is exactly what heuristics look for.
  Code-signing and/or a Defender exclusion may be needed.
- **No tamper-evidence.** Local admin access can stop the service and wipe
  the data folder. If that's part of your threat model, ship logs off-box
  rather than relying on local storage alone.
