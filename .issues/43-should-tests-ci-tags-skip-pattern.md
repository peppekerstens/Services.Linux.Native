---
name: SHOULD — Tests lack -Tags "CI" and $PSDefaultParameterValues skip pattern
labels: [enhancement, SHOULD]
---

## Rule violated
- **Rule number:** Rule 16
- **Rule name:** Tests must use CI/RequireAdmin tags and skip pattern

## Location
- **File:** `tests/Services.Linux.Native.Tests.ps1`

## What's wrong
Tests don't use `-Tags "CI"` on Describe blocks and don't save/restore `$PSDefaultParameterValues` for platform-specific skipping.

## How to fix
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

## Severity
- [ ] SHOULD — should be fixed before merge
