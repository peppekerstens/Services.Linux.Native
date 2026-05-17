---
name: SHOULD — ShouldProcess uses raw unitName without display name formatting
labels: [enhancement, SHOULD]
---

## Rule violated
- **Rule number:** Rule 13
- **Rule name:** ShouldProcess must use a formatted display string

## Location
- **File:** `src/Services.Linux.Native/Commands/ServiceUnixBase.cs`, line 55-60

## What's wrong
`FormatShouldProcessTarget()` strips the `.service` suffix but doesn't include the display name. Rule 13 requires: `"sshd (Secure Shell Daemon)"`, not just `"sshd"`.

```csharp
internal static string FormatShouldProcessTarget(string unitName)
{
    if (unitName.EndsWith(".service", ...))
        return unitName.Substring(0, unitName.Length - 8);  // → "sshd"
    return unitName;
}
```

## How to fix
Resolve the display name via D-Bus and format it:
```csharp
string target = string.IsNullOrEmpty(displayName) || displayName == unitName
    ? unitName
    : $"{unitName} ({displayName})";
```

## Severity
- [ ] SHOULD — should be fixed before merge
