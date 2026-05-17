---
name: SHOULD — ServiceStartupType enum lacks XML documentation
labels: [enhancement, SHOULD]
---

## Rule violated
- **Rule number:** Rule 18
- **Rule name:** Public output types must have XML documentation comments

## Location
- **File:** `src/Services.Linux.Native/Models/ServiceStartupType.cs`

## What's wrong
`ServiceStartupType` enum and its values have no `<summary>` XML docs. Rule 18 requires documentation on all public types.

## How to fix
```csharp
/// <summary>
/// Specifies the startup type of a service.
/// </summary>
public enum ServiceStartupType
{
    /// <summary>Invalid or unspecified value.</summary>
    InvalidValue = -1,
    /// <summary>The service starts automatically at boot.</summary>
    Automatic = 2,
    // ...
}
```

## Severity
- [ ] SHOULD — should be fixed before merge
