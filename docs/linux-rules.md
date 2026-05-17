# Linux Development Rules

Derived from manual testing findings across all Stage 6 C# binary modules.
Applies to every Linux module — PowerShell and C#.

---

## Foundational Principle: Native .NET APIs First

**HARD REQUIREMENT:** Always prefer native .NET APIs and direct system calls
over spawning subprocesses. Let .NET handle the heavy lifting.

- Use `Tmds.DBus.Protocol` for systemd communication — not `systemctl` subprocess
- Use `System.IO`, `System.Net`, `System.Text.RegularExpressions` — not `grep`, `awk`, `sed`
- Use `LibraryImport` P/Invoke for libc calls — not shell wrappers
- Subprocess (`Process.Start`) is a **last resort** only when no .NET API exists
- This applies to ALL Linux modules, not just Services

**Rationale:** Subprocess calls add overhead, string parsing complexity, culture
sensitivity issues, and deviate from the Windows model. Native APIs are faster,
type-safe, and maintainable. Windows cmdlets call Win32 APIs directly — Linux
cmdlets must do the same with native Linux APIs.

---

## Rule 1: Elevation checks are mandatory for write operations

Every cmdlet that changes system state must check elevation before acting.
Read-only cmdlets must not check elevation.

**Windows precedent:** `#requires -RunAsAdministrator` for scripts.
Windows binary cmdlets let Win32 APIs fail and catch `SecurityException`.

**Linux reality:** There is no equivalent engine-level enforcement for binary
cmdlets. The check must be explicit in the cmdlet code.

**Implementation:**

```csharp
// In every write-operation cmdlet's OperateOnService() or equivalent:
if (!Utils.IsAdministrator())
{
    throw new PSSecurityException(
        $"{MyInvocation.MyCommand.Name} requires root privileges.");
}
```

`Utils.IsAdministrator()` is the PowerShell engine utility. After the fork
fix (commit `70f96ff`), it checks effective UID on Linux instead of always
returning `true`.

**Error type:** `PSSecurityException` — the same exception class Windows
PowerShell uses for elevation failures. Error ID: `UnauthorizedAccess`.
Error category: `SecurityError`.

**Message format:**
- Linux: `"{CmdletName} requires root privileges."`
- Windows: `"{CmdletName} requires an elevated session."`

---

## Rule 2: Template units are excluded from enumeration

systemd template units (names containing `@.`) cannot be directly started,
stopped, or restarted. They require an instance suffix.

**Finding:** `Get-Service` returned `ppp@.service` from `ListUnitFiles`.
Piping it to `Start-Service` produced:
```
org.freedesktop.DBus.Error.InvalidArgs: Unit name ppp@.service is missing the instance name.
```

**Fix:** Filter template units at the enumeration layer:
```csharp
if (file.EndsWith(".service", StringComparison.OrdinalIgnoreCase)
    && !file.Contains("@."))
```

This applies to any module that enumerates systemd units.

---

## Rule 3: Use native .NET APIs and direct system calls — no subprocess fallback

**HARD REQUIREMENT:** Always prefer native .NET APIs and direct system calls
over spawning subprocesses. Let .NET handle the heavy lifting.

**Finding (anti-pattern):** Early versions of this module considered using
`systemctl` subprocess for write operations because D-Bus polkit errors lack
an interactive agent. This was rejected.

**Windows precedent:** `ServiceController` calls `advapi32.dll` directly.
It does not spawn `sc.exe`. Linux modules must follow the same pattern.

**Correct approach:**
```csharp
// Use Tmds.DBus.Protocol to call systemd directly
// Catch polkit errors reactively and translate to user-friendly messages
try
{
    await conn.CallMethodAsync(msg).ConfigureAwait(false);
}
catch (DBusExceptionBase ex) when (ex.Message.Contains("InteractiveAuthorizationRequired"))
{
    throw new InvalidOperationException(
        $"{operation} failed: root privileges are required. Use 'sudo pwsh'.", ex);
}
```

