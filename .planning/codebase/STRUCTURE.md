# Codebase Structure

**Analysis Date:** 2026-03-21

## Directory Layout

```
IoCTools/
├── IoCTools.Abstractions/           # Public attributes and enumerations NuGet package
│   ├── Annotations/                 # All attribute classes ([Scoped], [Singleton], [Transient], [Inject], etc.)
│   └── Enumerations/                # InstanceSharing, Lifetime, NamingConvention, RegistrationMode
├── IoCTools.Generator/              # Roslyn IIncrementalGenerator NuGet package (netstandard2.0)
│   └── IoCTools.Generator/          # Actual project root (double-nested)
│       ├── Analysis/                # DependencyAnalyzer, TypeAnalyzer, InjectFieldAnalyzer, etc.
│       ├── CodeGeneration/          # ConstructorGenerator (partial), ServiceRegistrationGenerator (partial)
│       ├── Diagnostics/             # DiagnosticDescriptors (partial), DiagnosticUtilities, Descriptors/, Configuration/, Helpers/
│       ├── Generator/               # Pipeline orchestration, emitters, rules, discovery
│       │   ├── Diagnostics/         # DiagnosticRules.cs facade
│       │   │   └── Validators/      # 20+ individual validator classes (one concern each)
│       │   ├── Intent/              # ServiceIntentEvaluator.cs
│       │   └── Pipeline/            # ServiceClassPipeline, RegistrationPipeline, DiagnosticsPipeline
│       ├── Models/                  # ServiceClassInfo, ServiceRegistration, InheritanceHierarchyDependencies, etc.
│       ├── Properties/              # Assembly attributes
│       └── Utilities/               # AttributeTypeChecker, GeneratorStyleOptions, TypeHelpers, etc.
├── IoCTools.Generator.Tests/        # xUnit test suite for generator (1650+ tests)
│   └── *.cs                         # One test file per feature/scenario
├── IoCTools.Tools.Cli/              # Developer CLI tool (dotnet tool)
│   ├── CommandLine/                 # Per-command runners (RegistrationPrinter, GraphPrinter, etc.)
│   └── Utilities/                   # Supporting utilities
├── IoCTools.Tools.Cli.Tests/        # xUnit tests for CLI tool
│   ├── GeneratorStubs/              # Fake generator types for isolated testing
│   ├── Infrastructure/              # Test infrastructure helpers
│   └── TestProjects/                # Real .NET projects used as test fixtures
│       ├── EmptyProject/
│       ├── FieldsProject/
│       ├── MultiTargetProject/
│       └── RegistrationProject/
├── IoCTools.Sample/                 # Integration test / feature demo project
│   ├── Configuration/               # Configuration model classes (AppSettings, EmailSettings, etc.)
│   ├── Controllers/                 # Sample ASP.NET-style controllers
│   ├── FrameworkStubs/              # Stub types for framework interfaces used in demos
│   ├── Interfaces/                  # Service contract interfaces
│   ├── Services/                    # 18 service example files (one per feature area)
│   └── generated/                   # Generator output (committed; EmitCompilerGeneratedFiles=true)
│       └── IoCTools.Generator/IoCTools.Generator.DependencyInjectionGenerator/
├── .github/workflows/               # CI workflow definitions
├── .planning/codebase/              # GSD architecture documents
├── IoCTools.sln                     # Solution file
├── global.json                      # SDK version pin
├── CLAUDE.md                        # Project instructions for Claude Code
├── ideas.md                         # Curated implementation backlog
└── README.md
```

## Directory Purposes

**`IoCTools.Abstractions/Annotations/`:**
- Purpose: Every attribute class that user code places on services
- Key files: `ScopedAttribute.cs`, `SingletonAttribute.cs`, `TransientAttribute.cs`, `InjectAttribute.cs`, `DependsOnAttribute.cs` (generic), `RegisterAsAttribute.cs`, `RegisterAsAllAttribute.cs`, `ConditionalServiceAttribute.cs`, `ExternalServiceAttribute.cs`, `InjectConfigurationAttribute.cs`, `SkipRegistrationAttribute.cs`

