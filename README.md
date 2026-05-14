# Services.Linux.Native

Native Linux `*-Service` cmdlets for PowerShell 7, using D-Bus/systemd.

## Cmdlets

| Cmdlet | Description |
|---|---|
| `Get-Service` | List systemd services via D-Bus `ListUnits` + `ListUnitFiles` |
| `Start-Service` | Start a unit via `StartUnit` |
| `Stop-Service` | Stop a unit via `StopUnit` |
| `Restart-Service` | Restart a unit via `RestartUnit` |
| `Set-Service` | Change startup type (`EnableUnits`/`DisableUnits`) or status |
| `New-Service` | Create a unit file in `/etc/systemd/system/` |
| `Remove-Service` | Stop, disable, and delete a unit file |
| `Suspend-Service` | Not supported (systemd has no pause/continue) |
| `Resume-Service` | Not supported (systemd has no pause/continue) |

## Build

```sh
dotnet build -c Release
```

Publish with dependencies for module distribution:

```sh
dotnet publish -c Release --output bin/Release/net8.0/publish
```

## Test

```powershell
Import-Module Pester -MinimumVersion 5.5.0
Invoke-Pester -Path tests/Services.Linux.Native.Tests.ps1
```

## Dependencies

- [System.Management.Automation](https://www.nuget.org/packages/System.Management.Automation/) 7.4.\*
- [Tmds.DBus.Protocol](https://www.nuget.org/packages/Tmds.DBus.Protocol/) 0.93.0
- .NET 8.0
- PowerShell 7+
- systemd (D-Bus)