**Subprocess is a last resort** — only when no .NET API or native library
exists for the task. Even then, prefer P/Invoke (`LibraryImport`) over
`Process.Start`.

**Rationale:** Subprocess calls add overhead, string parsing complexity,
culture sensitivity issues, and deviate from the Windows model. Native
APIs are faster, type-safe, and maintainable.

---

## Rule 4: Process invocation follows C# conventions

All subprocess calls must follow the Stage 6 C# conventions:

1. `ProcessStartInfo.ArgumentList` — never manual string joining
2. `ReadToEndAsync()` on both stdout and stderr — prevents deadlock
3. Exit code checked on every subprocess — non-zero throws or writes error
4. `ConfigureAwait(false)` on all async calls
5. All string formatting must specify `CultureInfo.InvariantCulture` — never rely on current culture for error messages or machine-readable output.

---

## Rule 5: `#requires -RunAsElevated` for PowerShell scripts

Scripts that call write-operation cmdlets should declare elevation upfront.

**After fork fix (commit `70f96ff`):**
- `#requires -RunAsAdministrator` works on Linux (checks effective UID)
- `#requires -RunAsElevated` is a platform-neutral alias

**Usage:**
```powershell
#requires -RunAsElevated

function Invoke-DiskOperation {
    [CmdletBinding(SupportsShouldProcess)]
    param(...)
    # ...
}
```

The engine throws `ScriptRequiresException` before the script executes if
the user is not elevated. Error message is platform-specific:
- Linux: *"Start PowerShell using 'sudo pwsh'"*
- Windows: *"Run as Administrator"*

---

## Rule 6: Consistent error handling across all modules

Every module uses the same error pattern:

| Scenario | Error type | Error ID | Category |
|---|---|---|---|
| Not elevated | `PSSecurityException` | `UnauthorizedAccess` | `SecurityError` |
| D-Bus failure | `InvalidOperationException` | `DBusOperationFailed` | `OperationStopped` |
| Subprocess failed | `InvalidOperationException` | `SubprocessFailed` | `OperationStopped` |
| Invalid input | `ArgumentException` | `InvalidArgument` | `InvalidArgument` |
| Unit not found | `ItemNotFoundException` | `UnitNotFound` | `ObjectNotFound` |

Error messages must not be hardcoded string literals in cmdlet code. Use `const`
string fields in a dedicated static class (standalone repos) or `.resx` resource
files (upstream). Interpolate values using `string.Format(CultureInfo.InvariantCulture, ...)`
or `StringUtil.Format()`.

---

## Rule 7: No silent error swallowing

Every `catch` block must do one of:
- `throw` (re-throw)
- `WriteError()` with a constructed `ErrorRecord`
- `ThrowTerminatingError()` with a constructed `ErrorRecord`

Bare `catch { }` is forbidden. `catch (Exception)` requires a comment
justifying why it catches all exceptions.

---

## Rule 8: Platform branching at the top of `ProcessRecord()`

C# cmdlets that have Windows counterparts must branch immediately:

```csharp
protected override void ProcessRecord()
{
    if (OperatingSystem.IsWindows())
    {
        // Delegate to Windows built-in
        InvokeCommand.InvokeScript("Microsoft.PowerShell.Management\\Get-Service");
        return;
    }
    // Linux implementation
}
```

This prevents infinite recursion when the module name matches a Windows
built-in cmdlet.

**Upstream note:** For code compiled into the same binary, prefer `#if UNIX` /
`#if !UNIX` preprocessor guards at the file or class level. Runtime checks
(`OperatingSystem.IsWindows()`) are only needed for macOS vs Linux distinctions
within the UNIX build.

---

## Rule 9: Cross-platform type alignment is mandatory

When a Linux cmdlet returns objects that have a Windows counterpart, the
Linux type MUST be designed to maximize compatibility with the Windows type.

**Priority order (apply in sequence):**

1. **Inherit from a common cross-platform base class** — If the Windows type
   inherits from a cross-platform class (e.g. `System.ComponentModel.Component`),
   the Linux type MUST inherit from the same base. This enables shared type
   checks (`-is [Component]`), `IDisposable` semantics, and designer support.

