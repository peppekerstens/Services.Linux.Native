# Module Review Checklist

Checklist for reviewing C# binary modules against project conventions.

## MUST (blockers)

- [ ] **Rule 1:** Native .NET APIs first, subprocess only as last resort
- [ ] **Rule 2:** No subprocess for tasks with native BCL or P/Invoke alternatives
- [ ] **Rule 3:** Native API is the platform's primary interface (D-Bus for systemd, libc for accounts)
- [ ] **Rule 4:** Exit code checked on every subprocess; non-zero throws or writes terminating error
- [ ] **Rule 5:** `ProcessStartInfo.ArgumentList` exclusively — no manual quoting/joining
- [ ] **Rule 6:** Error ID `UnauthorizedAccess`, Category `SecurityError` for elevation failures
- [ ] **Rule 7:** No bare `catch (Exception) { }` — every catch writes warning or is justified
- [ ] **Rule 8:** `OperatingSystem.IsWindows()` guard on all cmdlets
- [ ] **Rule 9:** Cross-platform type alignment — match Windows property names, types, inheritance
- [ ] **Rule 10:** Copyright header `// Copyright (c) peppekerstens. All rights reserved.`
- [ ] **Rule 11:** Error messages are const fields or centralized resources, not hardcoded strings

## SHOULD (strongly recommended)

- [ ] **Rule 12:** `HelpUri` and `RemotingCapability` on all `[Cmdlet]` attributes
- [ ] **Rule 13:** `ShouldProcess` uses display name formatting (not raw identifiers)
- [ ] **Rule 14:** `[Alias()]` on parameters matching Windows cmdlet parameter names
- [ ] **Rule 15:** Tests use `-Tags "CI"` and `$PSDefaultParameterValues` skip pattern
- [ ] **Rule 16:** No LINQ in hot paths — use foreach loops
- [ ] **Rule 17:** XML documentation on all public types and members
- [ ] **Rule 18:** Template units (`@.` pattern) filtered from systemd output

## PROJECT FILE

- [ ] `<TargetFramework>net8.0</TargetFramework>`
- [ ] `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- [ ] `<Deterministic>true</Deterministic>`
- [ ] `<GenerateDocumentationFile>false</GenerateDocumentationFile>`
- [ ] SMA pinned to `7.4.6` exactly
- [ ] File-scoped namespaces (`namespace Foo;`)

## PROCESS INVOCATION

- [ ] `ProcessStartInfo.ArgumentList` used (no manual string joining)
- [ ] `ReadToEndAsync()` on both stdout and stderr
- [ ] Exit code checked after process completes
- [ ] No shell invocation (`UseShellExecute = false`)

## CMDLET STRUCTURE

- [ ] `SupportsShouldProcess` on write cmdlets only
- [ ] Stubs throw `NotImplementedException` (no `SupportsShouldProcess`)
- [ ] Pipeline input supported on relevant parameters
- [ ] `[OutputType()]` annotation present
