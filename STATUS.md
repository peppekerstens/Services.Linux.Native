# Services.Linux.Native — Module Status

**Last updated:** 2026-05-16
**Version:** 0.1.2
**GHA Build:** ✅ green
**GHA Pester:** ✅ green (5-distro + Windows)

---

## Current State

7 cmdlets + 2 stubs implemented via D-Bus (`Tmds.DBus.Protocol`). Start/Stop/Restart translate polkit errors. Set/New/Remove do not (issue #29).

### Output Types

| Type | Inherits | Windows Counterpart | Rule 9 Status |
|---|---|---|---|
| `LinuxServiceInfo` | `object` | `ServiceController : Component` | ⬜ Non-compliant |

### Rule 9 Compliance Gaps

**Critical:**
- Does not inherit `Component` (Windows: `ServiceController : Component`)
- `Name` instead of `ServiceName`
- `Status` is `string` (fork: `ServiceControllerStatus` enum)
- Missing methods: `Start()`, `Stop()`, `Refresh()`, `Dispose()`, `WaitForStatus()`
- Missing properties: `MachineName`, `CanStop`, `CanPauseAndContinue`, `CanShutdown`, `DependentServices`, `ServicesDependedOn`, `ServiceType`

**Design proposal:** Replace `LinuxServiceInfo` with `LinuxServiceController : Component` (~570 lines C#). Full analysis: `docs/linuxserviceinfo-vs-servicecontroller-20260516.md`

---

## Known Issues

| Issue | Severity | Status |
|---|---|---|
| #29 — `Set/New/Remove-Service` leak raw D-Bus `InteractiveAuthorizationRequired` errors | MUST | ⬜ Open |

## Next Steps

1. Fix issue #29 — add polkit error translation to `EnableUnits`/`DisableUnits` and `DaemonReload`
2. Build `LinuxServiceController : Component` (replaces `LinuxServiceInfo`)
3. Update all cmdlets to use new type
4. Add Pester tests for new properties and methods
5. Trigger GHA pester workflow — verify all green

---

## Reference

| Resource | Location |
|---|---|
| Source code | `src/Services.Linux.Native/` |
| Tests | `tests/Services.Linux.Native.Tests/` |
| Type analysis | `docs/linuxserviceinfo-vs-servicecontroller-20260516.md` |
| Linux rules | `docs/linux-rules.md` |
| Coordination repo | `https://github.com/peppekerstens/opencode` |
