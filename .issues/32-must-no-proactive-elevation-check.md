---
name: MUST — No proactive elevation check on write cmdlets
labels: [bug, MUST]
---

## Rule violated
- **Rule number:** Rule 1
- **Rule name:** Elevation checks are mandatory for write operations

## Location
- **File:** `src/Services.Linux.Native/Commands/StartServiceCommand.cs`, `StopServiceCommand.cs`, `RestartServiceCommand.cs`, `SetServiceCommand.cs`, `NewServiceCommand.cs`, `RemoveServiceCommand.cs`

## What's wrong
Write cmdlets rely on reactive D-Bus polkit error translation instead of proactively checking elevation before acting. Rule 1 requires: "Every cmdlet that changes system state must check elevation before acting."

Current pattern:
```csharp
try { SystemdHelper.StartUnit(unitName); }
catch (Exception ex) { WriteDBusError(unitName, "Start", ex); }
```

Required pattern:
```csharp
if (!Utils.IsAdministrator())
{
    throw new PSSecurityException($"{MyInvocation.MyCommand.Name} requires root privileges.");
}
```

## How to fix
Add `Utils.IsAdministrator()` check at the start of each write cmdlet's `OperateOnService()` or `ProcessRecord()` method, before any D-Bus call. The `Utils.IsAdministrator()` fix from the PowerShell fork (commit `70f96ff`) checks effective UID via `/proc/self/status` on Linux.

## Severity
- [x] MUST — blocks merge
