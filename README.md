# Services.Linux.Native

[![Build](https://github.com/peppekerstens/Services.Linux.Native/actions/workflows/build.yml/badge.svg)](https://github.com/peppekerstens/Services.Linux.Native/actions/workflows/build.yml)
[![Pester Tests](https://github.com/peppekerstens/Services.Linux.Native/actions/workflows/pester.yml/badge.svg)](https://github.com/peppekerstens/Services.Linux.Native/actions/workflows/pester.yml)

> Native C# binary module implementing the full 9-cmdlet `*-Service` surface of `Microsoft.PowerShell.Management` for Linux, using D-Bus (`Tmds.DBus.Protocol`) to communicate with systemd.

Part of Stage 6 (Tier 3) of the [PowerShell Linux Commands](https://peppekerstens.github.io) project. This is the standalone binary module that will be ported upstream to `PowerShell/PowerShell` after GHA validation.

---

## What it does

Provides Windows-compatible `*-Service` cmdlets for Linux by talking to systemd over D-Bus (no `systemctl` subprocess for reads). Write operations open one D-Bus connection per command invocation, shared across multiple operations (e.g., `Stop-Service` + `Disable-Service` + `DaemonReload` use a single connection).

| Cmdlet | Status | Backend | Notes |
|---|---|---|---|
| `Get-Service` | Full | D-Bus `ListUnits` + `ListUnitFiles` | Name/wildcard filter; InputObject pipeline |
| `Start-Service` | Full | D-Bus `StartUnit` | SupportsShouldProcess |
| `Stop-Service` | Full | D-Bus `StopUnit` | SupportsShouldProcess |
| `Restart-Service` | Full | D-Bus `RestartUnit` | SupportsShouldProcess |
| `Set-Service` | Full | D-Bus `EnableUnits`/`DisableUnits` + `StartUnit`/`StopUnit` | Change startup type and/or running state |
| `New-Service` | Full | Write unit file + D-Bus `DaemonReload` + `EnableUnits` | Creates `.service` unit |
| `Remove-Service` | Full | D-Bus `StopUnit` + `DisableUnits` + delete unit file + `DaemonReload` | Complete teardown |
| `Suspend-Service` | Stub | PlatformNotSupported | systemd has no pause/continue |
| `Resume-Service` | Stub | PlatformNotSupported | systemd has no pause/continue |

All write cmdlets support `-WhatIf` and `-Confirm`.

---

## Requirements

- Linux with systemd (D-Bus)
- PowerShell 7.4+, .NET 8
- Root for `New-Service` and `Remove-Service` (system unit directory)
- Non-root users can manage user-scope services (`~/.config/systemd/user/`)

---

## Installation

```powershell
git clone https://github.com/peppekerstens/Services.Linux.Native
dotnet publish Services.Linux.Native/src/Services.Linux.Native --configuration Release --output bin/Release/net8.0/publish
Import-Module ./Services.Linux.Native/bin/Release/net8.0/publish/Services.Linux.Native.dll
```

> `dotnet publish` is required — `dotnet build` alone does not copy NuGet dependency DLLs (`Tmds.DBus.Protocol.dll`) on WSL. See [Implementation Notes](#implementation-notes).

---

## Usage

```powershell
# List all services
Get-Service

# Filter by name with wildcards
Get-Service -Name ssh* | Format-Table Name, Status, StartType

# Start / Stop / Restart
Start-Service sshd
Stop-Service sshd
Restart-Service sshd

# Change startup type
Set-Service -Name sshd -StartupType Automatic

# Create a new service
New-Service -Name myapp -BinaryPathName '/usr/bin/myapp --daemon' -Description 'My application'

# Remove a service (stops, disables, and deletes unit file)
Remove-Service -Name myapp

# Pipeline: Get-Service | Set-Service
Get-Service -Name mysql | Set-Service -Status Stopped -StartupType Disabled

# Use WhatIf to preview
Remove-Service -Name myapp -WhatIf
```

---

## Manual Testing

For a detailed, step-by-step guide on setting up your environment and testing these modules, see the blog post: [Testing the native layer](https://peppekerstens.github.io/testing-the-native-layer/).

### Option 1: Interactive Container (Recommended)
Use the pre-built CI images to avoid dependency issues.

```powershell
# Start an interactive shell in the Ubuntu 24.04 test container
docker compose -f docker-compose.test.yml run ubuntu-24 pwsh
```
Once inside:
```powershell
Import-Module /module/bin/Release/net8.0/publish/Services.Linux.Native.dll
Get-Service
```

### Option 2: Bare WSL
Test directly in your WSL distro (requires `.NET 8 SDK`).

```powershell
# Build and publish to ensure all NuGet dependencies are copied
dotnet publish src/Services.Linux.Native --configuration Release --output bin/Release/net8.0/publish

# Load the DLL
pwsh
Import-Module ./bin/Release/net8.0/publish/Services.Linux.Native.dll
Get-Service
```


| Distro | Image |
|---|---|
| Ubuntu 24.04 | `ghcr.io/peppekerstens/pwsh-pester-ubuntu:24.04` |
| Debian 12 | `ghcr.io/peppekerstens/pwsh-pester-debian:12` |
| Fedora 40 | `ghcr.io/peppekerstens/pwsh-pester-fedora:40` |
| openSUSE Tumbleweed | `ghcr.io/peppekerstens/pwsh-pester-opensuse:tumbleweed` |
| Arch Linux | `ghcr.io/peppekerstens/pwsh-pester-arch:latest` |

### Test scenarios

| Describe block | Scope | Tests |
|---|---|---|
| Module surface | everywhere | 9 cmdlet export checks + assembly load |
| Get-Service read-only | Linux (any user) | Enumerate, wildcard filter, nonexistent name |
| Stub cmdlets | everywhere | Suspend/Resume throw PlatformNotSupported |
| WhatIf safety | everywhere | All write cmdlets with -WhatIf |
| Start/Stop/Restart lifecycle | Linux + root | Create temp service, start, stop, restart, remove |
| Set-Service startup type | Linux + root | Change between Automatic/Manual/Disabled |
| New/Remove round-trip | Linux + root | Create unit, verify, remove, verify gone |
| Pipeline InputObject | Linux + root | Get-Service \| Stop-Service, Get-Service \| Remove-Service |

Run locally (requires systemd):

```powershell
Invoke-Pester -Path tests/Services.Linux.Native.Tests.ps1 -Output Detailed
```

---

## Implementation Notes

- **D-Bus, not `systemctl`**: Read operations use `ListUnits` + `ListUnitFiles` D-Bus methods on the systemd Manager interface — no subprocess overhead. Write operations use `StartUnit`, `StopUnit`, `EnableUnitFiles`, `DisableUnitFiles`.
- **`dotnet publish` for dependency resolution**: `Tmds.DBus.Protocol` is a NuGet dependency. `dotnet build` does not copy it to the output directory for library projects on WSL. `dotnet publish --output` is required to produce a self-contained module directory. Test `BeforeAll` probes both build and publish paths.
- **`string Status` avoids CA1416**: `ServiceControllerStatus` is annotated `[SupportedOSPlatform("windows")]` in .NET 8. Using `string` with constants `"Running"`, `"Stopped"` avoids platform-check build errors inside `TreatWarningsAsErrors=true` builds.
- **Shared D-Bus connection per invocation**: Compound cmdlets (`Set-Service`, `New-Service`, `Remove-Service`) open one D-Bus connection and pass it to all D-Bus operations. `Get-Service` and single-operation cmdlets use their own connection.
- **SynchronizationContext safety**: PowerShell's default runspace has no `SynchronizationContext`, so blocking on async (`.GetAwaiter().GetResult()`) is safe. Every blocking call is annotated with a comment documenting this assumption.
- **User-unit support**: `New-Service` and `Remove-Service` detect non-root callers via `id -u` and route to `~/.config/systemd/user/` instead of `/etc/systemd/system/`.

---

## Version history

| Version | Changes |
|---|---|
| 0.1.0 | Initial release. 7 full cmdlets, 2 stubs. D-Bus via `Tmds.DBus.Protocol` 0.93.0. 20 Pester tests. `dotnet publish` required for dependency resolution. |
| 0.1.1 | Code review fixes: `ConfigureAwait(false)` on all async calls; shared D-Bus connection for compound cmdlets; `IsNonRoot()` + `GetUserUnitDir()` for non-root unit file paths; removed dead `LU_*` constants. |

---

## Related

- [`PowerShell/PowerShell`](https://github.com/PowerShell/PowerShell) — target repo for upstream `*-Service` contribution (fork: `peppekerstens/PowerShell`, branch: `feature/service-unix-systemctl`)
- [opencode project plan](https://github.com/peppekerstens/opencode) — multi-stage project tracking
- [Blog series](https://peppekerstens.github.io) — write-up of the full journey

---

## License

[GNU General Public License v3](LICENSE)
