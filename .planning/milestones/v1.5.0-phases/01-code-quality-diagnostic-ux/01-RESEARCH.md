# Phase 1: Code Quality and Diagnostic UX - Research

**Researched:** 2026-03-21
**Domain:** Roslyn source generator diagnostics, C# code quality patterns
**Confidence:** HIGH

## Summary

Phase 1 is a stabilization and polish phase with no new features. The work involves: (1) adding `helpLinkUri` and specific IDE categories to all 87 diagnostic descriptors, (2) replacing bare `catch(Exception)` blocks with the proven DiagnosticsRunner pattern, (3) centralizing ad-hoc `RegisterAsAllAttribute` string checks through `AttributeTypeChecker`, (4) expanding the `ReportDiagnosticDelegate` pattern to more validators, (5) enhancing IOC012/013/015/016-019 diagnostic messages, and (6) fixing 1 CS8603 nullable warning.

All work stays within the existing codebase patterns. No new libraries are needed. The `DiagnosticDescriptor` constructor already supports an 8th `helpLinkUri` parameter (used by `DiagnosticDescriptorFactory.WithSeverity` which already preserves it). The `AttributeTypeChecker` already has the `RegisterAsAllAttribute` constant and `IsAttribute()` method. The DiagnosticsRunner exception pattern is proven and can be copied directly.

**Primary recommendation:** Execute as mechanical refactoring -- each task is a find-and-replace or augment operation on known locations. No design decisions required beyond the ones locked in CONTEXT.md.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** All 87 diagnostic descriptors get HelpLinkUri pointing to GitHub repo anchors: `https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#iocXXX`
- **D-02:** Single `docs/diagnostics.md` file with one anchor per diagnostic -- no per-diagnostic files
- **D-03:** Lean content per anchor: diagnostic code, severity, one-line cause, one-line fix. Code examples and detailed guidance deferred to Phase 4 (Documentation)
- **D-04:** If the file is renamed/moved, the HelpLinkUri update is part of the same commit
- **D-05:** Categories use prefixed names matching the existing descriptor file organization:
  - `IoCTools.Lifetime` (LifetimeDiagnostics.cs)
  - `IoCTools.Dependency` (DependencyDiagnostics.cs)
  - `IoCTools.Configuration` (ConfigurationDiagnostics.cs)
  - `IoCTools.Registration` (RegistrationDiagnostics.cs)
  - `IoCTools.Structural` (StructuralDiagnostics.cs)
- **D-06:** Mapping is 1:1 with descriptor source files -- no ambiguity about which diagnostic goes where
- **D-07:** Replace bare `catch(Exception)` blocks with: emit internal diagnostic (IOC996/997) + return gracefully
- **D-08:** Use OOM/SOF filter: `catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)`
- **D-09:** Match the existing DiagnosticsRunner.cs pattern -- user sees the error in build output but build isn't blocked
- **D-10:** Two sites to fix: ConstructorGenerator.cs line 437, ServiceRegistrationGenerator.RegistrationCode.cs lines 90-96
- **D-11:** Route all 10+ ad-hoc `.Name == "RegisterAsAllAttribute"` checks through `AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute)`
- **D-12:** Also convert 3+ inline `.ToDisplayString()` FQN comparisons in RegistrationSelector.cs and DependencySetValidator.cs to use AttributeTypeChecker
- **D-13:** Expand ReportDiagnosticDelegate pattern to 3-4 more validators -- currently only used in ServiceRegistrationGenerator.RegisterAs.cs

