# Phase 5: Fix Solution File and FV Integration Wiring - Research

**Researched:** 2026-03-29
**Domain:** .NET solution structure, Roslyn analyzer packaging, CLI catalog maintenance
**Confidence:** HIGH

## Summary

Phase 5 addresses four discrete issues identified in the v1.5.0 milestone audit: a blocking solution build failure (BROKEN-01), missing CLI diagnostic catalog entries (BROKEN-02), HelpLinkUri username inconsistencies in FluentValidation descriptors, and missing analyzer release tracking for IOC100-102.

All four issues are well-understood, precisely located in the codebase, and have straightforward fixes. The solution file issue is a naming collision between a real project and a solution folder. The DiagnosticCatalog gap is three missing entries. The HelpLinkUri fix is a single constant change. The analyzer release tracking requires adding an AnalyzerReleases.Unshipped.md file.

**Primary recommendation:** Execute as a single plan with 4 small tasks -- each fix is independent and mechanical.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| BROKEN-01 | Fix solution-level build failure (MSB5004 duplicate project name "IoCTools.Generator") | Solution file line 27 has solution folder with same name as real project on line 3; rename folder |
| BROKEN-02 | Add IOC100-102 to DiagnosticCatalog for CLI suppress command | DiagnosticCatalog.cs BuildCatalog() ends at IOC094; add 3 entries for FluentValidation diagnostics |
| TECH-DEBT-1 | Fix HelpLinkUri inconsistency (nate123456 vs nathan-p-lane) | FluentValidationDiagnosticDescriptors.cs line 12 uses nate123456; main generator uses nathan-p-lane |
| TECH-DEBT-2 | Fix RS2008 analyzer release tracking warning | Main generator suppresses RS2008 via NoWarn; FV generator needs either same suppression or proper AnalyzerReleases.Unshipped.md |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- Generator targets netstandard2.0 -- no records, init properties, required members
- File-scoped namespaces, using inside namespace, var preferred
- 4 spaces, UTF-8, LF, final newline (per .editorconfig)
- Testing: FluentAssertions, xUnit, sealed test classes, Arrange/Act/Assert
- Manual DiagnosticCatalog approach (decision from Phase 02)

## Architecture Patterns

### Issue 1: Solution File Duplicate Name (BROKEN-01)

**What:** `IoCTools.sln` line 3 declares `IoCTools.Generator` as a real C# project (GUID type `FAE04EC0-301F-11D3-BF4B-00C04F79EFBC`). Line 27 declares another entry named `IoCTools.Generator` as a solution folder (GUID type `2150E333-8FDC-42A3-9474-1A3956D46DE8`). MSBuild rejects duplicate names regardless of type.

**Current state:**
- Line 3: Real project `IoCTools.Generator` -> `IoCTools.Generator\IoCTools.Generator\IoCTools.Generator.csproj` (GUID `{ABDDF82E-...}`)
- Line 21: Solution folder `IoCTools.FluentValidation` (GUID `{DF809D30-...}`) -- this one works fine
- Line 27: Solution folder `IoCTools.Generator` (GUID `{EB9FB446-...}`) -- COLLISION

The solution folder on line 27 appears to have no `NestedProjects` entries -- the `NestedProjects` section only nests the FV project under the FV folder. The Generator solution folder was likely added to mirror the FV folder structure but was never wired up.

**Fix:** Rename the solution folder display name on line 27 from `"IoCTools.Generator"` to something non-colliding. Options:
- `"Generator"` -- simple, matches FV folder pattern where `IoCTools.FluentValidation` folder already groups FV projects
- Delete line 27 entirely if it has no nested children (currently no `NestedProjects` entries reference GUID `{EB9FB446-...}`)

**Recommended approach:** Delete the unused solution folder entry (line 27) entirely. It has no nested projects and serves no purpose. If the intent was to nest the real Generator project, add a `NestedProjects` entry instead.

### Issue 2: DiagnosticCatalog Missing IOC100-102 (BROKEN-02)

**What:** `IoCTools.Tools.Cli/Utilities/DiagnosticCatalog.cs` `BuildCatalog()` method contains entries for IOC001-IOC094 (main generator) and IOC087-IOC089 (configuration). It does not include IOC100, IOC101, or IOC102 (FluentValidation diagnostics).

**Impact:** `ioc-tools suppress --codes IOC100` silently produces nothing because the code pattern can only find known IDs.

**Fix:** Add three entries to `BuildCatalog()`:

```csharp
// IoCTools.FluentValidation
new("IOC100", "Validator directly instantiates DI-managed child validator", "IoCTools.FluentValidation", "Warning"),
new("IOC101", "Validator composition creates lifetime mismatch", "IoCTools.FluentValidation", "Warning"),
new("IOC102", "Validator class missing partial modifier", "IoCTools.FluentValidation", "Error"),
```

Source data from `FluentValidationDiagnosticDescriptors.cs`: IOC100 is Warning, IOC101 is Warning, IOC102 is Error. Category is `IoCTools.FluentValidation`.

### Issue 3: HelpLinkUri Username Inconsistency (TECH-DEBT-1)

**What:** `FluentValidationDiagnosticDescriptors.cs` line 12 defines:
```csharp
private const string HelpLinkBase = "https://github.com/nate123456/IoCTools/blob/main/docs/diagnostics.md";
```

All main generator descriptors use `nathan-p-lane`:
```
https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#iocXXX
```

Note: The actual git remote is `sansiquay/IoCTools`. Both usernames in the code are potentially wrong relative to the actual repo, but the consistency fix should at minimum make FV match the main generator pattern (`nathan-p-lane`).

