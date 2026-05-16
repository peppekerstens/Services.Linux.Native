# LinuxServiceInfo vs ServiceController — Compatibility Analysis

**Date:** 2026-05-16
**Status:** Open — awaiting upstream design decision
**Context:** Services.Linux.Native returns `LinuxServiceInfo` from `Get-Service`. Windows `Get-Service` returns `System.ServiceProcess.ServiceController`. This document details every incompatibility between the two types.

---

## 1. Type definitions

### LinuxServiceInfo (this repo)

```csharp
public sealed class LinuxServiceInfo   // extends object
{
    public string Name { get; internal set; }
    public string DisplayName { get; internal set; }
    public string Status { get; internal set; } = "Stopped";
    public ServiceStartupType StartType { get; internal set; }
    public string ActiveState { get; internal set; }
    public string SubState { get; internal set; }
}
```

- **Inheritance:** `object` (no base class)
- **Methods:** None
- **OutputType:** `[OutputType(typeof(LinuxServiceInfo))]` on `Get-Service`
- **No** `ToString()` override, **no** type converter, **no** format definition

### LinuxServiceInfo (PowerShell fork — `feature/service-unix-systemctl`)

```csharp
public sealed class LinuxServiceInfo   // extends object
{
    public string Name { get; internal set; }
    public string DisplayName { get; internal set; }
    public ServiceControllerStatus Status { get; internal set; }     // ← enum, not string
    public ServiceStartupType StartType { get; internal set; }
    public string ActiveState { get; internal set; }
    public string SubState { get; internal set; }
}
```

The fork changes `Status` from `string` to `ServiceControllerStatus`. All other fields identical.

### ServiceController (Windows, .NET 8)

```
System.ServiceProcess.ServiceController
  → System.ComponentModel.Component
    → System.MarshalByRefObject
      → System.Object
```

Implements: `IComponent`, `IDisposable`

---

## 2. Inheritance incompatibility

| Aspect | ServiceController | LinuxServiceInfo |
|---|---|---|
| Base class | `Component` → `MarshalByRefObject` | `object` |
| Implements | `IComponent`, `IDisposable` | nothing |
| Has `Disposed` event | Yes | No |

**Impact:** `$svc -is [System.ServiceProcess.ServiceController]` returns `$false` on Linux. Any script that type-checks or casts will break.

```powershell
# Windows: $true, Linux: $false
(Get-Service sshd) -is [System.ServiceProcess.ServiceController]

# Pipeline type filter — returns nothing on Linux
Get-Service | Where-Object { $_ -is [System.ServiceProcess.ServiceController] }
```

---

## 3. Property comparison

### Properties present on ServiceController but MISSING on LinuxServiceInfo

| Property | Type | Notes |
|---|---|---|
| `ServiceName` | `string` | Primary name — Linux uses `Name` instead |
| `MachineName` | `string` | |
| `CanPauseAndContinue` | `bool` | |
| `CanShutdown` | `bool` | |
| `CanStop` | `bool` | |
| `DependentServices` | `ServiceController[]` | |
| `ServicesDependedOn` | `ServiceController[]` | |
| `ServiceType` | `ServiceType` | Enum |
| `ServiceHandle` | `SafeHandle` | |
| `Container` | `IContainer` | From `Component` |
| `Site` | `ISite` | From `Component` |

### ETS NoteProperties (added by PowerShell's type system on Windows)

| Property | Type | Present on Linux? |
|---|---|---|
| `BinaryPathName` | `string` | No |
| `DelayedAutoStart` | `bool` | No |
| `Description` | `string` | No |
| `StartupType` | `ServiceStartupType` | No — Linux has `StartType` (different name) |
| `UserName` | `string` | No |

### Alias Properties (PowerShell)

| Alias | Maps To | Present on Linux? |
|---|---|---|
| `Name` | `ServiceName` | Yes — but directly, not as alias |
| `RequiredServices` | `ServicesDependedOn` | No |

### Properties present on LinuxServiceInfo but NOT on ServiceController

| Property | Type | Notes |
|---|---|---|
| `ActiveState` | `string` | Raw systemd state (`active`, `inactive`, `failed`) |
| `SubState` | `string` | Raw systemd sub-state (`running`, `dead`, `exited`) |

---

## 4. Type differences — Status property

