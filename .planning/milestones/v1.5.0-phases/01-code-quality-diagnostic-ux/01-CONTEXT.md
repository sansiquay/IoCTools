# Phase 1: Code Quality and Diagnostic UX - Context

**Gathered:** 2026-03-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Harden exception handling, centralize patterns, and polish diagnostic metadata across the existing codebase. No new features — this is stabilization and UX polish for the existing 87 diagnostics and generator internals.

</domain>

<decisions>
## Implementation Decisions

### HelpLinkUri strategy
- **D-01:** All 87 diagnostic descriptors get HelpLinkUri pointing to GitHub repo anchors: `https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#iocXXX`
- **D-02:** Single `docs/diagnostics.md` file with one anchor per diagnostic — no per-diagnostic files
- **D-03:** Lean content per anchor: diagnostic code, severity, one-line cause, one-line fix. Code examples and detailed guidance deferred to Phase 4 (Documentation)
- **D-04:** If the file is renamed/moved, the HelpLinkUri update is part of the same commit

### IDE category naming
- **D-05:** Categories use prefixed names matching the existing descriptor file organization:
  - `IoCTools.Lifetime` (LifetimeDiagnostics.cs)
  - `IoCTools.Dependency` (DependencyDiagnostics.cs)
  - `IoCTools.Configuration` (ConfigurationDiagnostics.cs)
  - `IoCTools.Registration` (RegistrationDiagnostics.cs)
  - `IoCTools.Structural` (StructuralDiagnostics.cs)
- **D-06:** Mapping is 1:1 with descriptor source files — no ambiguity about which diagnostic goes where

### Exception handling
- **D-07:** Replace bare `catch(Exception)` blocks with: emit internal diagnostic (IOC996/997) + return gracefully
- **D-08:** Use OOM/SOF filter: `catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)`
- **D-09:** Match the existing DiagnosticsRunner.cs pattern — user sees the error in build output but build isn't blocked
- **D-10:** Two sites to fix: ConstructorGenerator.cs line 437, ServiceRegistrationGenerator.RegistrationCode.cs lines 90-96

### RegisterAsAllAttribute centralization
- **D-11:** Route all 10+ ad-hoc `.Name == "RegisterAsAllAttribute"` checks through `AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute)`
- **D-12:** Also convert 3+ inline `.ToDisplayString()` FQN comparisons in RegistrationSelector.cs and DependencySetValidator.cs to use AttributeTypeChecker

### ReportDiagnosticDelegate adoption
- **D-13:** Expand ReportDiagnosticDelegate pattern to 3-4 more validators — currently only used in ServiceRegistrationGenerator.RegisterAs.cs

### Claude's Discretion
- Which 3-4 validators to adopt ReportDiagnosticDelegate in (pick based on complexity benefit)
- Exact IOC996/997 descriptor message text and description
- QUAL-02 delegate shape — whether to use the existing file-private delegate or create a shared abstraction
- IOC012/013 exact wording for IServiceProvider/CreateScope() suggestion
- IOC015 exact format for inheritance path display (e.g., "A -> B -> C" vs "A → B → C")
- IOC016-019 configuration example format in messages

</decisions>

<specifics>
## Specific Ideas

- Follow the convention of other analyzers for category prefixes (e.g., `Microsoft.Design`, `StyleCop.Naming`)
- DiagnosticsRunner.cs is the model pattern for exception handling — replicate it, don't invent a new approach
- The 8-argument `DiagnosticDescriptor` constructor adds `helpLinkUri` as the 8th parameter

</specifics>

<canonical_refs>
## Canonical References

### Diagnostic descriptors
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/LifetimeDiagnostics.cs` — 10 descriptors (IOC012-015, IOC033, IOC059-060, IOC075, IOC084, IOC087)
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/StructuralDiagnostics.cs` — 13 descriptors
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs` — 27 descriptors
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/DependencyDiagnostics.cs` — 27 descriptors
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/ConfigurationDiagnostics.cs` — 10 descriptors

### Exception handling sites
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorGenerator.cs` §437 — bare catch returning ""
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator.RegistrationCode.cs` §90-96 — conditional re-throw
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/DiagnosticsRunner.cs` §111-127 — model pattern to follow

### RegisterAsAllAttribute check locations (ad-hoc, to centralize)
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorEmitter.cs` §34
- `IoCTools.Generator/IoCTools.Generator/Analysis/TypeAnalyzer.cs` §180
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/BaseConstructorCallBuilder.cs` §199
- `IoCTools.Generator/IoCTools.Generator/Pipelines/ServiceClassPipeline.cs` §31
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/DiagnosticsRunner.cs` §315, §377
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Validators/MissedOpportunityValidator.cs` §31
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/ServiceRegistrationScan.cs` §43, §96
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/DiagnosticScan.cs` §59
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/RegistrationSelector.cs` §95-96, §159-160, §218-220 (inline FQN comparisons)
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Validators/DependencySetValidator.cs` §75 (inline FQN)

### ReportDiagnosticDelegate
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator.RegisterAs.cs` §10 — existing delegate definition

### Diagnostic message improvements
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/LifetimeDiagnostics.cs` — IOC012, IOC013, IOC015 messages to enhance
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/ConfigurationDiagnostics.cs` — IOC016-019 messages to enhance

### CS8603 fix
- `IoCTools.Sample/Services/MultiInterfaceExamples.cs` §53 — `GetValueOrDefault(id) ?? null` nullable return

### InstanceSharing.Separate comments
- `IoCTools.Abstractions/Annotations/RegisterAsAttribute.cs` — default parameter definitions
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator.RegisterAs.cs` §36 — inline comment
- `IoCTools.Sample/Services/RegisterAsExamples.cs` §331-332 — sample usage comments

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AttributeTypeChecker`: Already has `RegisterAsAllAttribute` constant and `IsAttribute()` method — just need to route all checks through it
- `DiagnosticDescriptorFactory.WithSeverity()`: Already preserves `HelpLinkUri` when creating severity-overridden copies — will work correctly once base descriptors have URIs
- `DiagnosticsRunner.cs` exception pattern: Proven pattern with OOM/SOF filter — copy directly

### Established Patterns
- Descriptor files organized by category (5 partial classes) — category assignment maps 1:1
- 7-argument `DiagnosticDescriptor` constructor used everywhere — needs migration to 8-argument form for `helpLinkUri`
- `AttributeTypeChecker.IsAttribute(attr, fullName)` is the canonical check pattern

### Integration Points
- `DiagnosticDescriptors` partial class (5 files) — all descriptors modified for category + helpLinkUri
- `docs/diagnostics.md` — new file, link target for all HelpLinkUris
- Validators using `SourceProductionContext.ReportDiagnostic()` — candidates for delegate adoption

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-code-quality-diagnostic-ux*
*Context gathered: 2026-03-21*