2. **Match property names and types** — Use the same property names as the
   Windows type. If the Windows type uses `ServiceName`, the Linux type must
   also use `ServiceName` (not `Name`). If the Windows type uses an enum,
   the Linux type must use the same enum (with `#if UNIX` guards if needed
   to suppress CA1416 warnings).

3. **Match method signatures** — Implement the same instance methods with
   identical signatures. Methods that cannot be implemented on Linux must
   throw `PlatformNotSupportedException` with a clear message explaining
   why (e.g. "systemd has no pause/resume concept").

4. **Add Linux-specific extensions only after alignment** — Linux-specific
   properties (e.g. `ActiveState`, `SubState` for systemd) may be added
   AFTER all Windows-compatible properties are present. They must be clearly
   documented as Linux-only.

5. **Split the class tree only as a last resort** — If the Windows type is
   fundamentally incompatible (e.g. `ServiceController` wraps Windows-only
   SC_HANDLE), create a parallel Linux type that inherits from the same
   cross-platform base class (`Component`) and implements the same public
   API surface. The Linux type lives in the same namespace
   (`Microsoft.PowerShell.Commands`) to avoid naming conflicts.

**Rationale:** Cross-platform scripts should work on both Windows and Linux
without modification. Type mismatches (`$svc -is [ServiceController]` returning
`$false` on Linux) silently break pipeline filters and type checks.

**Example — correct approach:**
```csharp
// Windows: System.ServiceProcess.ServiceController : Component
// Linux:   Microsoft.PowerShell.Commands.LinuxServiceController : Component
// Both inherit from Component. Both expose Status, ServiceName, StartType,
// Start(), Stop(), Refresh(), etc.
```

**Example — incorrect approach:**
```csharp
// Linux type extends object, uses different property names, no methods
public sealed class LinuxServiceInfo { public string Name { get; set; } }
// Scripts that access $svc.ServiceName or call $svc.Stop() break.
```

---

## Rule 10: Copyright and license headers are mandatory on every source file

**Upstream precedent:** `.editorconfig` enforces `file_header_template` on every
`.cs` file. CONTRIBUTING.md explicitly requires this.

**Requirement:** Every `.cs` file must start with:
```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
```

For standalone repos outside the PowerShell tree, use the repo's own copyright:
```csharp
// Copyright (c) peppekerstens.
// Licensed under the MIT License.
```

---

## Rule 11: Error messages must use resource strings, not hardcoded literals

**Upstream precedent:** `Service.cs` uses `ServiceResources.CouldNotStartService`,
`ServiceResources.NoServiceFoundForGivenName`, etc. from `.resx` files.
`WriteNonTerminatingError()` takes a resource key, not a literal string.

**Standalone repo minimum:** Use `const` string fields in a dedicated static class:
```csharp
internal static class ErrorMessages
{
    internal const string ElevationRequired = "{0} requires root privileges. Use 'sudo pwsh'.";
    internal const string SubprocessFailed = "{0} failed: {1} (exit code {2})";
}
```

Interpolate using `string.Format(CultureInfo.InvariantCulture, ...)`.

---

## Rule 12: Every cmdlet must declare HelpUri and RemotingCapability

**Upstream precedent:** Every cmdlet attribute in `Service.cs` includes
`HelpUri = "https://go.microsoft.com/fwlink/?LinkID=..."` and
`RemotingCapability = RemotingCapability.SupportedByCommand`.

**Standalone repo approach — layered documentation:**

**Phase 1 (MVP — current):** Point `HelpUri` to the canonical Microsoft Learn
page for the Windows equivalent cmdlet. Since our cmdlets are designed to match
Windows behavior, the Microsoft docs are authoritative for the shared surface
(parameters, syntax, examples).

```csharp
[Cmdlet(VerbsLifecycle.Start, "Service",
    SupportsShouldProcess = true,
    HelpUri = "https://learn.microsoft.com/powershell/module/microsoft.powershell.management/start-service",
    RemotingCapability = RemotingCapability.SupportedByCommand)]
```

