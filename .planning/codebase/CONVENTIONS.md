# Coding Conventions

**Analysis Date:** 2026-03-21

## Naming Patterns

**Files:**
- PascalCase for all `.cs` files matching the primary type they contain
- Partial class files use dot-separated suffixes: `ConstructorGenerator.cs`, `ConstructorGenerator.Parameters.cs`, `ConstructorGenerator.Rendering.cs`
- Test files named `{Subject}Tests.cs` exactly matching test class name
- Diagnostic descriptor files grouped by concern: `StructuralDiagnostics.cs`, `LifetimeDiagnostics.cs`, `RegistrationDiagnostics.cs`

**Classes:**
- PascalCase; sealed where possible (`public sealed class CliServicesCommandTests`)
- Internal static utility classes throughout generator: `TypeNameUtilities`, `LifetimeCompatibilityChecker`, `ServiceDependencyUtilities`
- Validators named `{Concern}Validator`: `CircularDependencyValidator`, `LifetimeDependencyValidator`, `ManualRegistrationValidator`
- Analyzers named `{Concern}Analyzer`: `DependencyAnalyzer`, `ConfigurationFieldAnalyzer`, `TypeAnalyzer`
- Generators named `{Concern}Generator`: `ConstructorGenerator`, `ServiceRegistrationGenerator`
- Pipeline classes named `{Stage}Pipeline`: `ServiceClassPipeline`, `RegistrationPipeline`, `DiagnosticsPipeline`

**Methods:**
- PascalCase for public/internal; camelCase for private locals
- `Get{X}` for retrieval, `Build{X}` for construction, `Validate{X}` for validation, `Generate{X}` for code generation
- `Has{X}` / `Is{X}` for boolean predicate methods and properties
- `Collect{X}`, `Compile{X}`, `Extract{X}` for aggregation

**Parameters / locals:**
- camelCase throughout
- Single-letter lambdas (`x`, `a`, `m`) acceptable in concise LINQ chains; prefer descriptive names in longer expressions

**Fields:**
- Private fields: `_camelCase` prefix (e.g., `_data`, `_className`, `_attributes`)
- Private static readonly: same `_camelCase` convention (e.g., `private static readonly SemaphoreSlim Gate`)
- Static readonly constants in `DiagnosticDescriptors` use PascalCase property names matching diagnostic titles

**Interfaces:**
- `I` prefix + PascalCase: `IServiceCollection`, `IConfiguration`, `INamedTypeSymbol`

**Enums:**
- PascalCase type name; PascalCase member names
- Example: `LifetimeViolationType.SingletonDependsOnScoped`, `ServiceLifetime.Singleton`
- XML doc comments on every enum member describing meaning

**Attributes:**
- `Attribute` suffix on class name; exposed without suffix in usage
- Sealed: `public sealed class ScopedAttribute : Attribute`
- `[AttributeUsage(...)]` always declared on attribute classes

## Code Style

**Formatting:**
- 4 spaces indentation (`.editorconfig`)
- UTF-8, LF line endings, final newline, no trailing whitespace
- `var` preferred for all types when apparent or built-in (`.editorconfig`: `csharp_style_var_*`)

**Namespaces:**
- File-scoped namespaces preferred: `namespace IoCTools.Generator.Utilities;`
- `using` directives inside namespace (`.editorconfig`: `csharp_using_directive_placement = inside_namespace`)
- System directives sorted first (`dotnet_sort_system_directives_first = true`)
- Blank line separates System usings from other using groups (`dotnet_separate_import_directive_groups = true`)

**Expression-bodied members:**
- Preferred for single-line methods and properties (`csharp_style_expression_bodied_methods = when_on_single_line`)
- Example: `public static string RepoRoot { get; } = LocateRepoRoot();`

**Null handling:**
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Null-forgiving operator `!` used only where caller has verified non-null
- Defensive null checks with early return in generator methods:
  ```csharp
  if (classDeclaration == null)
      throw new ArgumentNullException(nameof(classDeclaration));
  ```
- Null-conditional `?.` and null-coalescing `??` used freely in LINQ and property access chains

**Error handling in generator code:**
- `try/catch(Exception)` with silent skip used when loading optional assemblies (common in `SourceGeneratorTestHelper`)
- Validators always check `if (!diagnosticConfig.DiagnosticsEnabled) return;` first
- No exception propagation from source generator pipeline methods — emit diagnostics instead

## Import Organization

**Order:**
1. `System.*` namespaces (sorted, separated by blank line)
2. Third-party (`Microsoft.CodeAnalysis.*`, `Microsoft.Extensions.*`, `FluentAssertions`, etc.)
3. Project-internal namespaces (`IoCTools.Generator.*`, `IoCTools.Abstractions.*`)