Additionally, the `.csproj` files for ALL packages (Generator, FV, Testing, Abstractions) use `nate123456` in `PackageProjectUrl` and `RepositoryUrl`. These are a separate concern but could be addressed in this phase if desired.

**Fix:** Change `FluentValidationDiagnosticDescriptors.cs` line 12 to use `nathan-p-lane` to match all other diagnostic descriptors.

### Issue 4: RS2008 Analyzer Release Tracking (TECH-DEBT-2)

**What:** The main generator suppresses RS2008 via `<NoWarn>$(NoWarn);RS2008</NoWarn>` in its csproj. The FV generator csproj does NOT have this suppression, resulting in RS2008 warnings during build.

RS2008 fires when a Roslyn analyzer package declares `DiagnosticDescriptor` instances but lacks `AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md` tracking files. These files document which analyzer version introduced/shipped each diagnostic ID.

**Two approaches:**
1. **Suppress RS2008** (match main generator pattern): Add `<NoWarn>$(NoWarn);RS2008</NoWarn>` to FV csproj. Simplest, consistent with existing convention.
2. **Add AnalyzerReleases files** (proper tracking): Create `AnalyzerReleases.Unshipped.md` with IOC100-102 entries. More work but follows Roslyn best practices.

**Recommended:** Suppress RS2008 to match the existing main generator convention. Adding proper release tracking would be a separate initiative affecting both generators.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Solution file editing | Manual text parsing | Direct text edit of .sln | Solution files are simple text format; dotnet sln commands could also work but manual edit is more precise for renames/deletes |

## Common Pitfalls

### Pitfall 1: Solution folder GUID confusion
**What goes wrong:** Editing the wrong line or GUID in the .sln file
**Why it happens:** Solution files have two different GUID types -- project type GUIDs and project instance GUIDs
**How to avoid:** `{2150E333-8FDC-42A3-9474-1A3956D46DE8}` is always a solution folder. `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}` is always a C# project. Target line 27 specifically (GUID `{EB9FB446-...}`).
**Warning signs:** Build still fails after edit, or projects disappear from solution

### Pitfall 2: DiagnosticCatalog category mismatch
**What goes wrong:** Using wrong category string for FV entries
**Why it happens:** Main generator uses subcategories like `IoCTools.Dependency`, `IoCTools.Lifetime` etc. FV descriptors use `IoCTools.FluentValidation`.
**How to avoid:** Copy category string exactly from `FluentValidationDiagnosticDescriptors.cs` -- it is `"IoCTools.FluentValidation"`
**Warning signs:** CLI suppress command groups FV diagnostics incorrectly

### Pitfall 3: Incomplete .sln cleanup
**What goes wrong:** Removing solution folder line but leaving orphaned references in GlobalSection
**Why it happens:** Solution files have cross-references in NestedProjects and ProjectConfigurationPlatforms sections
**How to avoid:** Check that GUID `{EB9FB446-FAA7-0AA6-C162-AFF533E84A0C}` does not appear in any other section. Currently confirmed: no NestedProjects or configuration entries reference this GUID.

## Code Examples

### DiagnosticCatalog entries to add
```csharp
// Source: FluentValidationDiagnosticDescriptors.cs (IOC100-102)
// Add after the IoCTools.Configuration section:

// IoCTools.FluentValidation
new("IOC100", "Validator directly instantiates DI-managed child validator", "IoCTools.FluentValidation", "Warning"),
new("IOC101", "Validator composition creates lifetime mismatch", "IoCTools.FluentValidation", "Warning"),
new("IOC102", "Validator class missing partial modifier", "IoCTools.FluentValidation", "Error"),
```

### HelpLinkUri fix
```csharp
// FluentValidationDiagnosticDescriptors.cs line 12
// Before:
private const string HelpLinkBase = "https://github.com/nate123456/IoCTools/blob/main/docs/diagnostics.md";
// After:
private const string HelpLinkBase = "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md";
```

### RS2008 suppression
```xml
<!-- IoCTools.FluentValidation.csproj, add to PropertyGroup -->
<NoWarn>$(NoWarn);RS2008</NoWarn>
```

### Solution file fix (delete approach)
```
Delete line 27 and 28 from IoCTools.sln:
  Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "IoCTools.Generator", "IoCTools.Generator", "{EB9FB446-FAA7-0AA6-C162-AFF533E84A0C}"
  EndProject
```

## Open Questions

1. **Should csproj PackageProjectUrl/RepositoryUrl also be fixed?**
   - What we know: All csproj files use `nate123456`, the actual remote is `sansiquay`. Main generator descriptors use `nathan-p-lane`.
   - What's unclear: Which username is canonical for public-facing URLs (NuGet package metadata)
   - Recommendation: Out of scope for this phase unless user decides otherwise. The audit only flagged the HelpLinkUri inconsistency, not csproj URLs.

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection of `IoCTools.sln` (lines 3, 21, 27)
- Direct codebase inspection of `DiagnosticCatalog.cs` (confirmed IOC094 is last entry)
- Direct codebase inspection of `FluentValidationDiagnosticDescriptors.cs` (confirmed `nate123456` in HelpLinkBase)
- Build verification: `dotnet build IoCTools.sln` confirmed MSB5004 error
- Direct inspection of main generator csproj RS2008 suppression pattern

## Metadata

**Confidence breakdown:**
- Solution fix: HIGH - verified build failure, root cause identified, fix is mechanical
- DiagnosticCatalog: HIGH - entries verified against FluentValidationDiagnosticDescriptors.cs
- HelpLinkUri: HIGH - direct string comparison between FV and main generator
- RS2008: HIGH - existing suppression pattern in main generator csproj

**Research date:** 2026-03-29
**Valid until:** Indefinite (codebase-specific fixes, no external dependency concerns)