**Phase 2 (future):** A dedicated docs site (GitHub Pages + PlatyPS) that
documents only the **delta** — Linux-specific differences from Windows:
- `ActiveState` / `SubState` properties (systemd-specific)
- `UnitType` enum (`Simple`, `Forking`, `Oneshot`, etc.)
- Elevation behavior (`sudo pwsh` vs "Run as Administrator")
- Template unit filtering (`@.` pattern)
- D-Bus vs Win32 API under the hood

**Rationale:** No duplication of Microsoft-maintained content. Users get
authoritative docs for the shared surface. Our docs stay small and focused
on what actually differs. If Microsoft updates parameter docs, we don't
need to track it.

---

## Rule 13: ShouldProcess must use a formatted display string

**Upstream precedent:** `ShouldProcessServiceOperation(service)` produces
`"sshd (Secure Shell Daemon)"`, not just `"sshd.service"`.

**Requirement:** Resolve the display name and format it:
```csharp
string target = string.IsNullOrEmpty(displayName) || displayName == unitName
    ? unitName
    : $"{unitName} ({displayName})";

if (!ShouldProcess(target, "Start")) return;
```

---

## Rule 14: Parameter attributes must include Position, ValidateNotNullOrEmpty, and Alias

**Upstream precedent:** `GetServiceCommand.Name` parameter:
```csharp
[Parameter(Position = 0, ParameterSetName = "Default",
    ValueFromPipelineByPropertyName = true, ValueFromPipeline = true)]
[ValidateNotNullOrEmpty()]
[Alias("ServiceName")]
public string[] Name { get; set; }
```

**Requirement:** Primary name parameters must have `Position = 0`,
`ValidateNotNullOrEmpty`, and an `Alias` matching the Windows parameter name.

---

## Rule 15: Wildcard filtering must distinguish wildcard vs literal patterns

**Upstream precedent:** `MatchingServicesByServiceName()` uses
`WildcardPattern.ContainsWildcardCharacters(pattern)` to branch.
Literal non-match → error. Wildcard non-match → silent empty.

**Requirement:**
```csharp
if (WildcardPattern.ContainsWildcardCharacters(pattern))
{
    var wp = new WildcardPattern(pattern, WildcardOptions.IgnoreCase);
    if (wp.IsMatch(svc.Name)) return true;
}
else
{
    // Literal match — case-insensitive exact comparison
    if (svc.Name.Equals(pattern, StringComparison.OrdinalIgnoreCase))
        return true;
}
```

---

## Rule 16: Tests must use CI/RequireAdmin tags and $PSDefaultParameterValues skip pattern

**Upstream precedent:** All upstream test files use `BeforeAll`/`AfterAll` to
save/restore `$PSDefaultParameterValues`. Tags are `"CI"` for always-run,
`"RequireAdminOnWindows"` for elevated.

**Requirement:**
```powershell
Describe "Get-Service cmdlet tests" -Tags "CI" {
    BeforeAll {
        $originalDefaultParameterValues = $PSDefaultParameterValues.Clone()
        if (-not $IsLinux) {
            $PSDefaultParameterValues["it:skip"] = $true
        }
    }
    AfterAll {
        $global:PSDefaultParameterValues = $originalDefaultParameterValues
    }
}
```

---

## Rule 17: Avoid LINQ and params arrays in performance-sensitive code

**Upstream precedent:** `coding-guidelines.md`: "Avoid LINQ — it can create
lots of avoidable garbage." Upstream uses `for`/`foreach` exclusively.

**Requirement:** Use loops instead of LINQ in enumeration code paths:
```csharp
// BAD:
var result = services.Where(s => s.Name.StartsWith("s")).ToArray();

// GOOD:
var result = new List<LinuxServiceInfo>();
foreach (var svc in services)
{
    if (svc.Name.StartsWith("s", StringComparison.OrdinalIgnoreCase))
        result.Add(svc);
}
```

---

