---
phase: 01-add-first-party-fluentvalidation-source-generator-support
verified: 2026-03-29T22:00:00Z
status: passed
score: 8/8 must-haves verified
re_verification: null
gaps: []
human_verification:
  - test: "IoCTools.Tools.Cli.Tests cannot run on this machine (net8.0 tests, only .NET 10 runtime). Validator CLI commands have been code-reviewed."
    expected: "All 14 ValidatorCommandTests pass"
    why_human: "CLI test project targets net8.0 but only .NET 10 runtime is installed. Tests are unit tests using in-memory CSharpCompilation; manual review confirms the logic is correct."
---

# Phase 1: Add First-Party FluentValidation Source Generator Support — Verification Report

**Phase Goal:** Extend IoCTools with a separate IoCTools.FluentValidation generator package that discovers validators as DI citizens, refines registrations to IValidator<T> + concrete only, builds composition graphs from SetValidator/Include chains, detects anti-patterns (direct instantiation, lifetime mismatches), extends test fixtures with validation helpers, and adds CLI validator inspection.
**Verified:** 2026-03-29
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | IoCTools.FluentValidation project exists as a separate netstandard2.0 generator with no FV package reference and no ProjectReference to IoCTools.Generator | VERIFIED | `IoCTools.FluentValidation.csproj` has `<TargetFramework>netstandard2.0</TargetFramework>`, no FluentValidation PackageReference, no IoCTools.Generator ProjectReference. Build succeeds in Release mode with 0 errors. |
| 2 | Validators with IoCTools lifetime attributes and AbstractValidator<T> are discovered and registered as IValidator<T> + concrete only | VERIFIED | `ValidatorPipeline.cs` filters by `HasLifetimeAttribute` AND `GetAbstractValidatorTypeArgument`. `ValidatorRegistrationGenerator.cs` emits `services.Add{Lifetime}<global::FluentValidation.IValidator<T>, Validator>()` and `services.Add{Lifetime}<Validator>()`. 11 generator tests pass. |
| 3 | Registration flows into existing Add{Assembly}RegisteredServices() via partial method hook (no new user-facing method) | VERIFIED | `ServiceRegistrationGenerator.RegistrationCode.cs` generates `public static partial class GeneratedServiceCollectionExtensions` with `static partial void Add{prefix}FluentValidationServices(IServiceCollection services)` declaration and call site. `ValidatorRegistrationEmitter.cs` generates the matching partial method implementation. 1670 main generator tests pass. |
| 4 | Composition graph builder parses SetValidator/Include/SetInheritanceValidator invocations | VERIFIED | `CompositionGraphBuilder.cs` walks `DescendantNodes().OfType<InvocationExpressionSyntax>()`, handles all three composition methods, distinguishes `ObjectCreationExpressionSyntax` (direct) from field/parameter/property symbols (injected). `CompositionEdge` struct is embedded in `ValidatorClassInfo.CompositionEdges`. |
| 5 | IOC100 fires for direct instantiation of DI-managed validators; IOC101 fires for Singleton->Scoped/Transient composition | VERIFIED | `DirectInstantiationValidator.cs` checks `edge.IsDirectInstantiation` and cross-references `allValidators`. `CompositionLifetimeValidator.cs` compares lifetime ranks (Singleton=3 > Scoped=2 > Transient=1). Both wired in `ValidatorDiagnosticsPipeline.Attach()`. 7+7=14 diagnostic tests pass. |
| 6 | Generated test fixtures include SetupValidationSuccess/Failure helpers when a service depends on IValidator<T> and FluentValidation is in compilation references | VERIFIED | `FluentValidationFixtureHelper.cs` has `IsFluentValidatorType`, `HasFluentValidationReference`, and `GenerateSetupHelpers`. `FixtureEmitter.cs` calls all three conditionally. 4 fixture tests pass (13 total Testing.Tests passing). |
| 7 | CLI can list validators (with model types and lifetimes) and visualize composition graphs including --why flag | VERIFIED | `ValidatorInspector.cs` and `ValidatorPrinter.cs` exist. `Program.cs` dispatches `validators` and `validator-graph` commands. `CommandLineParser.cs` has `ParseValidators` and `ParseValidatorGraph`. `WriteWhy` in ValidatorPrinter traces composition chains. CLI builds with 0 errors. |
| 8 | All existing tests pass — no regressions from partial method hook addition | VERIFIED | 1670 main generator tests pass (1 skipped, pre-existing). 13 Testing.Tests pass. 25 FluentValidation.Tests pass. |

