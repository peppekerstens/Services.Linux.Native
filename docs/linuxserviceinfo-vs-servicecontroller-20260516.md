# LinuxServiceInfo vs ServiceController — Compatibility Analysis & Design Proposal

**Date:** 2026-05-16
**Status:** Design proposal — pending approval
**Context:** Services.Linux.Native returns `LinuxServiceInfo` from `Get-Service`. Windows `Get-Service` returns `System.ServiceProcess.ServiceController`. This document analyzes every incompatibility and proposes a path to cross-platform type alignment.

---

## 1. Current State Analysis

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
    public ServiceControllerStatus Status { get; internal set; }
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

## 2. Inheritance Incompatibility

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

## 3. Research Findings — Component Inheritance Feasibility

`System.ComponentModel.Component` is **cross-platform** in .NET 8. It lives in `System.ComponentModel.Primitives` and has no platform-specific dependencies. Its source is shared across all TFMs.

`System.ServiceProcess.ServiceController` is **Windows-only**. It is annotated with `[SupportedOSPlatform("windows")]` and internally wraps SC_HANDLE via Win32 P/Invoke (`advapi32.dll`). It cannot be subclassed on Linux.

**Conclusion:** `Component` is a viable shared base. `ServiceController` is not. A `LinuxServiceController : Component` can be created in the same namespace (`Microsoft.PowerShell.Commands`) without any platform guard on the base class itself.

---

## 4. Class Tree Divergence Point

```
                    System.Object
                         │
              System.MarshalByRefObject
                         │
              System.ComponentModel.Component    ← SHARED (cross-platform)
                    ╱            ╲
                   ╱              ╲
    ServiceController              LinuxServiceController (proposed)
    (Windows-only)                 (Linux, same namespace)
    [SupportedOSPlatform("windows")]
    Wraps SC_HANDLE                Wraps systemctl/D-Bus
```

The divergence is at `Component`. Both types inherit from it. Both expose the same public API surface. Scripts that type-check against `Component` work on both platforms. Scripts that type-check against `ServiceController` still break — but that is unavoidable without rewriting the Windows type.

---

## 5. Design Proposal — `LinuxServiceController : Component`

### Property/Method Mapping Table

| ServiceController Member | Type | Linux Mapping | Implementation |
|---|---|---|---|
| `ServiceName` | `string` | **Direct** — rename `Name` → `ServiceName` | `systemctl list-units` |
| `DisplayName` | `string` | **Direct** — already present | `systemctl show -p Description` |
| `Status` | `ServiceControllerStatus` | **Direct** — use enum (fork approach) | `systemctl show -p ActiveState` |
| `StartType` | `ServiceStartMode` | **Direct** — use Windows enum | `systemctl show -p UnitFileState` |
| `MachineName` | `string` | **Synthetic** — always `"."` (localhost) | Constant |
| `CanPauseAndContinue` | `bool` | **Not supported** — throw `PlatformNotSupportedException` | systemd has no pause |
| `CanShutdown` | `bool` | **Not supported** — throw `PlatformNotSupportedException` | systemd has no shutdown signal |
| `CanStop` | `bool` | **Direct** — `true` if `ActiveState != "inactive"` | `systemctl show -p ActiveState` |
| `CanPauseAndContinue` | `bool` | **Synthetic** — always `false` | No D-Bus call needed |
| `CanShutdown` | `bool` | **Synthetic** — always `false` | No D-Bus call needed |
| `DependentServices` | `ServiceController[]` | **Synthetic** — query `WantedBy`/`RequiredBy` | `systemctl list-dependencies --reverse` |
| `ServicesDependedOn` | `ServiceController[]` | **Synthetic** — query `Wants`/`Requires` | `systemctl list-dependencies` |
| `ServiceType` | `ServiceType` | **Synthetic** — map from `Type` property | `systemctl show -p Type` |
| `ServiceHandle` | `SafeHandle` | **Not supported** — throw `PlatformNotSupportedException` | No SC_HANDLE on Linux |
| `Container` | `IContainer` | **Inherited** from `Component` | Base class |
| `Site` | `ISite` | **Inherited** from `Component` | Base class |
| `Start()` | `void` | **Direct** — `systemctl start` | subprocess |
| `Start(string[])` | `void` | **Direct** — `systemctl start` with args | subprocess |
| `Stop()` | `void` | **Direct** — `systemctl stop` | subprocess |
| `Stop(bool)` | `void` | **Direct** — `systemctl stop` (ignore dep flag) | subprocess |
| `Pause()` | `void` | **Not supported** — throw `PlatformNotSupportedException` | No pause concept |
| `Continue()` | `void` | **Not supported** — throw `PlatformNotSupportedException` | No pause concept |
| `Refresh()` | `void` | **Direct** — re-query D-Bus/systemctl | D-Bus or subprocess |
| `Close()` | `void` | **No-op** — no handle to close | — |
| `Dispose()` | `void` | **Direct** — inherited from `Component` | Base class |
| `ExecuteCommand(int)` | `void` | **Not supported** — throw `PlatformNotSupportedException` | No equivalent |
| `WaitForStatus(...)` | `void` | **Synthetic** — poll `ActiveState` with timeout | subprocess loop |
| `ToString()` | `string` | **Direct** — return `ServiceName` | Override |

