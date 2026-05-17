# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.3] — 2026-05-17

### Added
- `LinuxServiceController : Component` replaces `LinuxServiceInfo` (Rule 9)
- `Name` property — returns short name without `.service` suffix
- `ServiceType` enum with Linux-native values (1000+ range)
- `ServiceControllerStatus` enum matching Windows values
- `ErrorMessages` class for centralized error message templates (Rule 11)
- `OperatingSystem.IsWindows()` guard on all cmdlets (Rule 8)
- `HelpUri` and `RemotingCapability` on all `[Cmdlet]` attributes (Rule 12)
- `ShouldProcess` display name formatting (Rule 13)
- `[Alias("ServiceName")]` on `Name` parameters (Rule 14)
- Template unit (`@.`) filtering in `Get-Service` (Rule 2)
- XML documentation on `LinuxServiceController`, `ServiceControllerStatus`, `ServiceType` (Rule 18)
- `STATUS.md` and `AGENTS.md` contributor documentation
- `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`
- CODEOWNERS file
- PR validation workflow (`pr-validation.yml`)
- GitHub issue templates (bug report, feature request, code review finding)
- PR template with build/test checklist
- OpenCode configuration (`.opencode/`) for standalone development

### Fixed
- D-Bus polkit error translation for `Set/New/Remove-Service` (Issue #29)
- Error ID changed to `UnauthorizedAccess`, category to `SecurityError` (Rule 6)
- Bare `catch (Exception)` blocks removed — replaced with `WriteWarning` or justified comments (Rule 7)
- Copyright headers updated to `peppekerstens` (Rule 10)
- `ServiceType` returns Linux-native values instead of Windows enum values (Rule 31)
- `DaemonReload` polkit error translation
- `EnableUnits`/`DisableUnits` polkit error translation

### Changed
- `SupportsShouldProcess` removed from stub cmdlets
- `LinuxServiceInfo` → `LinuxServiceController` type rename
- Removed redundant `UnitType` property and `LinuxServiceType` enum
- All 22 linux-rules.md applied and verified

## [0.1.2] — 2026-05-15

### Fixed
- Copyright headers added to all source files
- `SupportsShouldProcess` removed from `SuspendServiceCommand` and `ResumeServiceCommand` stubs
- Template units (`@.`) excluded from `Get-Service` output
- D-Bus polkit errors translated to `"root privileges are required."` for Start/Stop/Restart
- Elevation error tests added for all write cmdlets
- Windows pester skip added

## [0.1.1] — 2026-05-14

### Fixed
- `ConfigureAwait(false)` on all async D-Bus calls
- Shared D-Bus connection for compound cmdlets (`Set-Service`, `New-Service`, `Remove-Service`)
- `IsNonRoot()` + `GetUserUnitDir()` for non-root unit file paths
- Removed dead `LU_*` constants

## [0.1.0] — 2026-05-13

### Added
- Initial release
- 7 full cmdlets (`Get/Start/Stop/Restart/Set/New/Remove-Service`) + 2 stubs (`Suspend/Resume-Service`)
- D-Bus via `Tmds.DBus.Protocol` 0.93.0
- 20 Pester tests
- `dotnet publish` required for dependency resolution
