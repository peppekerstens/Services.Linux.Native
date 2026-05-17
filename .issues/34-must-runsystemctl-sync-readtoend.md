---
name: MUST — RunSystemctl uses sync ReadToEnd, manual arg splitting, no CultureInfo
labels: [bug, MUST]
---

## Rule violated
- **Rule number:** Rule 4
- **Rule name:** Process invocation follows C# conventions

## Location
- **File:** `src/Services.Linux.Native/Models/LinuxServiceController.cs`, lines 329-348

## What's wrong
`RunSystemctl()` in `LinuxServiceController` violates Rule 4:
1. Uses sync `ReadToEnd()` instead of `ReadToEndAsync()` — deadlock risk
2. Manually splits string into arguments: `arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)` — should use `ProcessStartInfo.ArgumentList` directly
3. No `CultureInfo.InvariantCulture` for string formatting

```csharp
private static string RunSystemctl(string arguments)
{
    var psi = new ProcessStartInfo { FileName = "systemctl", ... };
    foreach (var arg in arguments.Split(' ', ...))  // ❌ manual splitting
        psi.ArgumentList.Add(arg);
    string stdout = process.StandardOutput.ReadToEnd();  // ❌ sync
    string stderr = process.StandardError.ReadToEnd();   // ❌ sync
}
```

## How to fix
Change signature to accept `string[]` or `IEnumerable<string>` instead of a single string. Use `ReadToEndAsync()` on both stdout/stderr. Add `CultureInfo.InvariantCulture` to any string formatting.

## Severity
- [x] MUST — blocks merge