### Claude's Discretion
- Which 3-4 validators to adopt ReportDiagnosticDelegate in (pick based on complexity benefit)
- Exact IOC996/997 descriptor message text and description
- QUAL-02 delegate shape -- whether to use the existing file-private delegate or create a shared abstraction
- IOC012/013 exact wording for IServiceProvider/CreateScope() suggestion
- IOC015 exact format for inheritance path display (e.g., "A -> B -> C" vs "A -> B -> C")
- IOC016-019 configuration example format in messages

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| QUAL-01 | Centralize RegisterAsAllAttribute checks using AttributeTypeChecker (20 inconsistent locations) | 10 `.Name ==` sites + 3 `.ToDisplayString()` FQN sites identified; `AttributeTypeChecker.IsAttribute()` already exists |
| QUAL-02 | Adopt ReportDiagnosticDelegate pattern in 3-4 more validators | Existing delegate in `ServiceRegistrationGenerator.RegisterAs.cs` line 10; candidates identified in DiagnosticRules.cs and DependencyUsageValidator.cs |
| QUAL-03 | Resolve CS8603 null reference warnings in sample code (3 instances in MultiInterfaceExamples.cs) | Line 53: `GetValueOrDefault(id) ?? null` -- redundant null-coalesce on already-nullable return |
| QUAL-04 | Add code comments explaining InstanceSharing.Separate default behavior | 3 sites: RegisterAsAttribute.cs, ServiceRegistrationGenerator.RegisterAs.cs line 36, RegisterAsExamples.cs lines 331-332 |
| QUAL-05 | Tighten bare catch(Exception) blocks in ConstructorGenerator, InterfaceDiscovery, and ServiceRegistrationGenerator | ConstructorGenerator.cs line 437 (bare catch returning ""), ServiceRegistrationGenerator.RegistrationCode.cs lines 90-96 (conditional re-throw). InterfaceDiscovery.cs already uses specific exceptions (InvalidOperationException, NullReferenceException) -- no change needed there |
| DUX-01 | HelpLinkUri added to all 87+ diagnostic descriptors | All 87 descriptors use 7-arg constructor; add 8th param. Create `docs/diagnostics.md` as link target |
| DUX-02 | IDE categories updated to specific groupings | Currently all use `"IoCTools"` category; change to `IoCTools.{Subcategory}` per D-05 mapping |
| DUX-03 | IOC012/013 messages suggest IServiceProvider/CreateScope() pattern | Current messages reference lifetime changes only; need to add factory/scope pattern |
| DUX-04 | IOC015 message shows full inheritance path (A -> B -> C) | Current message is generic; needs format string with path parameter |
| DUX-05 | IOC016-019 messages include configuration examples showing valid usage | Current descriptions are generic; need inline examples |
</phase_requirements>

## Standard Stack

### Core
No new libraries needed. This phase works entirely within the existing stack.

| Library | Version | Purpose | Status |
|---------|---------|---------|--------|
| Microsoft.CodeAnalysis.CSharp | 4.5.0 | Roslyn APIs for DiagnosticDescriptor | Already installed |
| Microsoft.CodeAnalysis.Analyzers | 3.3.4 | Analyzer rules enforcement | Already installed |

### Supporting
No additional libraries.

### Alternatives Considered
None -- this is pure internal refactoring.

## Architecture Patterns

### DiagnosticDescriptor Construction Pattern

**Current (7-arg):**
```csharp
public static readonly DiagnosticDescriptor SomeRule = new(
    "IOC0XX",
    "Title",
    "Message format '{0}'",
    "IoCTools",          // category -- CHANGE THIS
    DiagnosticSeverity.Error,
    true,
    "Description text.");
```

**Target (8-arg with helpLinkUri):**
```csharp
public static readonly DiagnosticDescriptor SomeRule = new(
    "IOC0XX",
    "Title",
    "Message format '{0}'",
    "IoCTools.Lifetime",  // category -- use subcategory
    DiagnosticSeverity.Error,
    true,
    "Description text.",
    "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc0xx");
```

The `DiagnosticDescriptor` constructor positional parameters are:
1. `string id`
2. `string title` (or `LocalizableString`)
3. `string messageFormat` (or `LocalizableString`)
4. `string category`
5. `DiagnosticSeverity defaultSeverity`
6. `bool isEnabledByDefault`
7. `string? description` (or `LocalizableString?`)
8. `string? helpLinkUri` -- **ADD THIS**
9. `params string[] customTags` -- rarely used, but `DiagnosticDescriptorFactory.WithSeverity` already preserves it

### Category-to-File Mapping

| File | Category Value | Descriptor Count |
|------|---------------|------------------|
| LifetimeDiagnostics.cs | `IoCTools.Lifetime` | 10 |
| DependencyDiagnostics.cs | `IoCTools.Dependency` | 27 |
| ConfigurationDiagnostics.cs | `IoCTools.Configuration` | 10 |
| RegistrationDiagnostics.cs | `IoCTools.Registration` | 27 |
| StructuralDiagnostics.cs | `IoCTools.Structural` | 13 |
| **Total** | | **87** |

### Exception Handling Pattern (from DiagnosticsRunner.cs)

