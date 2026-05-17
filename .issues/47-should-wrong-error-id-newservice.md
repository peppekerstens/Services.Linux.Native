---
name: SHOULD — NewServiceCommand DaemonReload uses wrong error ID/category
labels: [bug, SHOULD]
---

## Rule violated
- **Rule number:** Rule 6
- **Rule name:** Consistent error handling across all modules

## Location
- **File:** `src/Services.Linux.Native/Commands/NewServiceCommand.cs`, lines 62-67

## What's wrong
`NewServiceCommand` DaemonReload catch block uses error ID `"ElevationRequired"` and category `"PermissionDenied"` instead of `"UnauthorizedAccess"` and `"SecurityError"` as required by Rule 6.

```csharp
WriteError(new ErrorRecord(
    new PSSecurityException(ErrorMessages.Format(ErrorMessages.ElevationRequired, MyInvocation.MyCommand.Name)),
    "ElevationRequired", ErrorCategory.PermissionDenied, unitName));  // ❌ wrong ID/category
```

## How to fix
Change to:
```csharp
WriteError(new ErrorRecord(
    new PSSecurityException(ErrorMessages.Format(ErrorMessages.ElevationRequired, MyInvocation.MyCommand.Name)),
    "UnauthorizedAccess", ErrorCategory.SecurityError, unitName));
```

## Severity
- [ ] SHOULD — should be fixed before merge