| | Windows | Linux (this repo) | Linux (fork) |
|---|---|---|---|
| `Status` type | `ServiceControllerStatus` (enum) | **`string`** | `ServiceControllerStatus` (enum) |

**This repo uses `string` for `Status`.** The fork changes it to the real enum.

```powershell
# Works on Windows and fork, BREAKS on this repo:
$s.Status -eq [ServiceControllerStatus]::Running

# Works everywhere (string coercion):
$s.Status -eq 'Running'
```

**Why this repo uses `string`:** `ServiceControllerStatus` is annotated `[SupportedOSPlatform("windows")]` in .NET 8. Using it triggers CA1416 platform-compatibility warnings inside `TreatWarningsAsErrors=true` builds. The fork suppresses this with `#if UNIX` guards.

---

## 5. Enum compatibility

### ServiceControllerStatus (Windows) vs Status string (this repo)

| Windows enum value | This repo string | Fork enum |
|---|---|---|
| `Stopped` | `"Stopped"` | `Stopped` |
| `StartPending` | `"StartPending"` | `StartPending` |
| `StopPending` | `"StopPending"` | `StopPending` |
| `Running` | `"Running"` | `Running` |
| `ContinuePending` | `"ContinuePending"` | `ContinuePending` |
| `PausePending` | — (missing) | `PausePending` |
| `Paused` | — (missing) | `Paused` |

systemd has no pause/resume concept, so `Paused` and `PausePending` have no Linux equivalent.

### ServiceStartMode (Windows) vs ServiceStartupType (Linux)

| Windows value | Numeric | Linux value | Numeric | Compatible? |
|---|---|---|---|---|
| `Boot` | 0 | — | — | No |
| `System` | 1 | — | — | No |
| `Automatic` | 2 | `Automatic` | 2 | Yes |
| `Manual` | 3 | `Manual` | 3 | Yes |
| `Disabled` | 4 | `Disabled` | 4 | Yes |
| — | — | `AutomaticDelayedStart` | 10 | Partial |

Values 2/3/4 are index-matched. `Boot` and `System` are Windows-only (kernel/driver services). `AutomaticDelayedStart` exists on Linux but not in `ServiceStartMode`.

---

## 6. Methods — all missing on LinuxServiceInfo

| Method | Signature | Impact |
|---|---|---|
| `Start()` | `void Start()` | `$svc.Start()` → MethodNotFoundException |
| `Start(string[])` | `void Start(string[] args)` | Same |
| `Stop()` | `void Stop()` | `$svc.Stop()` → MethodNotFoundException |
| `Stop(bool)` | `void Stop(bool stopDependentServices)` | Same |
| `Pause()` | `void Pause()` | Same |
| `Continue()` | `void Continue()` | Same |
| `Refresh()` | `void Refresh()` | `$svc.Refresh()` → MethodNotFoundException |
| `Close()` | `void Close()` | Same |
| `Dispose()` | `void Dispose()` | `$svc.Dispose()` → MethodNotFoundException |
| `ExecuteCommand(int)` | `void ExecuteCommand(int command)` | Same |
| `WaitForStatus(...)` | `void WaitForStatus(ServiceControllerStatus, TimeSpan)` | Same |
| `GetServices()` (static) | `ServiceController[] GetServices()` | Works but returns Windows services only |
| `GetDevices()` (static) | `ServiceController[] GetDevices()` | Same |

**Impact:** The most common cross-platform breakage pattern:

```powershell
$s = Get-Service sshd
$s.Stop()      # Windows: stops the service. Linux: MethodNotFoundException
$s.Refresh()   # Windows: re-reads state. Linux: MethodNotFoundException
```

---

## 7. Concrete breakage scenarios

### Breaking (scripts that will error)

```powershell
# 1. Type check — returns $false on Linux
$s = Get-Service sshd
$s -is [System.ServiceProcess.ServiceController]

# 2. Instance method calls — MethodNotFoundException
$s.Refresh()
$s.Start()
$s.Stop()
$s.Dispose()

# 3. Status enum comparison — FAILS on this repo (string vs enum), works on fork
$s.Status -eq [ServiceControllerStatus]::Running

# 4. Missing properties — PropertyNotFoundException
$s.ServiceName          # use $s.Name
$s.MachineName
$s.CanStop
$s.DependentServices
$s.ServicesDependedOn
$s.BinaryPathName       # ETS property
$s.Description          # ETS property
$s.StartupType          # ETS — use $s.StartType

# 5. Pipeline type filtering — returns nothing on Linux
Get-Service | Where-Object { $_ -is [System.ServiceProcess.ServiceController] }
```

