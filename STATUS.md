# Services.Linux.Native — Module Status

**Last updated:** 2026-05-17
**Version:** 0.1.3
**GHA Build:** ✅ green
**GHA Pester:** ✅ green (5-distro + Windows)

---

## Current State

7 cmdlets + 2 stubs implemented via D-Bus (`Tmds.DBus.Protocol`). All write cmdlets translate polkit errors to `"root privileges are required."`

### Cmdlets

| Cmdlet | Type | Status |
|---|---|---|
| `Get-Service` | Read | ✅ Implemented |
| `Start-Service` | Write | ✅ Implemented |
| `Stop-Service` | Write | ✅ Implemented |
| `Restart-Service` | Write | ✅ Implemented |
| `Set-Service` | Write | ✅ Implemented |
| `New-Service` | Write | ✅ Implemented |
| `Remove-Service` | Write | ✅ Implemented |
| `Suspend-Service` | Stub | ⚠️ `NotImplementedException` |
| `Resume-Service` | Stub | ⚠️ `NotImplementedException` |

### Output Types

| Type | Inherits | Windows Counterpart | Rule 9 Status |
|---|---|---|---|
| `LinuxServiceController` | `Component` | `ServiceController : Component` | ✅ Compliant |
| `ServiceStartupType` | `enum` | `ServiceStartMode` | ✅ Compliant (compatible values) |
| `ServiceType` | `enum` | `ServiceType` | ✅ Compliant (Linux-native 1000+ range) |
| `ServiceControllerStatus` | `enum` | `ServiceControllerStatus` | ✅ Compliant (matching values) |

### Rule 9 Compliance

**Fixed (2026-05-16):**
- `LinuxServiceController` inherits `System.ComponentModel.Component` (matches Windows `ServiceController : Component`)
- Added `Name` property — returns short name without `.service` suffix (enables `where Name -like 'cron*'`)
- `ServiceType` enum replaced with Linux-native values (`Simple`=1001, `Forking`=1002, etc.)
- Added `ServiceControllerStatus` enum matching Windows values exactly
- Removed redundant `UnitType` property and `LinuxServiceType` enum

### Elevation Handling

All write cmdlets translate D-Bus polkit `InteractiveAuthorizationRequired` errors to:
`"root privileges are required. Use 'sudo pwsh'."` (Error ID: `UnauthorizedAccess`, Category: `SecurityError`)

---

## Open Issues

| Issue | Severity | Description |
|---|---|---|
| #32 | MUST | No proactive elevation check — relies on reactive catch-and-translate |
| #34 | MUST | `RunSystemctl()` uses sync `ReadToEnd()`, manual arg splitting, no `CultureInfo` |
| #41 | SHOULD | `ShouldProcess` uses raw `unitName` without display name formatting |
| #42 | SHOULD | `Name` param missing `[Alias("ServiceName")]` on 4 cmdlets |
| #43 | SHOULD | Tests lack `-Tags "CI"` and `$PSDefaultParameterValues` skip pattern |
| #44 | SHOULD | LINQ in `DependentServices` and `ServicesDependedOn` getters |
| #45 | SHOULD | 9 cmdlet classes + `ServiceUnixBase` + enums lack XML docs |
| #46 | SHOULD | `ListUnits` parser does not filter `@.` template units |

---

## Resolved Issues

| Issue | Commit | Description |
|---|---|---|
| #29 | `1ba16a8` | D-Bus polkit error translation for Set/New/Remove-Service |
| #30 | `d98ba01` | Added `Name` property (short name without `.service` suffix) |
| #31 | `d98ba01` | `ServiceType` enum replaced with Linux-native values |
| #35 | `219db42` | Error ID changed to `UnauthorizedAccess`, category to `SecurityError` |
| #36 | `35d1fcc` | Bare `catch (Exception)` blocks removed |
| #37 | `da83f34` | `OperatingSystem.IsWindows()` guard added to all cmdlets |
| #38 | `20add13` | Copyright headers updated to `peppekerstens` |
| #39 | `d2243b7` | Error messages centralized into `ErrorMessages` class |
| #40 | `d60e170` | HelpUri points to Microsoft Learn pages (Phase 1) |

---

## Next Steps

1. Fix MUST issues (#32, #34) — proactive elevation check, async `RunSystemctl()`
2. Fix SHOULD issues (#41-46) — display formatting, aliases, test tags, LINQ removal, XML docs, template filtering
3. Replace `LinuxServiceInfo` in PowerShell fork's `ServiceUnix.cs` with `LinuxServiceController`
4. Sign Microsoft CLA, file RFC, submit upstream PR

---

## Reference

| Resource | Location |
|---|---|
| Source code | `src/Services.Linux.Native/` |
| Tests | `tests/Services.Linux.Native.Tests/` |
| Type analysis | `docs/linuxserviceinfo-vs-servicecontroller-20260516.md` |
| Linux rules | `docs/linux-rules.md` |
| Coordination repo | https://github.com/peppekerstens/opencode |
| Open issues | https://github.com/peppekerstens/Services.Linux.Native/issues |