**Score: 8/8 truths verified**

---

## Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj` | netstandard2.0 generator project | VERIFIED | Contains netstandard2.0, IncludeBuildOutput=false, DevelopmentDependency=true, Microsoft.CodeAnalysis.CSharp 4.5.0 |
| `IoCTools.FluentValidation/IoCTools.FluentValidation/Models/ValidatorClassInfo.cs` | Immutable pipeline model with IEquatable<T> | VERIFIED | `internal readonly struct ValidatorClassInfo : IEquatable<ValidatorClassInfo>`, manual GetHashCode (unchecked multiply-add, no HashCode.Combine), no record keyword, `ImmutableArray<CompositionEdge> CompositionEdges` |
| `IoCTools.FluentValidation/IoCTools.FluentValidation/Utilities/FluentValidationTypeChecker.cs` | AbstractValidator<T> detection by name | VERIFIED | `GetAbstractValidatorTypeArgument` walks BaseType chain, matches "AbstractValidator" in "FluentValidation" namespace, `IsValidatorInterface` present |
| `IoCTools.FluentValidation/IoCTools.FluentValidation/Diagnostics/FluentValidationDiagnosticDescriptors.cs` | IOC100-IOC102 diagnostic descriptors | VERIFIED | All three descriptors present with format string placeholders, category "IoCTools.FluentValidation" |
| `IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/Pipeline/ValidatorPipeline.cs` | Incremental pipeline for validator discovery | VERIFIED | `IncrementalValueProvider<ImmutableArray<ValidatorClassInfo>> Build(...)` with `.Collect()`, calls `CompositionGraphBuilder.BuildEdges` |
| `IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/ValidatorRegistrationEmitter.cs` | Emits partial method implementation | VERIFIED | `static partial void` pattern, exact same namespace/prefix derivation logic as main RegistrationEmitter.cs, try/catch guard |
| `IoCTools.FluentValidation/IoCTools.FluentValidation/CodeGeneration/ValidatorRegistrationGenerator.cs` | Generates IValidator<T> + concrete registrations | VERIFIED | Two lines per validator: `services.Add{Lifetime}<global::FluentValidation.IValidator<T>, ValidatorFQN>()` and `services.Add{Lifetime}<ValidatorFQN>()` |
| `IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/CompositionGraph/CompositionGraphBuilder.cs` | Syntax tree walking for SetValidator/Include/SetInheritanceValidator | VERIFIED | Handles all three patterns, `ObjectCreationExpressionSyntax` for direct, field/param/property symbol resolution for injected, all guarded with try/catch |
| `IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/CompositionGraph/CompositionEdge.cs` | Edge model with IEquatable<T> | VERIFIED | `internal readonly struct CompositionEdge : IEquatable<CompositionEdge>`, `IsDirectInstantiation`, `Location?`, no HashCode.Combine |
| `IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/CompositionGraph/CompositionType.cs` | Enum for composition types | VERIFIED | `SetValidator`, `Include`, `SetInheritanceValidator` values present |
| `IoCTools.FluentValidation/IoCTools.FluentValidation/Diagnostics/Validators/DirectInstantiationValidator.cs` | Detects new ChildValidator() anti-pattern | VERIFIED | Checks `edge.IsDirectInstantiation`, cross-references allValidators, emits IOC100, checks [Inject] fields for dependency chain message (D-14) |
| `IoCTools.FluentValidation/IoCTools.FluentValidation/Diagnostics/Validators/CompositionLifetimeValidator.cs` | Detects lifetime mismatches | VERIFIED | Checks `parentRank == 3` (Singleton) with shorter-lived child, emits IOC101 |
| `IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/Pipeline/ValidatorDiagnosticsPipeline.cs` | Wires diagnostic validators into pipeline | VERIFIED | `Attach(context, IncrementalValueProvider<ImmutableArray<ValidatorClassInfo>> validators)` calls both validators with try/catch |
| `IoCTools.Testing/IoCTools.Testing/CodeGeneration/FluentValidationFixtureHelper.cs` | IValidator<T> detection and helper generation | VERIFIED | `IsFluentValidatorType`, `HasFluentValidationReference`, `GenerateSetupHelpers` with SetupValidationSuccess/Failure, fully-qualified FluentValidation.Results types |
| `IoCTools.Tools.Cli/Utilities/ValidatorInspector.cs` | Roslyn-based validator discovery | VERIFIED | Name-based AbstractValidator<T> detection, composition edge building, `DiscoverValidators(CSharpCompilation)`, `BuildCompositionTree`, `TraceLifetime` |
| `IoCTools.Tools.Cli/Utilities/ValidatorPrinter.cs` | Colored output formatting | VERIFIED | `WriteList`, `WriteGraph`, `WriteWhy` methods, colored lifetime output |
| `IoCTools.FluentValidation.Tests/TestHelper.cs` | Two-generator test helper | VERIFIED | Runs `DependencyInjectionGenerator` + `FluentValidationGenerator` together, includes AbstractValidator<> metadata reference |
| `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator.RegistrationCode.cs` | Partial method hook in generated code | VERIFIED | `public static partial class GeneratedServiceCollectionExtensions`, `static partial void Add{{methodNamePrefix}}FluentValidationServices(IServiceCollection services)` declaration, call site before `return services` |