### Working (cross-platform compatible patterns)

```powershell
# 6. String comparison on Status — works everywhere
$s.Status -eq 'Running'

# 7. StartType comparison — works (numeric values match)
$s.StartType -eq 'Automatic'

# 8. Name-based filtering — works
Get-Service -Name 'ssh*'

# 9. Format-Table default display — works
Get-Service | Format-Table Status, Name, DisplayName
```

---

## 8. Severity summary

| # | Incompatibility | Severity | This repo | Fork |
|---|---|---|---|---|
| 1 | `Status` is `string` instead of `ServiceControllerStatus` | **Breaking** | Yes | No (fixed) |
| 2 | No inheritance from `Component` — `-is [ServiceController]` fails | **Breaking** | Yes | Yes |
| 3 | No instance methods (`Start`, `Stop`, `Refresh`, `Dispose`, etc.) | **Breaking** | Yes | Yes |
| 4 | Missing `ServiceName` property (only `Name`) | **Breaking** | Yes | Yes |
| 5 | Missing ETS properties (`BinaryPathName`, `Description`, `StartupType`, `UserName`, `DelayedAutoStart`) | **Breaking** | Yes | Yes |
| 6 | Missing `DependentServices` / `ServicesDependedOn` | **Degrading** | Yes | Yes |
| 7 | Missing `CanPauseAndContinue`, `CanShutdown`, `CanStop` | **Degrading** | Yes | Yes |
| 8 | Missing `MachineName` property | **Degrading** | Yes | Yes |
| 9 | Missing `ServiceType`, `ServiceHandle` | **Degrading** | Yes | Yes |
| 10 | `ServiceStartupType` lacks `Boot` and `System` values | **Degrading** | Yes | Yes |
| 11 | No `Paused` / `PausePending` status values | **Degrading** | Yes | Yes |
| 12 | No `.ps1xml` format definition for `LinuxServiceInfo` | **Cosmetic** | Yes | Yes |
| 13 | Extra `ActiveState` / `SubState` properties (Linux-only) | **Cosmetic** | Yes | Yes |
| 14 | No `Disposed` event / `IDisposable` | **Cosmetic** | Yes | Yes |

---

## 9. Mitigation options

### Option A: Accept incompatibility (current approach)

Document the gaps. Cross-platform scripts must avoid:
- Type checks against `ServiceController`
- Instance method calls (`$svc.Start()`, `$svc.Stop()`, `$svc.Refresh()`)
- ETS property access (`$svc.Description`, `$svc.BinaryPathName`)
- Enum-typed `Status` comparison

### Option B: Add ETS type extensions

Create a `.ps1xml` type extension file that:
- Adds `ServiceName` as an alias for `Name`
- Adds `StartupType` as an alias for `StartType`
- Adds `BinaryPathName`, `Description`, `UserName` as script properties (resolved via `systemctl show`)
- Adds `DependentServices`, `ServicesDependedOn` as script properties
- Adds `CanStop`, `CanShutdown`, `CanPauseAndContinue` as script properties

This would make `$svc.ServiceName`, `$svc.Description`, etc. work without changing the C# type.

### Option C: Make Status use ServiceControllerStatus (fork approach)

The fork already does this. Requires `#if UNIX` guards to suppress CA1416 warnings.

### Option D: Full inheritance (not feasible)

`ServiceController` is sealed to Windows internals (SC_HANDLE, Win32 API calls). Making `LinuxServiceInfo` extend it is not possible without a complete rewrite of the base class.

---

## 10. Open questions

1. Should `Status` be changed from `string` to `ServiceControllerStatus` in this repo? (Fork already does this.)
2. Should ETS type extensions be added to bridge the property gap?
3. Should the upstream PR accept the incompatibility as a known limitation, or attempt to close the gap before submission?
4. Should `LinuxServiceInfo` implement `IDisposable` (no-op) to satisfy `using` patterns?

---

*This document is a living analysis. Update it as the type definition evolves.*