**`IoCTools.Abstractions/Enumerations/`:**
- Purpose: Enum types referenced by attribute constructors
- Key files: `InstanceSharing.cs` (Separate/Shared), `RegistrationMode.cs`, `NamingConvention.cs`, `Lifetime.cs`

**`IoCTools.Generator/IoCTools.Generator/Generator/`:**
- Purpose: Core generator orchestration — the `DependencyInjectionGenerator.cs` entry point lives here alongside the three pipeline files and the two emitter files
- Key files: `DependencyInjectionGenerator.cs`, `ServiceDiscovery.cs`, `RegistrationSelector.cs`, `RegistrationEmitter.cs`, `ConstructorEmitter.cs`, `DiagnosticRules.cs`, `DiagnosticsRunner.cs`

**`IoCTools.Generator/IoCTools.Generator/Generator/Pipeline/`:**
- Purpose: Incremental pipeline wiring — each file attaches one output to the shared `IncrementalValuesProvider<ServiceClassInfo>`
- Key files: `ServiceClassPipeline.cs`, `RegistrationPipeline.cs`, `DiagnosticsPipeline.cs`

**`IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/`:**
- Purpose: One validator class per diagnostic concern; called by `DiagnosticRules.cs`
- Pattern: Each validator is a static class with one public `Validate(...)` method
- Key files: `CircularDependencyValidator.cs`, `LifetimeDependencyValidator.cs`, `MissingPartialKeywordValidator.cs`, `ManualRegistrationValidator.cs`, `MissedOpportunityValidator.cs`, `BaseLifetimeConsistencyValidator.cs`, `DependencySetValidator.cs`

**`IoCTools.Generator/IoCTools.Generator/Analysis/`:**
- Purpose: Roslyn symbol analysis helpers used by both emitters and validators
- Key files: `DependencyAnalyzer.cs` (inheritance chain dependency collection), `TypeAnalyzer.cs` (IHostedService detection), `InjectFieldAnalyzer.cs`, `DependsOnFieldAnalyzer.cs`, `ConfigurationFieldAnalyzer.cs`, `CircularDependencyDetector.cs`, `ExternalServiceAnalyzer.cs`

**`IoCTools.Generator/IoCTools.Generator/CodeGeneration/`:**
- Purpose: Text generation — renders C# source strings from collected model data
- Key files: `ConstructorGenerator.cs` + partial files (`ConfigBinding`, `Namespaces`, `Parameters`, `Rendering`), `ServiceRegistrationGenerator.cs` + partials (`Conditional`, `MultiInterface`, `RegisterAs`, `RegistrationCode`, `Rendering`), `BaseConstructorCallBuilder.cs`

**`IoCTools.Generator/IoCTools.Generator/Models/`:**
- Purpose: Data transfer objects shared across analysis, codegen, and diagnostics layers
- Key files: `ServiceClassInfo.cs`, `ServiceRegistration.cs`, `InheritanceHierarchyDependencies.cs`, `DiagnosticConfiguration.cs`, `ConfigurationInjectionInfo.cs`, `ServiceClassInfo.cs`, `DependencySource.cs`

**`IoCTools.Generator/IoCTools.Generator/Utilities/`:**
- Purpose: Pure helper/utility classes — no side effects, no Roslyn output
- Key files: `AttributeTypeChecker.cs` (attribute name constants + checks), `GeneratorStyleOptions.cs` (MSBuild config), `TypeHelpers.cs`, `InterfaceDiscovery.cs`, `LifetimeCompatibilityChecker.cs`, `TypeSkipEvaluator.cs`, `DependencySetUtilities.cs`, `TypeNameUtilities.cs`, `Glob.cs`