### ETS NoteProperties (bridged via native properties)

| ETS Property | Linux Mapping | Implementation |
|---|---|---|
| `BinaryPathName` | **Direct** — `ExecStart` path | `systemctl show -p ExecStart` |
| `Description` | **Direct** — already present as `DisplayName` | `systemctl show -p Description` |
| `StartupType` | **Alias** — map to `StartType` | Same property, different name |
| `UserName` | **Synthetic** — `systemctl show -p User` | `systemctl show -p User` |
| `DelayedAutoStart` | **Synthetic** — check `TimeoutStartUSec` default | `systemctl show -p TimeoutStartUSec` |

---

## 6. D-Bus Capability Mapping

| Property/Method | D-Bus Interface | Method/Property | Subprocess Fallback |
|---|---|---|---|
| `ServiceName` | `org.freedesktop.systemd1.Manager` | `ListUnits()` | `systemctl list-units --no-pager` |
| `DisplayName` | `org.freedesktop.systemd1.Unit` | `Description` (Get) | `systemctl show -p Description` |
| `Status` | `org.freedesktop.systemd1.Unit` | `ActiveState` (Get) | `systemctl show -p ActiveState` |
| `StartType` | `org.freedesktop.systemd1.Unit` | `UnitFileState` (Get) | `systemctl show -p UnitFileState` |
| `CanStop` | `org.freedesktop.systemd1.Unit` | `ActiveState` (Get) | `systemctl is-active` |
| `DependentServices` | `org.freedesktop.systemd1.Manager` | `ListUnitsByPatterns()` | `systemctl list-dependencies --reverse` |
| `ServicesDependedOn` | `org.freedesktop.systemd1.Unit` | `Wants`/`Requires` (Get) | `systemctl list-dependencies` |
| `BinaryPathName` | `org.freedesktop.systemd1.Unit` | `ExecStart` (Get) | `systemctl show -p ExecStart` |
| `UserName` | `org.freedesktop.systemd1.Unit` | `User` (Get) | `systemctl show -p User` |
| `Start()` | `org.freedesktop.systemd1.Manager` | `StartUnit()` | `systemctl start` |
| `Stop()` | `org.freedesktop.systemd1.Manager` | `StopUnit()` | `systemctl stop` |
| `Refresh()` | `org.freedesktop.systemd1.Manager` | `Reload()` | `systemctl daemon-reload` |

**Note:** Write operations (`Start`, `Stop`, `Refresh`) use subprocess fallback per Rule 3 (polkit-protected). Read operations use D-Bus directly.

---

## 7. Implementation Complexity Estimates

| Component | Estimated Lines | D-Bus Calls per Unit | Subprocess Calls |
|---|---|---|---|
| `LinuxServiceController` class | ~350 | 0 (lazy-loaded) | 0 |
| `Get-Service` cmdlet (enumeration) | ~120 | 2 (`ListUnits` + `ListUnitFiles`) | 0 |
| `Start/Stop/Restart` cmdlets | ~80 | 0 | 1 per unit |
| `Set-Service` cmdlet | ~150 | 0 | 2 (enable/disable + daemon-reload) |
| `New/Remove-Service` cmdlets | ~200 | 0 | 2-3 per operation |
| Property getters (lazy) | ~200 | 1 per property (cached) | 1 per property (fallback) |
| **Total (new + refactored)** | **~1100** | **2-3 per enumeration** | **1-3 per write** |

