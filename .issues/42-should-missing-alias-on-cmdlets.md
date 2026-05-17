---
name: SHOULD — Name param missing [Alias("ServiceName")] on Set/New/Remove-Service
labels: [enhancement, SHOULD]
---

## Rule violated
- **Rule number:** Rule 14
- **Rule name:** Parameter attributes must include Position, ValidateNotNullOrEmpty, and Alias

## Location
- **File:** `src/Services.Linux.Native/Commands/SetServiceCommand.cs` line 17-21
- **File:** `src/Services.Linux.Native/Commands/NewServiceCommand.cs` line 17-20
- **File:** `src/Services.Linux.Native/Commands/RemoveServiceCommand.cs` line 17-21

## What's wrong
`SetServiceCommand.Name` has `[Alias("ServiceName")]` but lacks `Position = 0`.
`NewServiceCommand.Name` has `Position = 0` and `[Alias("ServiceName")]` — correct.
`RemoveServiceCommand.Name` has `[Alias("ServiceName")]` but lacks `Position = 0`.

## How to fix
Add `Position = 0` to the `[Parameter]` attribute on `SetServiceCommand.Name` and `RemoveServiceCommand.Name`.

## Severity
- [ ] SHOULD — should be fixed before merge