**`IoCTools.Generator/IoCTools.Generator/Diagnostics/`:**
- Purpose: Descriptor definitions organized by category (partial classes) and configuration parsing
- Key files: `DiagnosticDescriptors.cs` (root partial), `DiagnosticUtilities.cs` (MSBuild config parser), `Descriptors/LifetimeDiagnostics.cs`, `Descriptors/DependencyDiagnostics.cs`, `Descriptors/ConfigurationDiagnostics.cs`, `Descriptors/RegistrationDiagnostics.cs`, `Descriptors/StructuralDiagnostics.cs`, `Descriptors/DiagnosticDescriptorFactory.cs`

**`IoCTools.Tools.Cli/CommandLine/`:**
- Purpose: One runner + printer pair per CLI command
- Key files: `CommandLineParser.cs`, `RegistrationPrinter.cs`, `GraphPrinter.cs`, `ExplainPrinter.cs`, `WhyPrinter.cs`, `DoctorPrinter.cs`, `DiagnosticRunner.cs`, `CompareRunner.cs`, `ProfilePrinter.cs`, `ConfigAuditPrinter.cs`, `UsagePrinter.cs`

**`IoCTools.Sample/Services/`:**
- Purpose: 18 demonstration files — each covers a distinct feature area and serves as an integration test
- Key files: `BasicUsageExamples.cs`, `ArchitecturalEnhancementsExamples.cs`, `InheritanceExamples.cs`, `RegisterAsExamples.cs`, `DiagnosticExamples.cs`, `CollectionInjectionExamples.cs`, `BackgroundServiceExamples.cs`, `ConfigurationInjectionExamples.cs`

**`IoCTools.Sample/generated/`:**
- Purpose: Committed generator output for visibility and regression detection
- Generated: Yes (by Roslyn at build time with `EmitCompilerGeneratedFiles=true`)
- Committed: Yes — intentionally committed so diffs surface generator output changes
- Key files: `ServiceRegistrations_IoCToolsSample.g.cs` (one registration extension method), `*_Constructor.g.cs` (one per service class)

## Key File Locations

**Entry Points:**
- `IoCTools.Generator/IoCTools.Generator/DependencyInjectionGenerator.cs`: Generator entry point — `Initialize()` method
- `IoCTools.Tools.Cli/Program.cs`: CLI entry point — command dispatch switch

**Configuration:**
- `IoCTools.Generator/IoCTools.Generator/Utilities/GeneratorStyleOptions.cs`: MSBuild property → behavior mapping
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/DiagnosticUtilities.cs`: Diagnostic severity MSBuild parsing
- `IoCTools.Generator/IoCTools.Generator/Utilities/AttributeTypeChecker.cs`: Canonical attribute FQN constants
- `IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj`: NuGet packaging config, targets file reference

**Core Logic:**
- `IoCTools.Generator/IoCTools.Generator/Analysis/DependencyAnalyzer.cs`: Inheritance hierarchy traversal for constructor deps
- `IoCTools.Generator/IoCTools.Generator/Generator/RegistrationSelector.cs`: Decides which `ServiceRegistration` objects to create per class
- `IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticsRunner.cs`: Top-level diagnostic orchestration with cross-assembly support
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorGenerator.cs`: Renders constructor source text