The refactoring replaces the current `LinuxServiceInfo` (~60 lines, no methods) with a full `LinuxServiceController` (~350 lines) plus updated cmdlets. Net increase: ~500 lines of C#.

---

## 8. ETS Type Extension Alternative Analysis

### Option: Bridge gaps with `.ps1xml` type extensions instead of C# refactoring

**Pros:**
- No C# changes required — works with existing `LinuxServiceInfo`
- Quick to implement (XML file + module manifest update)
- Can add script properties that call `systemctl show` on demand
- No recompilation needed for property additions

**Cons:**
- Script properties incur subprocess overhead on every access (`$svc.Description` → `systemctl show`)
- Cannot add instance methods (`Start()`, `Stop()`, `Refresh()`) — ETS only supports properties and script methods (which are awkward)
- Type check `$svc -is [ServiceController]` still fails — ETS cannot change inheritance
- `IDisposable` / `using` patterns not supported
- No compile-time type safety — typos in property names are runtime errors
- Upstream portability: PowerShell engine does not ship module-specific `.ps1xml` files for Linux extensions

**Verdict:** ETS extensions are a stopgap. They bridge property names but cannot add methods or fix inheritance. The C# `LinuxServiceController : Component` approach is the correct long-term solution for upstream contribution.

---

## 9. Upstream Portability Plan

### How `LinuxServiceController` fits into `PowerShell/PowerShell`

1. **File location:** `src/Microsoft.PowerShell.Commands.Management/commands/management/ServiceUnix.cs`
2. **Namespace:** `Microsoft.PowerShell.Commands` (same as Windows `ServiceController`)
3. **Conditional compilation:**
   ```csharp
   #if UNIX
   public sealed class LinuxServiceController : Component
   {
       // ...
   }

   [Cmdlet(VerbsLifecycle.Get, "Service")]
   [OutputType(typeof(LinuxServiceController))]
   public class GetServiceCommand : PSCmdlet
   {
       // returns LinuxServiceController on Linux
   }
   #endif
   ```
4. **No changes to Windows `ServiceController`** — the Windows type remains unchanged in `Service.cs` (`#if !UNIX`).
5. **Shared types:** `ServiceStartupType` enum already exists in the fork. `ServiceControllerStatus` is used with `#pragma warning disable CA1416` guards.
6. **Test strategy:** Pester tests in the fork validate Linux-specific behavior. Windows tests remain unchanged.

### Upstream submission sequence

1. Fix all open issues in standalone repo (issue #29 — D-Bus error translation)
2. Port `LinuxServiceController` into fork branch `feature/service-unix-systemctl`
3. Update fork Pester tests for new type surface
4. Trigger GHA pester workflow — verify 5-distro green
5. Sign Microsoft CLA
6. File RFC at `PowerShell/PowerShell-RFC` describing the type alignment approach
7. Submit upstream PR

---

## 10. Concrete Steps to Move Forward

1. **Approve the design** — Confirm that `LinuxServiceController : Component` is the target type (not ETS extensions, not `LinuxServiceInfo` with alias properties).
2. **Create `LinuxServiceController` class** in `Services.Linux.Native/src/` — implement all properties from the mapping table, inherit from `Component`, use lazy D-Bus loading.
3. **Update `Get-Service` cmdlet** — change `OutputType` to `LinuxServiceController`, return instances of the new type.
4. **Update `Start/Stop/Restart-Service` cmdlets** — accept `LinuxServiceController` as pipeline input, call instance methods (`$svc.Start()`) instead of re-resolving names.
5. **Update `Set/New/Remove-Service` cmdlets** — ensure D-Bus polkit errors are translated per issue #29.
6. **Write Pester tests** — verify type inheritance (`-is [Component]`), property access, method calls, and `-WhatIf` safety.
7. **Trigger GHA pester workflow** — verify 5-distro + Windows green.
8. **Port into fork** — copy `LinuxServiceController` and updated cmdlets into `feature/service-unix-systemctl`.
9. **Update fork tests** — ensure Pester tests pass against the new type.
10. **File RFC and submit upstream PR** — describe the type alignment approach and reference this document.

---

*This document is a living design proposal. Update it as the type definition evolves and as upstream feedback is received.*
