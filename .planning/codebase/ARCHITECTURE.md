# Architecture

**Analysis Date:** 2026-03-21

## Pattern Overview

**Overall:** Roslyn Incremental Source Generator + CLI Tooling

**Key Characteristics:**
- Zero runtime overhead — all code generation occurs at compile time via `IIncrementalGenerator`
- Three-pipeline architecture inside the generator: service discovery, constructor emission, and diagnostics emission run independently but share the same `IncrementalValuesProvider<ServiceClassInfo>`
- Attribute-driven service intent: a class is only processed when it carries recognized markers (lifetime attributes, `[Inject]`, `[DependsOn]`, `IHostedService`, or is a partial class with interfaces)
- Cross-assembly awareness in diagnostics: referenced assemblies that themselves reference `IoCTools.Abstractions` are scanned for registered types

## Layers

**Abstractions Layer:**
- Purpose: Public API — all attributes and enumerations consumed by user projects
- Location: `IoCTools.Abstractions/`
- Contains: `[Scoped]`, `[Singleton]`, `[Transient]`, `[Inject]`, `[DependsOn]`, `[RegisterAs]`, `[RegisterAsAll]`, `[ConditionalService]`, `[ExternalService]`, `[InjectConfiguration]`, enums (`InstanceSharing`, `RegistrationMode`, `NamingConvention`, `Lifetime`)
- Depends on: Nothing (no project references)
- Used by: Every consumer project, `IoCTools.Generator`, `IoCTools.Sample`

**Generator Layer:**
- Purpose: Roslyn `IIncrementalGenerator` — produces constructor C# files and a service registration extension method file at build time
- Location: `IoCTools.Generator/IoCTools.Generator/`
- Contains: Pipelines, emitters, code generators, analyzers, validators, models, utilities
- Depends on: `IoCTools.Abstractions`, `Microsoft.CodeAnalysis.CSharp` (4.5.0)
- Used by: Consumer projects (referenced as an analyzer/generator NuGet package)

**CLI Tooling Layer:**
- Purpose: Developer-facing command-line tool for inspecting, debugging, and auditing IoCTools-enabled projects
- Location: `IoCTools.Tools.Cli/`
- Contains: Commands (`fields`, `services`, `explain`, `graph`, `why`, `doctor`, `compare`, `profile`, `config-audit`), printers, artifact reader, summary builder
- Depends on: `IoCTools.Generator` (via `InternalsVisibleTo`), MSBuild workspace APIs
- Used by: Developers running `ioc-tools` CLI

**Sample / Integration Layer:**
- Purpose: Live demonstration project and integration test suite
- Location: `IoCTools.Sample/`
- Contains: 18 service example files covering every feature, generated output under `generated/`
- Depends on: `IoCTools.Abstractions`, `IoCTools.Generator` (via generator reference)

**Formal Test Layer:**
- Purpose: Automated unit and integration tests for generator behavior
- Location: `IoCTools.Generator.Tests/`, `IoCTools.Tools.Cli.Tests/`
- Contains: 100+ test files using Roslyn compilation helpers (`SourceGeneratorTestHelper.cs`)
- Depends on: `IoCTools.Generator` (via `InternalsVisibleTo`), xUnit, FluentAssertions

## Data Flow

**Build-time Code Generation:**

1. Roslyn triggers `DependencyInjectionGenerator.Initialize()` on every incremental build
2. `ServiceClassPipeline.Build()` scans all `TypeDeclarationSyntax` nodes via `SyntaxProvider.CreateSyntaxProvider`; for each class, checks for service intent (lifetime attrs, `[Inject]`, `[DependsOn]`, `IHostedService`, partial+interfaces) and constructs `ServiceClassInfo` (symbol + syntax + semantic model)
3. Duplicate symbols (multiple partial declarations) are deduplicated via `GroupBy(SymbolEqualityComparer.Default)`
4. Three independent pipelines attach to the `IncrementalValuesProvider<ServiceClassInfo>`:
   - **RegistrationPipeline** → `RegistrationEmitter.Emit()` → `ServiceRegistrationGenerator` → emits `ServiceRegistrations_{Assembly}.g.cs`
   - **Constructor pipeline** → `ConstructorEmitter.EmitSingleConstructor()` per service → `ConstructorGenerator` → emits `{Namespace}_{Type}_Constructor.g.cs`
   - **DiagnosticsPipeline** → `DiagnosticsRunner.EmitWithReferencedTypes()` → 20+ validators → Roslyn diagnostics (no source output)

**Registration Generation Sub-flow:**
1. `RegistrationSelector.GetServicesToRegisterForSingleClass()` determines registrations per class based on attribute combination
2. `ServiceRegistrationGenerator` renders `IServiceCollection` extension method with `AddScoped/AddSingleton/AddTransient` calls, conditional blocks for `[ConditionalService]`, and `services.Configure<T>` + `TryAddSingleton` for configuration bindings
3. Output: one `IServiceCollection` extension method per assembly named `Add{SafeAssemblyName}RegisteredServices(this IServiceCollection services, IConfiguration configuration)`

**Constructor Generation Sub-flow:**
1. `DependencyAnalyzer.GetConstructorDependencies()` traverses the inheritance chain bottom-up collecting `[Inject]` fields, `[DependsOn]` attributes, and `[InjectConfiguration]` fields from every level
2. Base vs. derived dependencies are separated — base deps appear as `base(d1, d2)` parameters; derived deps get `this._field = dN` assignments
3. `ConstructorGenerator.GenerateInheritanceAwareConstructorCodeCore()` renders the constructor partial class file