## Rule 18: Public output types must have XML documentation comments

**Upstream precedent:** `coding-guidelines.md`: "Publicly visible types and
their members must be documented." `GenerateDocumentationFile = true` in build.

**Requirement:** Every public class and property must have `<summary>` XML docs:
```csharp
/// <summary>
/// Represents a systemd service unit.
/// </summary>
public sealed class LinuxServiceInfo
{
    /// <summary>
    /// The systemd unit name, e.g. <c>sshd.service</c>.
    /// </summary>
    public string Name { get; internal set; } = string.Empty;
}
```

---

## Rule 19: Field naming — s_ for static, _ for instance

**Upstream precedent:** `.editorconfig` enforces naming via `dotnet_naming_rule`.

**Requirement:**
```csharp
// Correct:
private readonly List<string> _names = new();
private static readonly char[] s_colonSeparator = new[] { ':' };

// Incorrect:
private readonly List<string> names = new();
private static char[] colonSeparator = new[] { ':' };
```

---

## Rule 20: Breaking changes to public types require RFC process

**Upstream precedent:** `breaking-change-contract.md` defines Bucket 1 (Public
Contract) changes requiring team approval.

**Requirement:** Design types correctly the first time. Adding properties is
acceptable. Renaming/removing public members after initial merge requires an RFC.

---

## Rule 21: Shared logic must use base classes, not copy-paste

**Upstream precedent:** `ServiceBaseCommand` → `MultipleServiceCommandBase` →
`ServiceOperationBaseCommand` → concrete cmdlets.

**Requirement:** Cmdlets that share parameters or logic must inherit from a
common abstract base class:
```csharp
public abstract class ServiceUnixBase : Pscmdlet
{
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Name",
        ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string[]? Name { get; set; }

    protected abstract void OperateOnService(string unitName);
}

public sealed class StartServiceCommand : ServiceUnixBase
{
    protected override void OperateOnService(string unitName) { ... }
}
```

---

## Rule 22: Nullable annotations must be consistent across all files

**Upstream precedent:** `ServiceUnix.cs` uses `#nullable enable` at file level.
Upstream is gradually migrating to nullable reference types.

**Requirement:** Pick one strategy and apply consistently:
```csharp
// File-level nullable enable (preferred for new code):
#nullable enable

namespace Microsoft.PowerShell.Commands
{
    public sealed class GetServiceCommand : Pscmdlet
    {
        public string[]? Name { get; set; }  // Explicitly nullable
    }
}
```

---

## Manual Testing Findings Log

### 2026-05-16 — Services.Linux.Native elevation failures

| Command | Error | Root Cause |
|---|---|---|
| `get-service \| where status -eq 'running' \| where name -eq 'cups-browsed.service' \| stop-service` | `InteractiveAuthorizationRequired` | D-Bus polkit-protected, no agent |
| `get-service \| where status -eq 'stopped' \| where name -like 'gnome-remote-desktop-configuration.service' \| start-service` | `InteractiveAuthorizationRequired` | D-Bus polkit-protected, no agent |
| `get-service \| where status -eq 'stopped' \| where name -like 'rsync.service' \| start-service` | `InteractiveAuthorizationRequired` | D-Bus polkit-protected, no agent |
| `get-service \| where status -eq 'stopped' \| where name -like 'ppp*' \| start-service` | `InvalidArgs: Unit name ppp@.service is missing the instance name` | Template unit returned by `ListUnitFiles` |

### 2026-05-16 — ScheduledTasks.Linux.Native pester failure

| Test | Error | Root Cause |
|---|---|---|
| "Set-ScheduledTask writes an error record" | `NotImplementedException` thrown instead of `ErrorRecord` | Test not updated after issue #28 fix |

### 2026-05-16 — PowerShell fork `Utils.IsAdministrator()`

| Platform | Before | After |
|---|---|---|
| Windows | `WindowsPrincipal.IsInRole(Administrator)` | Unchanged |
| Linux | Always `true` (no-op) | Checks effective UID via `/proc/self/status` |
