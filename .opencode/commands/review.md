---
description: Code review against linux-rules.md
agent: build
---
Review all `.cs` files in `src/` against `docs/linux-rules.md`.

**Step 1 — Read the rules**
Read `docs/linux-rules.md` to understand all applicable rules.

**Step 2 — Scan source files**
Read all `.cs` files in `src/Services.Linux.Native/`:
- `Commands/*.cs` — cmdlet implementations
- `Helpers/*.cs` — helper classes
- `Models/*.cs` — output types

**Step 3 — Check each rule**
For each rule in `linux-rules.md`, verify compliance across all files.
Report violations with:
- Rule number and name
- File path and line number
- What's wrong and how to fix it

**Step 4 — Additional checks**
- No dead code or unused imports
- No silent error swallowing (every `catch` is justified)
- Copyright headers present on all files
- File-scoped namespaces (C# 10+)
- `ProcessStartInfo.ArgumentList` used (no manual string joining)
- `ReadToEndAsync()` on stdout/stderr (no sync `ReadToEnd()`)
- `ConfigureAwait(false)` on async methods
- `SupportsShouldProcess` only on write cmdlets, not stubs

**Step 5 — Summary**
Report: total files reviewed, violations found (MUST/SHOULD/MINOR), compliance percentage.