**Model pattern to replicate:**
```csharp
try
{
    // generator work
}
catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
{
    GeneratorDiagnostics.Report(context, "IOC996",
        "Diagnostic validation pipeline error",
        $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    throw; // or return gracefully depending on site
}
```

**Site 1 -- ConstructorGenerator.cs line 437:**
Currently: `catch (Exception) { return ""; }`
Target: Add OOM/SOF filter, emit IOC997, return "".

**Site 2 -- ServiceRegistrationGenerator.RegistrationCode.cs lines 90-96:**
Currently: Conditional re-throw wrapping in InvalidOperationException only when conditionalServices is non-empty.
Target: Add OOM/SOF filter, emit IOC997, preserve the graceful-fallback behavior.

### RegisterAsAllAttribute Centralization Pattern

**Current ad-hoc pattern (10 sites):**
```csharp
attr.AttributeClass?.Name == "RegisterAsAllAttribute"
```

**Target centralized pattern:**
```csharp
AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute)
```

**FQN comparison sites (3 in RegistrationSelector.cs, 1 in DependencySetValidator.cs):**
```csharp
// Current:
attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute"

// Target:
AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute)
```

Note: `IsAttribute()` already uses `ToDisplayString()` internally, so this is a 1:1 behavioral replacement.

### ReportDiagnosticDelegate Pattern

**Existing definition in ServiceRegistrationGenerator.RegisterAs.cs:**
```csharp
private delegate void ReportDiagnosticDelegate(Diagnostic diagnostic);
```

**Usage pattern -- accept delegate instead of SourceProductionContext:**
```csharp
private static void ValidateSomething(
    INamedTypeSymbol classSymbol,
    ReportDiagnosticDelegate reportDiagnostic)
{
    var diagnostic = Diagnostic.Create(descriptor, location, args);
    reportDiagnostic(diagnostic);
}

// Called from context:
ValidateSomething(classSymbol, context.ReportDiagnostic);
```

