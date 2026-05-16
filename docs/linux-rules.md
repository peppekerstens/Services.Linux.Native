# Linux Development Rules — Services.Linux.Native

This repo follows the Stage 6 Linux Development Rules defined in the
opencode coordination repo. The most relevant rule for this module is:

## Rule 9: Cross-platform type alignment is mandatory

When a Linux cmdlet returns objects that have a Windows counterpart, the
Linux type MUST be designed to maximize compatibility with the Windows type.

**Priority order (apply in sequence):**

1. **Inherit from a common cross-platform base class** — If the Windows type
   inherits from a cross-platform class (e.g. `System.ComponentModel.Component`),
   the Linux type MUST inherit from the same base. This enables shared type
   checks (`-is [Component]`), `IDisposable` semantics, and designer support.

2. **Match property names and types** — Use the same property names as the
   Windows type. If the Windows type uses `ServiceName`, the Linux type must
   also use `ServiceName` (not `Name`). If the Windows type uses an enum,
   the Linux type must use the same enum (with `#if UNIX` guards if needed
   to suppress CA1416 warnings).

3. **Match method signatures** — Implement the same instance methods with
   identical signatures. Methods that cannot be implemented on Linux must
   throw `PlatformNotSupportedException` with a clear message explaining
   why (e.g. "systemd has no pause/resume concept").

4. **Add Linux-specific extensions only after alignment** — Linux-specific
   properties (e.g. `ActiveState`, `SubState` for systemd) may be added
   AFTER all Windows-compatible properties are present. They must be clearly
   documented as Linux-only.

5. **Split the class tree only as a last resort** — If the Windows type is
   fundamentally incompatible (e.g. `ServiceController` wraps Windows-only
   SC_HANDLE), create a parallel Linux type that inherits from the same
   cross-platform base class (`Component`) and implements the same public
   API surface. The Linux type lives in the same namespace
   (`Microsoft.PowerShell.Commands`) to avoid naming conflicts.

**Rationale:** Cross-platform scripts should work on both Windows and Linux
without modification. Type mismatches (`$svc -is [ServiceController]` returning
`$false` on Linux) silently break pipeline filters and type checks.

**Example — correct approach:**
```csharp
// Windows: System.ServiceProcess.ServiceController : Component
// Linux:   Microsoft.PowerShell.Commands.LinuxServiceController : Component
// Both inherit from Component. Both expose Status, ServiceName, StartType,
// Start(), Stop(), Refresh(), etc.
```

**Example — incorrect approach:**
```csharp
// Linux type extends object, uses different property names, no methods
public sealed class LinuxServiceInfo { public string Name { get; set; } }
// Scripts that access $svc.ServiceName or call $svc.Stop() break.
```

---

Full rules: `opencode/stage6/linux-rules.md` in the coordination repo.
