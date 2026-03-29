# Phase 1: Add First-Party FluentValidation Source Generator Support - Context

**Gathered:** 2026-03-29
**Status:** Ready for planning

<domain>
## Phase Boundary

Extend IoCTools to understand FluentValidation validators as first-class DI citizens. Validators already work as services today (lifetime attributes + `AbstractValidator<T>` = registered, constructor-generated, tested). This phase closes the gaps: registration refinement, validator composition graph awareness, anti-pattern detection, test fixture helpers, and CLI integration.

This is NOT a FluentValidation linter. Every feature must relate to dependency injection, service registration, or the DI graph.

</domain>

<decisions>
## Implementation Decisions

### Architecture
- **D-01:** Separate `IoCTools.FluentValidation` NuGet package — keeps the FluentValidation dependency optional. Users who don't use FluentValidation pay nothing.
- **D-02:** No new abstractions package. Existing IoCTools attributes (`[Scoped]`, `[Singleton]`, `[Transient]`, `[Inject]`, `[RegisterAs<T>]`, etc.) are sufficient. Validators are services — no special attributes needed.
- **D-03:** No new user-facing registration method. Validator registrations flow into the existing `Add{Assembly}RegisteredServices()` via partial class/method coordination between the two generators. Single call site, zero ceremony.
- **D-04:** Target `netstandard2.0` — same constraints as `IoCTools.Generator` (no records, no init, no `HashCode`). If we want modern C#, we do it across the board.
- **D-05:** Generator is fully independent — no `ProjectReference` to `IoCTools.Generator`. Follows the `IoCTools.Testing` precedent (decision D-12 from Phase 03).
- **D-06:** FluentValidation NuGet dependency flows to consumers intentionally (like Moq in IoCTools.Testing). Pin to specific stable version.

### Discovery & Registration
- **D-07:** Discovery signal = existing IoCTools lifetime attribute + class inherits `AbstractValidator<T>`. The base class tells the generator what `T` is (via `BaseType.TypeArguments[0]`). No new attributes, no convention-only discovery.
- **D-08:** Registration refinement — register only `IValidator<T>` + concrete type for validators, NOT all interfaces. FluentValidation's own DI extensions deliberately skip `IValidator` (non-generic) and `IEnumerable<IValidationRule>`. IoCTools should match this behavior. This is a correctness fix — the current `InterfaceDiscovery` over-registers.
- **D-09:** `partial` required on validator classes (same as all IoCTools services — IOC080 enforces this).
- **D-10:** Validator lifetime determined by the IoCTools attribute on the class (`[Scoped]`, `[Singleton]`, `[Transient]`). No special defaults, no separate MSBuild property — validators are services, same rules.

### Validator Composition Graph
- **D-11:** Parse validator bodies to build a composition graph. Walk syntax trees for `SetValidator(...)`, `Include(...)`, and `SetInheritanceValidator(...)` invocations. Resolve type arguments and add edges to the DI dependency graph.
- **D-12:** This graph enables: lifetime constraint propagation through composition chains, richer anti-pattern diagnostics, and CLI visualization of how validators compose through DI.

### Anti-Pattern Detection (Diagnostics)
- **D-13:** Detect `SetValidator(new ChildValidator())` and `Include(new SharedRulesValidator())` where the instantiated type is a DI-managed service or has `[Inject]` fields. Diagnostic: "ChildValidator is instantiated directly but has DI dependencies that won't be resolved."
- **D-14:** Leverage composition graph for richer messages — show the full dependency chain being bypassed (e.g., "AddressValidator depends on Scoped AppDbContext which won't be injected").
- **D-15:** Follow existing diagnostic patterns: `DiagnosticDescriptors`, `{Concern}Validator`, configurable MSBuild severity.

### Test Fixtures
- **D-16:** Extend `IoCTools.Testing` fixture generation for `IValidator<T>` parameters. When a service depends on `IValidator<T>`, generate `SetupValidationSuccess()` and `SetupValidationFailure()` helpers using FluentValidation's `ValidationResult` API.
- **D-17:** Only generate FluentValidation-aware helpers when FluentValidation is detected in the compilation's references. Do NOT add FluentValidation as a hard dependency of IoCTools.Testing.

### CLI
- **D-18:** CLI validator inspection is in scope. Extend `ioc-tools` with validator-aware commands showing model-to-validator mapping, composition graph, and dependency chains.
- **D-19:** CLI should leverage the composition graph to answer questions like "why is this validator Scoped?" by tracing through SetValidator/Include chains to the root Scoped dependency.

### Out of Scope (explicitly)
- **D-20:** No validation rule analysis (property coverage, rule strength, async/sync detection, ruleset completeness). Those are FluentValidation linting, not DI.
- **D-21:** No empty validator scaffolding via source generation. Rules are business logic.
- **D-22:** No MediatR pipeline behavior auto-wiring. Separate framework concern.
- **D-23:** No rule generation from data annotations. Rules are business logic.