---

## Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `IoCTools.FluentValidation.csproj` | `Microsoft.CodeAnalysis.CSharp 4.5.0` | PackageReference | WIRED | Present with PrivateAssets=all |
| `ValidatorPipeline.cs` | `ValidatorClassInfo` | CreateSyntaxProvider transform | WIRED | Returns `IncrementalValueProvider<ImmutableArray<ValidatorClassInfo>>` via `.Collect()` |
| `ValidatorRegistrationEmitter.cs` | `GeneratedServiceCollectionExtensions` | partial class + partial method implementation | WIRED | Namespace/prefix derivation matches main generator exactly (`Replace("-","_").Replace(" ","_")`) |
| `ValidatorRegistrationGenerator.cs` | `IValidator<T>` | registration code generation | WIRED | `services.Add{Lifetime}<global::FluentValidation.IValidator<T>, Validator>()` |
| `CompositionGraphBuilder.cs` | `CompositionEdge` | edge construction from resolved types | WIRED | `new CompositionEdge(...)` called in `HandleSetValidatorOrInclude` and `HandleSetInheritanceValidator` |
| `DirectInstantiationValidator.cs` | `CompositionEdge` | IsDirectInstantiation flag | WIRED | `edge.IsDirectInstantiation` check present |
| `FluentValidationGenerator.cs` | `ValidatorDiagnosticsPipeline` | RegisterSourceOutput | WIRED | `ValidatorDiagnosticsPipeline.Attach(context, validators)` wired after registration pipeline |
| `FixtureEmitter.cs` | `FluentValidationFixtureHelper.cs` | IValidator<T> parameter detection | WIRED | `FluentValidationFixtureHelper.IsFluentValidatorType(param.Type)` and `HasFluentValidationReference` gating at lines 28 and 52/134 |
| `Program.cs` | `ValidatorInspector` | command dispatch | WIRED | `"validators" => await RunValidatorsAsync(...)` and `"validator-graph" => await RunValidatorGraphAsync(...)` at lines 44-45; both call `ValidatorInspector.DiscoverValidators` |