**Best candidates for adoption** (Claude's discretion -- recommendation):
1. **DiagnosticRules.cs** -- has 10+ `context.ReportDiagnostic()` calls across multiple validation methods; high testability benefit
2. **DependencyUsageValidator.cs** -- has 8+ `context.ReportDiagnostic()` calls; complex validation logic benefits from delegate isolation
3. **MissedOpportunityValidator.cs** -- 1 call, but clean small validator that sets a good pattern
4. **RegistrationEmitter.cs** -- 1 call in diagnostic path

**Recommendation:** Create a shared delegate type (not file-private) since it will be used across multiple files. Place in `Utilities/` or `Diagnostics/`:
```csharp
// In IoCTools.Generator/Utilities/ or Diagnostics/
internal delegate void ReportDiagnosticDelegate(Diagnostic diagnostic);
```

### Anti-Patterns to Avoid
- **Changing diagnostic IDs** -- this is a breaking change for consumers who suppress diagnostics by ID
- **Changing default severities** -- same concern; only change category and helpLinkUri
- **Bulk find-replace without test verification** -- each descriptor file change must pass existing tests

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| HelpLinkUri URL generation | String concatenation per-descriptor | Constant prefix + ID pattern | 87 URLs must be consistent; a helper or constant avoids typos |
| Diagnostic category constants | Inline strings per-descriptor | Static constants or a helper | 87 category assignments must be consistent across files |
| RegisterAsAll detection | Ad-hoc `.Name ==` string comparisons | `AttributeTypeChecker.IsAttribute()` | Already exists, tested, handles edge cases |

**Key insight:** The `DiagnosticDescriptorFactory.WithSeverity` method already preserves `HelpLinkUri` when creating severity-overridden copies. No changes needed to the factory -- just adding `helpLinkUri` to base descriptors will flow through automatically.

## Common Pitfalls

### Pitfall 1: DiagnosticDescriptor Constructor Arg Ordering
**What goes wrong:** The 7-arg and 8-arg constructors are both valid; mixing up positional args silently compiles but produces wrong diagnostics.
**Why it happens:** The `description` (7th) and `helpLinkUri` (8th) are both `string?` so the compiler won't catch swaps.
**How to avoid:** Add `helpLinkUri` strictly as the 8th positional argument after the existing `description` string. Verify by checking that the existing description text stays in place.
**Warning signs:** Tests failing with unexpected diagnostic messages or empty HelpLinkUri values.

### Pitfall 2: Category String Must Match Exactly
**What goes wrong:** IDE filtering breaks if category strings have typos or inconsistent casing.
**Why it happens:** Category is a free-form string with no compile-time validation.
**How to avoid:** Define constants for the 5 category strings and reference them in all descriptors.
**Warning signs:** Diagnostics not appearing under expected IDE categories.

### Pitfall 3: RegisterAsAllAttribute Name vs FQN Comparison
**What goes wrong:** `AttributeTypeChecker.IsAttribute()` compares using `ToDisplayString()` (fully-qualified name), but the ad-hoc checks use `.Name` (short name without namespace).
**Why it happens:** `.Name == "RegisterAsAllAttribute"` matches the short name, while `IsAttribute(attr, RegisterAsAllAttribute)` matches the FQN.
**How to avoid:** Both approaches work for `RegisterAsAllAttribute` since the name is unique to IoCTools and `ToDisplayString()` will match. However, verify that no external assembly defines a type with the same short name. The FQN comparison is strictly more correct.
**Warning signs:** Tests failing for services with attributes from referenced assemblies.

### Pitfall 4: Exception Filter Syntax in netstandard2.0
**What goes wrong:** The `when` exception filter syntax `catch (Exception ex) when (ex is not OutOfMemoryException)` is C# 9+ syntax.
**Why it happens:** The generator targets netstandard2.0 but uses `LangVersion: latest` in the csproj.
**How to avoid:** Verify the generator project's `LangVersion` setting. The existing DiagnosticsRunner.cs already uses this pattern successfully, so it's confirmed to work.
**Warning signs:** Build errors in the generator project.

### Pitfall 5: CS8603 Fix Changing Behavior
**What goes wrong:** Fixing `GetValueOrDefault(id) ?? null` to just `GetValueOrDefault(id)` is a no-op refactor (ConcurrentDictionary.GetValueOrDefault already returns `default` which is null for reference types). But removing the `?? null` may surface the nullable return type more explicitly.
**How to avoid:** Ensure the method return type is `User?` (nullable) and add the `!` null-forgiving operator or proper null annotation.
**Warning signs:** New CS8603 warnings appearing elsewhere after the fix.

## Code Examples

### Adding HelpLinkUri to a Descriptor
```csharp
// Before (7-arg):
public static readonly DiagnosticDescriptor SingletonDependsOnScoped = new(
    "IOC012",
    "Singleton service depends on Scoped service",
    "Singleton service '{0}' depends on Scoped service '{1}'. ...",
    "IoCTools",
    DiagnosticSeverity.Error,
    true,
    "Fix the lifetime mismatch by: ...");

// After (8-arg with category + helpLinkUri):
public static readonly DiagnosticDescriptor SingletonDependsOnScoped = new(
    "IOC012",
    "Singleton service depends on Scoped service",
    "Singleton service '{0}' depends on Scoped service '{1}'. ...",
    "IoCTools.Lifetime",
    DiagnosticSeverity.Error,
    true,
    "Fix the lifetime mismatch by: ...",
    "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc012");
```

### Enhanced IOC012 Message (with CreateScope suggestion)
```csharp
public static readonly DiagnosticDescriptor SingletonDependsOnScoped = new(
    "IOC012",
    "Singleton service depends on Scoped service",
    "Singleton service '{0}' depends on Scoped service '{1}'. Singleton services cannot capture shorter-lived dependencies.",
    "IoCTools.Lifetime",
    DiagnosticSeverity.Error,
    true,
    "Fix the lifetime mismatch by: 1) Changing dependency '{1}' to [Singleton], "
    + "2) Changing this service to [Scoped] or [Transient], "
    + "3) Inject IServiceProvider and call CreateScope() to resolve '{1}' on demand, "
    + "or 4) Use a factory delegate Func<{1}> to create instances per-use.",
    "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc012");
```

### Enhanced IOC015 Message (with inheritance path)
```csharp
public static readonly DiagnosticDescriptor InheritanceChainLifetimeValidation = new(
    "IOC015",
    "Service lifetime mismatch in inheritance chain",
    "Service lifetime mismatch in inheritance chain: {0} ({1}) -> {2}. "
    + "The full inheritance path is: {3}",
    "IoCTools.Lifetime",
    DiagnosticSeverity.Error,
    true,
    "Fix the inheritance lifetime hierarchy by: ...",
    "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc015");
```

Note: The IOC015 message format change requires updating the `Diagnostic.Create()` call sites that populate `{0}`, `{1}`, etc. to include the full path string (e.g., `"A -> B -> C"`).

### docs/diagnostics.md Entry Format
```markdown
## IOC012

**Severity:** Error
**Category:** IoCTools.Lifetime

**Cause:** A Singleton service declares a dependency on a Scoped service, which would capture a short-lived instance.

**Fix:** Change the dependency to Singleton, change the consumer to Scoped/Transient, or inject IServiceProvider and use CreateScope().
```

### Centralizing RegisterAsAll Check
```csharp
// Before (ad-hoc):
.Any(attr => attr.AttributeClass?.Name == "RegisterAsAllAttribute");

// After (centralized):
.Any(attr => AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute));
```

### Exception Handling Fix (ConstructorGenerator)
```csharp
// Before:
catch (Exception)
{
    return "";
}

// After:
catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
{
    // Emit diagnostic so the error is visible in build output
    // Note: ConstructorGenerator doesn't have SourceProductionContext here,
    // so we log to a collection or use a different reporting mechanism
    return "";
}
```

**Important:** ConstructorGenerator.cs line 437 is inside `GenerateConstructorCode()` which does NOT have a `SourceProductionContext` parameter. The diagnostic emission must either: (a) bubble the exception up to a caller that has context, or (b) use a different mechanism. The DiagnosticsRunner pattern requires `SourceProductionContext`. Check the call chain to determine the right approach.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Generic `"IoCTools"` category | Prefixed categories like `Microsoft.Design` | Industry standard | Better IDE filtering |
| No help links | HelpLinkUri on every diagnostic | Roslyn convention since 1.x | Clickable links in VS Error List |
| Bare catch blocks | Exception filters with diagnostic emission | .NET analyzer best practice | Visible errors without build crashes |

**Deprecated/outdated:**
- IOC010 is already marked `[Obsolete]` (consolidated into IOC014) -- still needs helpLinkUri and category update

## Open Questions

1. **ConstructorGenerator exception context**
   - What we know: Line 437 is inside `GenerateConstructorCode()` which returns a `string`. It does not receive `SourceProductionContext`.
   - What's unclear: Whether the caller of this method can catch and emit the diagnostic, or if the exception needs to be wrapped and reported differently.
   - Recommendation: During implementation, trace the call chain. The caller likely has `SourceProductionContext` and can emit IOC997 there. If not, store the exception message in the return value or throw a typed exception that the pipeline catches.

2. **ReportDiagnosticDelegate scope**
   - What we know: Currently file-private in `ServiceRegistrationGenerator.RegisterAs.cs`
   - What's unclear: Whether to keep it file-private (duplicate in each file) or create a shared type
   - Recommendation: Create a shared `internal delegate` in `IoCTools.Generator.Diagnostics` namespace since 3-4 validators will use it. Avoids duplication while keeping scope internal.

3. **IOC015 message format string changes**
   - What we know: The current message format has 3 placeholders (`{0}`, `{1}`, `{2}`)
   - What's unclear: How many callers create `Diagnostic.Create()` calls for IOC015 and whether adding `{3}` for the path breaks any existing call sites
   - Recommendation: Search all `InheritanceChainLifetimeValidation` usages during implementation and update format args consistently.

## Sources

### Primary (HIGH confidence)
- **Codebase inspection** -- all diagnostic descriptor files read directly
- **DiagnosticsRunner.cs** -- exception handling pattern verified at Generator/DiagnosticsRunner.cs lines 111-130
- **AttributeTypeChecker.cs** -- existing centralization target verified
- **DiagnosticDescriptorFactory.cs** -- confirmed `HelpLinkUri` preservation in `WithSeverity()`

### Secondary (MEDIUM confidence)
- **Roslyn DiagnosticDescriptor API** -- constructor overloads confirmed via existing usage in `DiagnosticDescriptorFactory.WithSeverity()` which already passes `helpLinkUri` as 8th positional arg (line 25)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, entirely within existing code
- Architecture: HIGH -- all patterns already exist in codebase, just need wider adoption
- Pitfalls: HIGH -- identified from direct code inspection of the exact files to modify

**Research date:** 2026-03-21
**Valid until:** 2026-06-21 (stable -- no external dependencies changing)
