# CLAUDE.md

## Project Overview

IoCTools is a .NET source generator library for dependency injection. It auto-discovers services via lifetime attributes (`[Scoped]`, `[Singleton]`, `[Transient]`), generates constructors, validates DI config at build time (IOC001-IOC086), and includes a CLI (`ioc-tools`).

**Components:** Abstractions (attributes/enums) | Generator (source gen, diagnostics) | Sample (18 example files) | CLI (inspect/debug tool) | Tests (1650+ tests)

## Commands

```bash
dotnet build                            # Build all
dotnet build --configuration Release    # Release build
dotnet run --project IoCTools.Sample    # Run sample
dotnet pack                             # NuGet packages

# Tests
cd IoCTools.Generator.Tests && dotnet test   # 1650+ generator tests
cd IoCTools.Tools.Cli.Tests && dotnet test   # CLI tests
cd IoCTools.Sample && dotnet build            # Integration tests (diagnostic warnings expected)
```

## Critical Constraints

**Generator targets `netstandard2.0`** — these C# features are NOT available in generator code:
- No `record` types — use classes/structs with manual `IEquatable<T>` and `GetHashCode()` (no `HashCode` type)
- No `init` properties, `required` members, or advanced pattern matching
- Use `System.ValueTuple` for tuples

## Diagnostic Configuration

MSBuild properties in consumer `.csproj`:
```xml
<IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>  <!-- IOC001 -->
<IoCToolsManualSeverity>Info</IoCToolsManualSeverity>                      <!-- IOC002 -->
<IoCToolsLifetimeValidationSeverity>Warning</IoCToolsLifetimeValidationSeverity> <!-- IOC003/012/015 -->
<IoCToolsDisableDiagnostics>true</IoCToolsDisableDiagnostics>
<IoCToolsIgnoredTypePatterns>*.Abstractions.*;*.Contracts.*</IoCToolsIgnoredTypePatterns>
```

Severity options: `Error`, `Warning`, `Info`, `Hidden`. Full diagnostic table in `DiagnosticDescriptors.cs`.

## Key Patterns

- Services must be `partial` (enforced by IOC080)
- `[Inject]` fields get constructor params; `[DependsOn<T>]` for declarative deps
- `[RegisterAs<T>(InstanceSharing.Shared)]` for factory pattern (same instance across interfaces)
- `[RegisterAs<T>]` without lifetime = RegisterAs-only (e.g., EF DbContext registered externally)
- `[InjectConfiguration]` for config binding; `[ExternalService]` for cross-assembly deps
- `[RegisterAsAll]` with `RegistrationMode` for multi-interface registration
- Constructor gen: base deps first, then derived; indexed vars (`d1`, `d2`); `base()` chaining

## Architecture

- Entry: `DependencyInjectionGenerator.cs` (~18 lines, wires 3 pipelines)
- Pipelines: `ServiceClassPipeline` (discovery) → `RegistrationPipeline` + `ConstructorEmitter` + `DiagnosticsPipeline`
- Core model: `ServiceClassInfo` (immutable value struct, pipeline data carrier)
- Cross-assembly: referenced assemblies with IoCTools.Abstractions are scanned for registered types
- Generator never throws — emits diagnostics; `catch` guards around validators
- CLI: `Program.cs` dispatches to command runners via `ProjectContext` (MSBuild workspace)

## Architectural Limits

Intentional trade-offs (attempts caused 25-95 test regressions):
- Complex field access modifiers (protected/internal/public `[Inject]` fields)
- Advanced generic constraints (unmanaged, complex combinations)
- Deep config injection nesting with inheritance + generics
- Workaround: manual constructors or `[DependsOn]` alternatives

## Testing Conventions

- Use `InternalsVisibleTo` in `.csproj` for testing internal methods
- Cover all enum value combinations, null inputs, and invalid inputs
- Organize with `#region` directives; `sealed` test classes
- Arrange/Act/Assert pattern; FluentAssertions; xUnit

## Naming Conventions

- Files: PascalCase matching primary type; partials use dot-suffix (`ConstructorGenerator.Parameters.cs`)
- Tests: `{Subject}Tests.cs`; validators: `{Concern}Validator`; analyzers: `{Concern}Analyzer`
- Fields: `_camelCase`; methods: `Get/Build/Validate/Generate/Has/Is/Collect` prefixes
- Generator internals: `internal static class`; attributes: always `sealed`
- File-scoped namespaces; `using` inside namespace; `var` preferred

## Code Style

- 4 spaces, UTF-8, LF, final newline (see `.editorconfig`)
- Nullable reference types enabled
- Expression-bodied members when single-line
- No exception propagation from generator — emit diagnostics instead
- Validators short-circuit: `if (!config.DiagnosticsEnabled) return;`

<!-- GSD:project-start source:PROJECT.md -->
## Project

**IoCTools v1.5.0**

IoCTools is a .NET source generator library that simplifies dependency injection in .NET applications. It auto-discovers services via lifetime attributes, generates constructors, validates DI configuration at build time with 86 diagnostics (IOC001-IOC086), and includes a CLI for inspection/debugging. This milestone adds a test fixture generator, expands diagnostics, improves the CLI, and overhauls documentation.

**Core Value:** Eliminate DI boilerplate — both in production code (service registration, constructors) and now in test code (mock declarations, SUT construction) — with zero runtime overhead through compile-time source generation.

### Constraints

- **netstandard2.0**: Generator and Abstractions must maintain netstandard2.0 target for broad compatibility — no records, init-only properties, required members
- **IoCTools.Testing target**: Can target net8.0+ since it's test-project-only
- **Moq dependency**: IoCTools.Testing will take a dependency on Moq — version should align with common usage (latest stable)
- **Source generator limitations**: Generated test fixtures must work within Roslyn source generator constraints (no runtime reflection)
<!-- GSD:project-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd:quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd:debug` for investigation and bug fixing
- `/gsd:execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd:profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