**Diagnostic Descriptors:**
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/` — five partial files, one per category

**Testing:**
- `IoCTools.Generator.Tests/SourceGeneratorTestHelper.cs`: Shared Roslyn compilation helper used by all test files
- `IoCTools.Generator.Tests/EnhancedTestUtilities.cs`: Additional test utilities
- `IoCTools.Tools.Cli.Tests/Infrastructure/`: CLI test infrastructure

## Naming Conventions

**Files:**
- Generator source files use `PascalCase.cs`
- Generated output files use `{Namespace}_{TypeName}_Constructor.g.cs` for constructors and `ServiceRegistrations_{SafeAssemblyName}.g.cs` for registration extensions
- Test files use `{FeatureArea}Tests.cs` or `{Specific}Tests.cs`
- Partial class files use `{ClassName}.{Purpose}.cs` (e.g., `ConstructorGenerator.Rendering.cs`)

**Directories:**
- `PascalCase` throughout

**Types:**
- Attribute classes: `{Name}Attribute` (e.g., `ScopedAttribute`)
- Validator classes: `{Concern}Validator` (e.g., `CircularDependencyValidator`)
- Pipeline classes: `{Stage}Pipeline` (e.g., `ServiceClassPipeline`)
- Printer/runner classes: `{Command}Printer` or `{Command}Runner` (e.g., `GraphPrinter`, `DiagnosticRunner`)
- Generator partials: `ServiceRegistrationGenerator.{Concern}.cs`

## Where to Add New Code

**New lifetime or registration attribute:**
- Attribute class: `IoCTools.Abstractions/Annotations/{AttributeName}Attribute.cs`
- FQN constant: `IoCTools.Generator/IoCTools.Generator/Utilities/AttributeTypeChecker.cs`
- Intent detection: `IoCTools.Generator/IoCTools.Generator/Generator/Pipeline/ServiceClassPipeline.cs` and `Generator/Intent/ServiceIntentEvaluator.cs`
- Registration handling: `IoCTools.Generator/IoCTools.Generator/Generator/RegistrationSelector.cs`

**New diagnostic (IOCxxx):**
- Descriptor: add to the appropriate partial in `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/` (LifetimeDiagnostics, DependencyDiagnostics, ConfigurationDiagnostics, RegistrationDiagnostics, or StructuralDiagnostics)
- Validator: new file in `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/{Concern}Validator.cs`
- Wiring: add a delegation method to `IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticRules.cs`
- Call site: `IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticsRunner.cs` in `ValidateDependenciesComplete()` or `ValidateAllServiceDiagnosticsWithReferencedTypes()`
- Tests: new file `IoCTools.Generator.Tests/{DiagnosticCode}{FeatureName}Tests.cs`
- Sample demo: `IoCTools.Sample/Services/DiagnosticExamples.cs`

**New generator analysis helper:**
- Location: `IoCTools.Generator/IoCTools.Generator/Analysis/{AnalyzerName}Analyzer.cs`

**New CLI command:**
- Command runner + printer: `IoCTools.Tools.Cli/CommandLine/{CommandName}Runner.cs` and `{CommandName}Printer.cs`
- Parser method: add `Parse{CommandName}` to `IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs`
- Dispatch: add case to the switch in `IoCTools.Tools.Cli/Program.cs`
- Tests: `IoCTools.Tools.Cli.Tests/{CommandName}Tests.cs`

**New utility:**
- Shared generator helpers: `IoCTools.Generator/IoCTools.Generator/Utilities/{UtilityName}Utilities.cs`

## Special Directories

**`IoCTools.Abstractions/debug-generated/`, `cg-app/`, `cg-infra/`, `generated/`, `generated_files/`, `generated-app/`, `GeneratedApi/`, `minimal-generated/`, `sample_generated/`, `simple-generated/`:**
- Purpose: Debug/scratch output directories from development sessions
- Generated: Yes
- Committed: Yes (appear committed but contain only generated artifacts used during development)

**`IoCTools.Sample/generated/`:**
- Purpose: Intentionally committed generator output for regression visibility
- Generated: Yes (Roslyn, via `EmitCompilerGeneratedFiles=true` in project)
- Committed: Yes

**`IoCTools.Tools.Cli.Tests/TestProjects/`:**
- Purpose: Real minimal .NET projects used as test fixtures for CLI integration tests; some contain `.ioc-tools/generated/` subdirectories with cached generator artifacts
- Generated: Partially (the `.ioc-tools/generated/` subdirs)
- Committed: Yes (project files and service files committed; generated artifacts also committed as test fixtures)

---

*Structure analysis: 2026-03-21*
