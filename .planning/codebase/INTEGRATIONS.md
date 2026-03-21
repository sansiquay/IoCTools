# External Integrations

**Analysis Date:** 2026-03-21

## APIs & External Services

**None detected.** IoCTools is a developer tooling library (source generator + CLI). It has no runtime external API integrations. Its purpose is compile-time code generation and static analysis.

## Data Storage

**Databases:** None - no database dependencies detected.

**File Storage:**
- Local filesystem only
- The CLI reads/writes generated source files from the compiler output path
- `GeneratorArtifactWriter` in `IoCTools.Tools.Cli/GeneratorArtifactWriter.cs` reads compiler-emitted files from disk
- `CompareRunner` in `IoCTools.Tools.Cli/Utilities/CompareRunner.cs` writes snapshot files to a user-specified output directory

**Caching:** None at runtime. The Roslyn incremental generator pipeline uses Roslyn's built-in incremental compilation caching internally.

## Authentication & Identity

**Auth Provider:** None - library has no authentication layer.

## Monitoring & Observability

**Error Tracking:** None detected.

**Logs:**
- CLI writes directly to `Console.Out` and `Console.Error` (no structured logging framework)
- Source generator emits Roslyn `Diagnostic` objects surfaced in the build output
- Sample app references `Microsoft.Extensions.Logging` for demonstrating logging injection patterns, not for IoCTools internal use

## CI/CD & Deployment

**Hosting:**
- NuGet.org (inferred from `PackageProjectUrl: https://github.com/nate123456/IoCTools` and `IsPackable=true`)
- Repository: `https://github.com/nate123456/IoCTools`

**CI Pipeline:** No CI configuration files detected (no `.github/workflows/`, `.azure-pipelines.yml`, etc.)

**Packaging:**
- `IoCTools.Abstractions` - NuGet package, `netstandard2.0`, version 1.4.0, auto-packed on build
- `IoCTools.Generator` - NuGet analyzer package, `netstandard2.0`, version 1.4.0, distributed as `analyzers/dotnet/cs/netstandard2.0`
- `IoCTools.Tools.Cli` - .NET global tool (`PackAsTool=true`, tool command `ioc-tools`), version 1.4.0, `net8.0`

## Environment Configuration

**Required env vars:** None. All configuration is via MSBuild project properties.

**MSBuild-based configuration (consumer-side):**
- `IoCToolsIgnoredTypePatterns` - Semicolon-separated glob patterns for cross-assembly interfaces
- `IoCToolsNoImplementationSeverity` - IOC001 severity (Error/Warning/Info/Hidden)
- `IoCToolsUnregisteredSeverity` - IOC002 severity
- `IoCToolsLifetimeValidationSeverity` - IOC003/IOC012/IOC015 severity
- `IoCToolsConditionalServiceValidationSeverity` - Conditional service diagnostic severity
- `IoCToolsPartialClassValidationSeverity` - Partial class diagnostic severity
- `IoCToolsBackgroundServiceValidationSeverity` - Background service diagnostic severity
- `IoCToolsInheritanceChainValidationSeverity` - Inheritance chain diagnostic severity
- `IoCToolsDisableDiagnostics` - Boolean to disable all validation diagnostics
- `EmitCompilerGeneratedFiles` / `CompilerGeneratedFilesOutputPath` - Debug output of generated files

**Secrets location:** Not applicable - no secrets used.

## Webhooks & Callbacks

**Incoming:** None.

**Outgoing:** None.

## Roslyn / MSBuild Integration

**Integration point:** The generator integrates with the C# compiler pipeline via the Roslyn `IIncrementalGenerator` interface, declared with `[Generator]` attribute on `DependencyInjectionGenerator` in `IoCTools.Generator/IoCTools.Generator/DependencyInjectionGenerator.cs`.

**MSBuild targets:** `IoCTools.Generator/build/IoCTools.Generator.targets` is bundled inside the NuGet package at `build/`. It uses `<CompilerVisibleProperty>` to pass MSBuild properties into the generator's `AnalyzerConfigOptions`.

**Workspace loading (CLI):** The CLI uses `Microsoft.Build.Locator` + `Microsoft.CodeAnalysis.Workspaces.MSBuild` to open `.csproj` files as Roslyn workspaces at runtime, enabling the `ioc-tools` command to analyze projects without requiring a full build.

## Third-Party Library Integrations (Sample Only)

**Mediator.Abstractions 2.1.7:**
- Used only in `IoCTools.Sample` to demonstrate `[SkipAssignableTypes]` pattern
- Shows how IoCTools handles interfaces from external packages (`IRequest`, `INotification`, etc.)
- Not a runtime dependency of the generator or abstractions packages

---

*Integration audit: 2026-03-21*
