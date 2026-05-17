# Services.Linux.Native — Contributor Guide

## What this module is

A C# binary PowerShell module providing Linux `*-Service` cmdlets (`Get-Service`, `Start-Service`, `Stop-Service`, `Restart-Service`, `Set-Service`, `New-Service`, `Remove-Service`) via D-Bus `systemd` API. Designed as a drop-in replacement for Windows `ServiceController`-based cmdlets on Linux.

Part of the [PowerShell Linux Commands](https://github.com/peppekerstens/opencode) project.

---

## Quick Start

```bash
# Build
dotnet build -c Release

# Run tests (requires pwsh)
pwsh -c "Import-Module ./src/Services.Linux.Native/bin/Release/net8.0/Services.Linux.Native.dll; Invoke-Pester ./tests/"
```

---

## Architecture

```
src/Services.Linux.Native/
├── Commands/          # Cmdlet implementations
│   ├── ServiceUnixBase.cs    # Base class — resolves unit names, common logic
│   ├── GetServiceCommand.cs
│   ├── StartServiceCommand.cs
│   ├── StopServiceCommand.cs
│   ├── RestartServiceCommand.cs
│   ├── SetServiceCommand.cs
│   ├── NewServiceCommand.cs
│   ├── RemoveServiceCommand.cs
│   ├── SuspendServiceCommand.cs  # Stub
│   └── ResumeServiceCommand.cs   # Stub
├── Helpers/
│   ├── ErrorMessages.cs        # Centralized error message constants
│   └── SystemdHelper.cs        # D-Bus calls (ListUnits, Enable/Disable, DaemonReload, etc.)
└── Models/
    ├── LinuxServiceController.cs   # Main output type, inherits Component
    ├── ServiceControllerStatus.cs  # Status enum (matches Windows values)
    ├── ServiceStartupType.cs       # Startup type enum
    └── ServiceType.cs              # Linux-native service type enum (1000+ range)
```

### Key design decisions

- **D-Bus is the native API** — `Tmds.DBus.Protocol` for direct `systemd` communication. Subprocess (`systemctl`) is last resort only.
- **`LinuxServiceController : Component`** — matches Windows `ServiceController` inheritance for cross-platform type alignment (Rule 9).
- **Reactive elevation** — D-Bus polkit errors are caught and translated to `"root privileges are required."` No proactive `id -u` check.
- **`-WhatIf` safety** — `ServiceUnixBase.ProcessRecord()` resolves unit names without opening D-Bus, so `-WhatIf` works for non-root users.

---

## C# Conventions

| Rule | Detail |
|---|---|
| **Target** | `net8.0`, `TreatWarningsAsErrors=true`, `Deterministic=true` |
| **SMA** | Pinned to `7.4.6` exactly |
| **Namespaces** | File-scoped (`namespace Foo;`) |
| **P/Invoke** | `[LibraryImport]` + `partial` methods (source-generated) |
| **Process** | `ProcessStartInfo.ArgumentList` only, `ReadToEndAsync()` on stdout/stderr |
| **Cmdlets** | `SupportsShouldProcess` on write cmdlets only, stubs throw `NotImplementedException` |
| **Async** | `ConfigureAwait(false)` on all async methods |
| **Errors** | `ErrorRecord` with `UnauthorizedAccess` ID, `SecurityError` category |
| **Copyright** | `// Copyright (c) peppekerstens. All rights reserved.` |

Full rules: `docs/linux-rules.md`

### Version alignment
- **Single source of truth:** `<Version>` in `.csproj`
- **Must match:** `STATUS.md` `**Version:**` line, README.md version history table (latest entry)
- **Bump rule:** `.csproj` first, then `STATUS.md`, then README.md — in that order

---

## Testing

- **Framework:** Pester 5
- **Runner:** `pwsh -c "Invoke-Pester ./tests/"`
- **GHA:** 5-distro matrix (Ubuntu 24.04, Debian 12, Fedora 40, openSUSE Tumbleweed, Arch Linux) + Windows
- **Container images:** `ghcr.io/peppekerstens/pwsh-pester-*`
- **Test file:** `tests/Services.Linux.Native.Tests.ps1`

Tests load the DLL directly: `Import-Module ./src/.../Services.Linux.Native.dll`

---

## Current State

See `STATUS.md` for module state, open issues, and next steps.

**Open issues:** 8 (2 MUST, 6 SHOULD) — see [Issues](https://github.com/peppekerstens/Services.Linux.Native/issues)

---

## Boundaries

### What lives in this repo
- Source code (`src/Services.Linux.Native/`)
- Pester tests (`tests/`)
- CI/CD (`.github/workflows/`)
- Module status (`STATUS.md`)
- Contributor guide (`AGENTS.md`)
- Development rules (`docs/linux-rules.md`)
- OpenCode config (`.opencode/`)

### What lives elsewhere
- Cross-repo planning, status aggregation, project plan → https://github.com/peppekerstens/opencode
- Other modules → https://github.com/peppekerstens/
- PowerShell fork (upstream contribution) → https://github.com/peppekerstens/PowerShell
- Blog posts → https://peppekerstens.github.io

### What to do when
| Scenario | Where |
|---|---|
| Bug in this module | File issue in **this repo** |
| Feature request for this module | File issue in **this repo** |
| Cross-module convention change | File issue in **opencode** |
| Upstream PR to PowerShell | Work in **PowerShell fork** |

---

## Coordination

This module is part of a larger project. Cross-repo planning lives at:
- **Coordination repo:** https://github.com/peppekerstens/opencode
- **Project plan:** https://github.com/peppekerstens/opencode/blob/main/plan.md
- **PowerShell fork:** https://github.com/peppekerstens/PowerShell (branch: `feature/service-unix-systemctl`)