### Claude's Discretion
- Diagnostic ID numbering scheme (continuing IOC series or new FV series)
- Exact CLI command names and output format
- Internal architecture of the composition graph data structure
- How partial class/method coordination works between the two generators
- Registration refinement mechanism (FluentValidation-specific awareness vs. general-purpose interface filter)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### IoCTools Generator Architecture
- `IoCTools.Generator/IoCTools.Generator/DependencyInjectionGenerator.cs` — Main generator entry point, three-pipeline wiring pattern
- `IoCTools.Generator/IoCTools.Generator/Generator/Pipeline/ServiceClassPipeline.cs` — Service discovery pipeline, base class detection patterns (IHostedService precedent)
- `IoCTools.Generator/IoCTools.Generator/Generator/RegistrationEmitter.cs` — Registration orchestration, deduplication
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator.RegistrationCode.cs` — Generated AddServices() method body
- `IoCTools.Generator/IoCTools.Generator/Generator/RegistrationSelector.cs` — Per-class registration decisions, RegisterAs/RegisterAsAll paths
- `IoCTools.Generator/IoCTools.Generator/Generator/ServiceDiscovery.cs` — Lifetime attribute resolution, IHostedService base class detection

### Interface Discovery & Filtering
- `IoCTools.Generator/IoCTools.Generator/Generator/InterfaceDiscovery.cs` — Where interface registrations are collected. Filters `System.*` but nothing else — this is where D-08 needs a fix
- `IoCTools.Generator/IoCTools.Generator/Utilities/TypeSkipEvaluator.cs` — Type skip patterns (MediatR handlers, etc.)
- `IoCTools.Generator/IoCTools.Generator/Generator/Intent/ServiceIntentEvaluator.cs` — Service intent gate

### Diagnostics Infrastructure
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/` — Full diagnostic pattern: descriptors, validators, configuration
- `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/LifetimeDependencyValidator.cs` — Lifetime validation pattern reusable for validator composition chains

### Testing Generator (precedent for separate package)
- `IoCTools.Testing/IoCTools.Testing/IoCTools.TestingGenerator.cs` — Separate IIncrementalGenerator pattern
- `IoCTools.Testing/IoCTools.Testing/CodeGeneration/FixtureEmitter.cs` — Type-aware test fixture generation (IOptions<T> special handling at lines 87-110)
- `IoCTools.Testing/IoCTools.Testing/Analysis/ConstructorReader.cs` — Constructor parameter analysis

### Attributes & Abstractions
- `IoCTools.Abstractions/Annotations/` — All existing attribute definitions (no new attributes needed per D-02)
- `IoCTools.Generator/IoCTools.Generator/Utilities/AttributeTypeChecker.cs` — Attribute detection patterns

### Package Architecture Precedent
- `IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj` — Packaging model to follow (PrivateAssets on Roslyn, intentional flow of Moq)
- `IoCTools.Testing.Abstractions/IoCTools.Testing.Abstractions.csproj` — Shows the split pattern (though we're NOT creating an abstractions package per D-02)

### FluentValidation (external)
- FluentValidation DI documentation: https://docs.fluentvalidation.net/en/latest/di.html
- FluentValidation testing API: https://docs.fluentvalidation.net/en/latest/testing.html
- FluentValidation GitHub issues on DI: #2183 (lifetime), #2182 (duplicates), #2181 (internal types), #806 (DI composition)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ServiceClassPipeline` — Multi-stage filtering pipeline; composition graph parsing would add a new pipeline stage
- `LifetimeDependencyValidator` — Lifetime mismatch detection; extend for validator composition chain propagation
- `InterfaceDiscovery` — Interface filtering; needs modification for D-08 (validator-specific or general-purpose filter)
- `FixtureEmitter` — Type-aware fixture generation; extend with IValidator<T> detection for D-16
- `DiagnosticDescriptors` / `DiagnosticConfiguration` — Full diagnostic infrastructure ready for new validator diagnostics
- `TypeSkipEvaluator` — Type hierarchy detection patterns; `IsAssignableByName` can detect `AbstractValidator<T>`

### Established Patterns
- Attribute-driven discovery with explicit intent (no convention-only magic)
- Separate generator packages for optional features (IoCTools.Testing precedent)
- Struct-based immutable pipeline models with manual IEquatable<T> (netstandard2.0)
- Diagnostic validators short-circuit when diagnostics are disabled
- Generator never throws — emits diagnostics

### Integration Points
- `Add{Assembly}RegisteredServices()` — Partial class coordination point for FV generator contributions
- `InterfaceDiscovery.GetAllInterfacesForService()` — Where registration filtering for validators needs to hook in
- `DiagnosticsPipeline.Attach()` — Where new validator composition diagnostics attach
- CLI's `ProjectContext` / MSBuild workspace — Where validator CLI commands would plug in

</code_context>

<specifics>
## Specific Ideas

- "Who validates the validators?" — IoCTools validates DI correctness of validators at compile time
- Registration should be invisible — installing the package + adding IoCTools attributes to validators = full consent, no second setup call
- The validator composition graph (SetValidator/Include chains) is a first-class part of the DI graph, not a separate concern
- Existing IoCTools attributes are the API — new attributes only if existing set proves insufficient for validator-specific features

</specifics>

<deferred>
## Deferred Ideas

- FluentValidation linting (property coverage, rule strength, async/sync detection, ruleset completeness) — valuable but belongs in a FluentValidation-specific analyzer, not IoCTools
- MediatR ValidationBehavior auto-wiring — separate framework concern, one line of manual code
- Empty validator scaffolding via CLI (`ioc-tools scaffold-validator`) — could be a future CLI enhancement
- Modern C# across the board (records, init-only) — would require migrating all generators to a newer target, separate milestone
- Cross-assembly validator discovery — each assembly generates its own registrations, composes naturally via separate calls

</deferred>

---

*Phase: 01-add-first-party-fluentvalidation-source-generator-support*
*Context gathered: 2026-03-29*