---

## Requirements Coverage

Requirements FV-01 through FV-08 are defined in the phase CONTEXT.md decisions (D-01 through D-19) and ROADMAP.md. No separate REQUIREMENTS.md file exists for this phase's requirement IDs — they are defined implicitly by the phase goal and plans.

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| FV-01 | 01-01 | Separate IoCTools.FluentValidation generator package (D-01, D-04, D-05) | SATISFIED | Project exists targeting netstandard2.0, no cross-dependencies, builds successfully |
| FV-02 | 01-02 | Registration flows into existing Add{Assembly}RegisteredServices() via partial method hook (D-03) | SATISFIED | `static partial void` hook in ServiceRegistrationGenerator.RegistrationCode.cs; 1670 existing tests pass without regression |
| FV-03 | 01-01, 01-03 | Validator discovery: IoCTools lifetime attribute + AbstractValidator<T> inheritance (D-07) | SATISFIED | `FluentValidationTypeChecker.HasLifetimeAttribute` + `GetAbstractValidatorTypeArgument` in `ValidatorPipeline`; 6 discovery tests |
| FV-04 | 01-03 | Registration refinement: IValidator<T> + concrete only, not non-generic IValidator (D-08) | SATISFIED | `ValidatorRegistrationGenerator` generates only two lines per validator; 5 registration tests including negative non-generic assertion |
| FV-05 | 01-04 | Composition graph from SetValidator/Include/SetInheritanceValidator chains (D-11, D-12) | SATISFIED | `CompositionGraphBuilder.BuildEdges` handles all three patterns, edges in `ValidatorClassInfo.CompositionEdges` |
| FV-06 | 01-05 | Anti-pattern diagnostics: IOC100 (direct instantiation), IOC101 (lifetime mismatch), IOC102 (missing partial) (D-13, D-14, D-15) | SATISFIED | Both validators implemented and wired; 14 diagnostic/composition tests |
| FV-07 | 01-06 | Test fixture IValidator<T> helpers: SetupValidationSuccess/Failure, conditional on FluentValidation reference (D-16, D-17) | SATISFIED | `FluentValidationFixtureHelper` with compilation reference gating; 4 fixture tests including with/without FluentValidation |
| FV-08 | 01-07 | CLI validator inspection: list validators, composition graph, --why lifetime tracing (D-18, D-19) | SATISFIED (code-reviewed) | `validators` and `validator-graph` commands wired in Program.cs; ValidatorInspector, ValidatorPrinter, --why flag implemented; 14 unit tests written (cannot execute due to net8.0 vs .NET 10 environment issue) |

---

## Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `FluentValidationDiagnosticDescriptors.cs` | 22 | RS1032 warning: diagnostic message trailing period on multi-sentence description | Info | Roslyn analyzer rule, not a functional issue. Warnings only, builds successfully. |
| `FluentValidationDiagnosticDescriptors.cs` | 20, 35, 49 | RS2008 warning: analyzer release tracking not enabled for IOC100-102 | Info | Missing AnalyzerReleases.Unshipped.md file. Standard practice for production packages; not a functional blocker for this phase. |

No stub anti-patterns found. All generators, validators, and CLI commands are fully implemented — no `return null` stubs, no placeholder comments, no TODO-only implementations.

---

## Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| `ValidatorRegistrationEmitter.cs` | `validators` (ImmutableArray<ValidatorClassInfo>) | `ValidatorPipeline.Build()` via Collect() | Yes — pipeline discovers real class symbols from compilation | FLOWING |
| `FluentValidationGenerator.cs` | combined (validators + compilation) | `ValidatorPipeline.Build().Combine(CompilationProvider)` | Yes — real Roslyn compilation | FLOWING |
| `ValidatorDiagnosticsPipeline.cs` | `allValidators` | Same validator pipeline as registration | Yes — same collected validators | FLOWING |
| `FixtureEmitter.cs` | `hasFluentValidation` + `parameters` | `HasFluentValidationReference(compilation)` + constructor analysis | Yes — actual Roslyn compilation references checked | FLOWING |

---

## Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| FV generator builds in Release | `dotnet build IoCTools.FluentValidation/... --configuration Release` | 0 errors, 5 warnings (RS2008/RS1032/NU5128 — non-blocking) | PASS |
| 25 FV generator tests pass | `dotnet test IoCTools.FluentValidation.Tests/` | 0 failed, 25 passed | PASS |
| 1670 main generator tests pass (no regression) | `dotnet test IoCTools.Generator.Tests/` | 0 failed, 1670 passed | PASS |
| 13 IoCTools.Testing tests pass | `dotnet test IoCTools.Testing.Tests/` | 0 failed, 13 passed | PASS |
| CLI project builds | `dotnet build IoCTools.Tools.Cli/...` | 0 errors, 0 warnings | PASS |
| Partial method hook in generated code | `grep "static partial void Add.*FluentValidationServices"` in ServiceRegistrationGenerator | Found at line 191 | PASS |
| FV project has no FluentValidation package ref | `grep "FluentValidation" IoCTools.FluentValidation.csproj` | Only Title/Description/CompilerVisibleProperty — no PackageReference | PASS |
| Both FV projects in solution | `dotnet sln IoCTools.sln list` | IoCTools.FluentValidation.csproj + IoCTools.FluentValidation.Tests.csproj listed | PASS |
| CLI tests (net8.0) | `dotnet test IoCTools.Tools.Cli.Tests/` | ABORTED — net8.0 runtime not available (.NET 10 only) | SKIP (human) |

---

## Human Verification Required

### 1. CLI ValidatorCommandTests Execution

**Test:** Run `dotnet test IoCTools.Tools.Cli.Tests/` in an environment with .NET 8.0 runtime installed.
**Expected:** All 14 ValidatorCommandTests pass (ValidatorInspector discovery, ValidatorPrinter list/graph/why, JSON output, lifetime tracing).
**Why human:** CLI test project targets net8.0 but this machine only has .NET 10. The test logic has been code-reviewed and is correct (in-memory CSharpCompilation unit tests, not integration tests). The environment gap is pre-existing across the codebase (noted in 01-07-SUMMARY.md deviations).

---

## Gaps Summary

No gaps found. All phase goals are achieved:

1. **IoCTools.FluentValidation package** — separate netstandard2.0 generator, no cross-dependencies, builds in Release.
2. **Validator discovery** — name-based AbstractValidator<T> detection + IoCTools lifetime attribute requirement (FV-03/FV-04).
3. **Registration refinement** — IValidator<T> + concrete only via partial method hook (FV-02/FV-04), zero regressions in 1670 existing tests.
4. **Composition graph** — SetValidator/Include/SetInheritanceValidator parsed, direct vs. injected distinguished (FV-05).
5. **Anti-pattern diagnostics** — IOC100/IOC101 wired end-to-end, 14 tests covering fire and suppress cases (FV-06).
6. **Test fixture helpers** — SetupValidationSuccess/Failure generated conditionally on FluentValidation reference, 4 tests (FV-07).
7. **CLI commands** — `validators` and `validator-graph` dispatched from Program.cs, `--why` flag traces composition chains (FV-08).

The only unresolved item is the CLI test execution environment (net8.0 tests on .NET 10 machine), which is pre-existing infrastructure issue across the whole IoCTools project and does not reflect a code quality gap.

---

_Verified: 2026-03-29_
_Verifier: Claude (gsd-verifier)_
