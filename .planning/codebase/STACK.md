# Technology Stack

**Analysis Date:** 2026-03-21

## Languages

**Primary:**
- C# (LangVersion: `latest` in generator/abstractions, `11.0` in test project, `default` in sample) - All projects

**Secondary:**
- MSBuild / `.targets` files - Build integration in `IoCTools.Generator/build/IoCTools.Generator.targets`

## Runtime

**Environment:**
- .NET 8.0 - Sample app (`IoCTools.Sample`), CLI tool (`IoCTools.Tools.Cli`), both test projects
- .NET Standard 2.0 - Generator (`IoCTools.Generator`) and abstractions (`IoCTools.Abstractions`) for broad framework compatibility

**Package Manager:**
- NuGet (dotnet CLI)
- Lockfile: Not present (no `packages.lock.json`)

**SDK Version:**
- .NET SDK 9.0.100 (pinned via `global.json` with `rollForward: latestFeature`)

## Frameworks

**Core:**
- Roslyn `IIncrementalGenerator` API - Source generator pipeline in `IoCTools.Generator/IoCTools.Generator/DependencyInjectionGenerator.cs`
- Microsoft.Extensions.DependencyInjection (6.x) - DI container integration used in tests and sample
- Microsoft.Extensions.Hosting (6.0.1) - `IHostedService` background service support in sample

**Testing:**
- xunit 2.9.3 - Test runner for `IoCTools.Generator.Tests`
- xunit 2.6.3 - Test runner for `IoCTools.Tools.Cli.Tests`
- FluentAssertions 6.12.0 - Assertion library in both test projects
- Xunit.SkippableFact 1.5.23 - Conditional test skipping in generator tests
- Microsoft.NET.Test.Sdk 17.8.0 / 17.9.0 - Test SDK for both test projects
- coverlet.collector 6.0.4 - Code coverage collection in generator tests

**Build/Dev:**
- MSBuild targets file (`IoCTools.Generator.targets`) - Exposes MSBuild properties to the analyzer
- `dotnet pack` - NuGet package creation with `GeneratePackageOnBuild=true` on generator and abstractions
- `PackAsTool=true` on `IoCTools.Tools.Cli` - Packaged as a .NET global/local tool (`ioc-tools` command)

## Key Dependencies

**Critical:**
- `Microsoft.CodeAnalysis.CSharp` 4.5.0 - Roslyn compiler APIs for source generation and analysis (used in generator, tests, CLI)
- `Microsoft.CodeAnalysis.Analyzers` 3.3.4 - Roslyn analyzer rules enforcement (generator and tests)
- `Microsoft.CodeAnalysis.CSharp.Workspaces` 4.5.0 - Workspace APIs for CLI project loading
- `Microsoft.CodeAnalysis.Workspaces.MSBuild` 4.5.0 - MSBuild workspace integration for CLI
- `Microsoft.Build.Locator` 1.10.12 - Locates MSBuild for CLI workspace loading

**Infrastructure:**
- `Microsoft.Extensions.Configuration` 6.x - Configuration system used in tests and sample
- `Microsoft.Extensions.Configuration.Json` 6.x - JSON configuration support
- `Microsoft.Extensions.Logging` 6.x - Logging abstractions used in tests
- `Microsoft.Extensions.Options` 6.x - Options pattern support
- `Microsoft.Extensions.Caching.Memory` 6.0.2 - Memory cache used in tests and sample
- `Microsoft.Extensions.Http` 6.0.1 - HttpClient factory in sample
- `System.Collections.Immutable` 6.0.0 - Immutable collections in generator tests
- `Microsoft.CodeAnalysis.CSharp.Scripting` 4.5.0 - Scripting API used in generator tests

**Sample-only:**
- `Mediator.Abstractions` 2.1.7 - Mediator pattern abstractions (demonstrates `[SkipAssignableTypes]` pattern)
- `Microsoft.Extensions.Options.DataAnnotations` 6.0.0 - Options validation with data annotations

## Configuration

**Environment:**
- MSBuild properties passed to the generator via `CompilerVisibleProperty` mechanism
- Key MSBuild properties:
  - `IoCToolsIgnoredTypePatterns` - Cross-assembly interface patterns to ignore (semicolon-separated, supports `*` wildcard)
  - `IoCToolsNoImplementationSeverity` - Severity for IOC001 (Error/Warning/Info/Hidden)
  - `IoCToolsUnregisteredSeverity` - Severity for IOC002
  - `IoCToolsLifetimeValidationSeverity` - Severity for IOC003/IOC012/IOC015
  - `IoCToolsConditionalServiceValidationSeverity`, `IoCToolsPartialClassValidationSeverity`, etc.
  - `IoCToolsDisableDiagnostics` - Disable all dependency validation (default: false)
  - `EmitCompilerGeneratedFiles` / `CompilerGeneratedFilesOutputPath` - Debug generated output

**Build:**
- `IoCTools.sln` - Visual Studio solution file
- `global.json` - SDK version pin at `9.0.100`
- `IoCTools.Generator/build/IoCTools.Generator.targets` - NuGet-bundled MSBuild targets
- `NuGet.Config` - Not detected

## Platform Requirements

**Development:**
- .NET SDK 9.0.100+
- Compatible IDE: Visual Studio, Rider, VS Code with C# Dev Kit

**Production:**
- `IoCTools.Abstractions`: targets `netstandard2.0` - compatible with .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+
- `IoCTools.Generator`: targets `netstandard2.0` - distributed as Roslyn analyzer/source generator NuGet package
- `IoCTools.Tools.Cli`: targets `net8.0` - distributed as .NET tool (`ioc-tools` command name)
- NuGet packages published with `GeneratePackageOnBuild=true` at version 1.3.0

---

*Stack analysis: 2026-03-21*