**Path Aliases:**
- Not used; all imports via fully qualified namespace `using` directives
- Generator code uses short `using Utilities;` (relative to file namespace) internally

**Global usings:**
- `IoCTools.Generator.Tests/GlobalUsings.cs` declares `global using FluentAssertions;` and `global using Microsoft.CodeAnalysis;`
- `IoCTools.Generator.Tests.csproj` adds `global using Xunit;` via `<Using Include="Xunit"/>`

## Error Handling

**Generator pipeline:**
- All validators receive `DiagnosticConfiguration` and short-circuit via `if (!config.DiagnosticsEnabled) return;`
- Diagnostics emitted via `context.ReportDiagnostic(Diagnostic.Create(...))` — never throw from generator
- `ConstructorGenerator` returns empty string `""` for non-partial classes rather than emitting error from code generation; diagnostics handled separately by `IOC080`

**Model constructors:**
- Guard clauses with `ArgumentNullException` for required symbol parameters:
  ```csharp
  ClassSymbol = classSymbol ?? throw new ArgumentNullException(nameof(classSymbol));
  ```
- `ArgumentException` for invalid string values (e.g., lifetime validation in `ServiceRegistration`)

**CLI code:**
- Exceptions from `Program.Main` propagate naturally; `CliTestHost` captures exit code

## Logging

**Framework:** Not used in generator or abstractions code (compile-time tools produce diagnostics, not logs)

**Tests:** `services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))` in `BuildServiceProvider` helper for runtime integration tests

## Comments

**When to Comment:**
- `<summary>` XML docs on all public and internal-public static methods and classes
- Inline `//` comments explaining non-obvious logic, generator decisions, and defensive patterns
- `// CRITICAL:`, `// FIXED:`, `// Defensive null checks` markers common in test helper and generator
- `[Obsolete]` attribute with explanatory message for deprecated diagnostic descriptors

**JSDoc/TSDoc equivalent (XML docs):**
- `<summary>` required on public API surface and internal utilities
- `<param>`, `<returns>`, `<remarks>` used on methods with complex signatures
- Test classes use `<summary>` on class to describe test scope (common pattern)

**Sections in test files:**
- `#region` directives used to group related test methods (seen in `LifetimeCompatibilityCheckerTests.cs`, `EnhancedTestUtilities.cs`, `MSBuildDiagnosticConfigurationTests.cs`)
- Region names: `#region GetViolationType - Singleton Consumer`, `#region Test Infrastructure`, `#region Mock Objects`

## Function Design

**Size:**
- Generator validators tend to be 40-100 lines; large validators split into partial classes or helper static methods
- `ConstructorGenerator` split across 5 partial files: `ConstructorGenerator.cs`, `.ConfigBinding.cs`, `.Namespaces.cs`, `.Parameters.cs`, `.Rendering.cs`
- Test methods: typically 10-30 lines following Arrange/Act/Assert; longer for integration scenarios with inline source code strings

**Parameters:**
- Named parameters preferred for attribute constructors with more than 2 arguments
- `params string[]` used in CLI test host (`CliTestHost.RunAsync(params string[] args)`)
- Optional parameters with defaults used in public helpers: `bool includeSystemReferences = true`

**Return Values:**
- Tuple returns used for multi-value outputs: `(Dictionary<...> AllImplementations, HashSet<...> AllRegisteredServices, ...)` from `TypeAnalyzer.CollectTypesAndBuildMaps`
- `string` return from code generators (empty string signals skip)
- `bool` from predicate helpers (`Has...`, `Is...`, `Contains...`)

## Module Design

**Exports:**
- Generator internals use `internal static class` (never public)
- Public API surface confined to `IoCTools.Abstractions` project: attribute types and enumerations
- Test infrastructure uses `internal` with `InternalsVisibleTo` declared in `.csproj`

**Partial classes:**
- Source generator target classes must be `partial` — enforced by `IOC080` diagnostic
- `ConstructorGenerator` and `ServiceRegistrationGenerator` themselves split into partial files by concern (parameters, rendering, conditional, multi-interface, RegisterAs)

**Sealed classes:**
- Attribute types: always `sealed`
- Test classes: `sealed` when no inheritance expected (CLI tests use `sealed`)
- Generator utility classes: `static` (no instantiation)

**Structs:**
- `readonly struct` used for lightweight value carriers in generator pipeline: `ServiceClassInfo`
- Manual `IEquatable<T>` implementation required (no record structs — `netstandard2.0` constraint)

---

*Convention analysis: 2026-03-21*