**State Management:**
- No runtime state — the generator is stateless; `ServiceClassInfo` is an immutable value struct used only within a single compilation pipeline run
- MSBuild properties (`build_property.*`) configure behavior via `GeneratorStyleOptions` and `DiagnosticConfiguration`, read fresh each build

## Key Abstractions

**`ServiceClassInfo`:**
- Purpose: Thin data transfer struct carrying everything the generator needs about one service class
- Location: `IoCTools.Generator/IoCTools.Generator/Models/ServiceClassInfo.cs`
- Pattern: Immutable value struct — `INamedTypeSymbol ClassSymbol`, `TypeDeclarationSyntax? ClassDeclaration`, `SemanticModel? SemanticModel`

**`ServiceRegistration`:**
- Purpose: Represents one `IServiceCollection.Add*<TInterface, TImpl>()` call to be emitted
- Location: `IoCTools.Generator/IoCTools.Generator/Models/ServiceRegistration.cs`
- Pattern: Validated value object — constructor throws on invalid lifetimes; holds `ClassSymbol`, `InterfaceSymbol`, `Lifetime`, `UseSharedInstance`, `HasConfigurationInjection`

**`InheritanceHierarchyDependencies`:**
- Purpose: Carries the result of walking the full inheritance chain for one class — separates base vs. derived dependencies for proper constructor generation
- Location: `IoCTools.Generator/IoCTools.Generator/Models/InheritanceHierarchyDependencies.cs`
- Pattern: Class with five typed collections: `AllDependencies`, `BaseDependencies`, `DerivedDependencies`, `RawAllDependencies` (with level), `AllDependenciesWithExternalFlag`

**`GeneratorStyleOptions`:**
- Purpose: MSBuild-configurable options controlling which types are skipped, implicit lifetime default
- Location: `IoCTools.Generator/IoCTools.Generator/Utilities/GeneratorStyleOptions.cs`
- Pattern: Sealed class built once per pipeline run via `From(AnalyzerConfigOptionsProvider, Compilation)` — reads `build_property.*` keys and supports glob patterns

**`DiagnosticConfiguration`:**
- Purpose: Per-project diagnostic severity overrides read from MSBuild properties
- Location: `IoCTools.Generator/IoCTools.Generator/Models/DiagnosticConfiguration.cs`
- Pattern: Mutable configuration parsed by `DiagnosticUtilities.GetDiagnosticConfiguration()` from `build_property.IoCTools*` keys

## Entry Points

**`DependencyInjectionGenerator`:**
- Location: `IoCTools.Generator/IoCTools.Generator/DependencyInjectionGenerator.cs`
- Triggers: Roslyn calls `Initialize()` on every incremental compilation
- Responsibilities: Wires `ServiceClassPipeline`, `RegistrationPipeline`, `ConstructorEmitter`, and `DiagnosticsPipeline` together — this file is intentionally ~18 lines

**`IoCTools.Tools.Cli/Program.cs`:**
- Location: `IoCTools.Tools.Cli/Program.cs`
- Triggers: CLI invocation (`ioc-tools <command> [args]`)
- Responsibilities: Dispatches to command runners (`fields`, `services`, `explain`, `graph`, `why`, `doctor`, `compare`, `profile`, `config-audit`); each runner creates a `ProjectContext` (MSBuild workspace), optionally a `GeneratorArtifactWriter` (reads `.ioc-tools/generated/` cached outputs), and calls an appropriate Printer

## Error Handling

**Strategy:** Two-tier error handling — generator exceptions are caught and converted to Roslyn diagnostics (IOC996/IOC997) so build failures surface cleanly; CLI exceptions are caught at `Program.Main` and written to `Console.Error`

**Patterns:**
- Generator: `catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)` → `GeneratorDiagnostics.Report(context, "IOC996", ...)` — OOM and StackOverflow are re-thrown directly
- Individual validators are wrapped in try/catch where one validator failure should not suppress downstream diagnostics (e.g., `DiagnosticsRunner.ValidateRedundantMemberNames`)
- CLI: top-level `catch (OperationCanceledException)` → exit 1; `catch (Exception)` → `Console.Error.WriteLine`; `DoctorPrinter` returns exit code 1 when any Error-severity diagnostic is present

## Cross-Cutting Concerns

**Logging:** None at runtime — generator is compile-time only. CLI uses `Console.WriteLine` / `Console.Error.WriteLine` directly.

**Validation:** `DiagnosticRules` is a facade class that delegates to ~20 individual validator classes in `Generator/Diagnostics/Validators/`. All validators operate on Roslyn symbols and emit via `SourceProductionContext.ReportDiagnostic()`.

**Configuration (MSBuild):** Generator behavior is fully controlled via MSBuild `<PropertyGroup>` entries. `GeneratorStyleOptions.From()` and `DiagnosticUtilities.GetDiagnosticConfiguration()` read these from `AnalyzerConfigOptionsProvider` with per-syntax-tree fallback.

**Deduplication:** Both service registrations and constructor emissions deduplicate by class symbol using `SymbolEqualityComparer.Default` to handle partial class declarations split across files.

---

*Architecture analysis: 2026-03-21*
