---
name: SHOULD — LINQ in DependentServices and ServicesDependedOn getters
labels: [enhancement, SHOULD]
---

## Rule violated
- **Rule number:** Rule 17
- **Rule name:** Avoid LINQ and params arrays in performance-sensitive code

## Location
- **File:** `src/Services.Linux.Native/Models/LinuxServiceController.cs`, lines 135-172

## What's wrong
`DependentServices` and `ServicesDependedOn` use LINQ (`.Select()`, `.Where()`, `.ToArray()`) in enumeration code paths. Rule 17 requires foreach loops.

```csharp
_dependentServices = names
    .Select(n => new LinuxServiceController(n, n, "Unknown", ...))
    .ToArray();
```

## How to fix
Replace with foreach loops:
```csharp
var deps = new List<LinuxServiceController>();
foreach (var n in names)
    deps.Add(new LinuxServiceController(n, n, "Unknown", ...));
_dependentServices = deps.ToArray();
```

## Severity
- [ ] SHOULD — should be fixed before merge
