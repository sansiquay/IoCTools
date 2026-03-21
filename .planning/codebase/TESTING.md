# Testing Patterns

**Analysis Date:** 2026-03-21

## Test Framework

**Runner:**
- xunit 2.9.3 (generator tests), xunit 2.6.3 (CLI tests)
- Config: no separate config file; project settings in `.csproj`

**Assertion Library:**
- FluentAssertions 6.12.0 — used exclusively (no raw `Assert.*` except in `DiagnosticDescriptorFactoryTests` which mixes both)

**Additional:**
- `Xunit.SkippableFact` 1.5.23 available in generator tests (for environment-conditional skipping)
- `coverlet.collector` 6.0.4 for coverage collection

**Run Commands:**
```bash
cd IoCTools.Generator.Tests && dotnet test    # 1650+ generator tests
cd IoCTools.Tools.Cli.Tests && dotnet test    # CLI integration tests
```

## Test File Organization

**Location:**
- Separate dedicated test projects, not co-located with source
- Generator tests: `IoCTools.Generator.Tests/` (flat — all test files at root)
- CLI tests: `IoCTools.Tools.Cli.Tests/` (flat test files + `Infrastructure/` subdirectory)

**Naming:**
- `{Subject}Tests.cs` — matches test class name exactly
- Test class matches filename: `ConstructorGenerationTests.cs` → `public class ConstructorGenerationTests`
- Infrastructure helpers: `SourceGeneratorTestHelper.cs`, `EnhancedTestUtilities.cs`

**Structure:**
```
IoCTools.Generator.Tests/
├── GlobalUsings.cs                     # global using FluentAssertions; global using Microsoft.CodeAnalysis;
├── SourceGeneratorTestHelper.cs        # Core compile-and-run infrastructure
├── EnhancedTestUtilities.cs            # Fluent builders, parsers, validators
├── *Tests.cs                           # ~80 test files, one class per file
└── generated/                          # Excluded from compilation

IoCTools.Tools.Cli.Tests/
├── Infrastructure/
│   ├── CliTestHost.cs                  # In-process CLI runner (captures stdout/stderr)
│   ├── TestPaths.cs                    # Repo root resolution, temp directories
│   └── EnvironmentVariableScope.cs     # IDisposable env var override
├── GeneratorStubs/                     # Excluded from compilation (stub generated files)
├── TestProjects/                       # Real .csproj fixtures (excluded from compilation)
│   ├── EmptyProject/
│   ├── FieldsProject/
│   ├── MultiTargetProject/
│   └── RegistrationProject/
└── *Tests.cs                           # CLI integration test files
```

## Test Structure

**Suite Organization:**
```csharp
// Standard generator test class
namespace IoCTools.Generator.Tests;

public class ConstructorGenerationTests
{
    [Fact]
    public void Constructor_SimpleService_GeneratesCorrectly()
    {
        // Arrange
        var source = @"...inline C# source code...";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse($"Compilation failed: ...");
        var constructorText = result.GetConstructorSourceText("SimpleService");
        constructorText.Should().Contain("public SimpleService(ITestService service)");
    }
}
```

**Region grouping** (common in files with many related facts):
```csharp
#region GetViolationType - Singleton Consumer
[Fact] public void GetViolationType_SingletonScoped_ReturnsSingletonDependsOnScoped() { ... }
#endregion

#region GetViolationType - Null and Invalid Inputs
[Fact] public void GetViolationType_NullConsumer_ReturnsCompatible() { ... }
#endregion
```

**Patterns:**
- Arrange/Act/Assert with comments on each section (explicit `// Arrange`, `// Act`, `// Assert`)
- Inline C# source strings passed to `SourceGeneratorTestHelper.CompileWithGenerator()` — no external fixture files for generator tests
- Failure messages embedded in FluentAssertions calls: `.Should().BeFalse($"Compilation errors: {string.Join(...)}")`

## Mocking

**Framework:** No mocking framework. All test doubles are hand-written or use real implementations.

**Generator tests — no mocks:**
- Real Roslyn compilation via `SourceGeneratorTestHelper.CompileWithGenerator()`
- Real `IServiceCollection` / `ServiceProvider` for runtime integration tests
- `EnhancedTestUtilities.MockConfigurations` provides pre-built `IConfiguration` instances (real objects, not mocks)

**CLI tests — real in-process execution:**
```csharp
// CliTestHost captures stdout/stderr by redirecting Console
var result = await CliTestHost.RunAsync("services", "--project", RegistrationProject);
result.ExitCode.Should().Be(0);
result.Stdout.Should().Contain("Service Registrations:");
```

**What to mock (not applicable — pattern is real objects):**
- `IConfiguration`: built via `new ConfigurationBuilder().AddInMemoryCollection(dict).Build()`
- Generator stub files: placed in `IoCTools.Tools.Cli.Tests/GeneratorStubs/` and injected via env var `IOC_TOOLS_GENERATOR_STUB`
- Environment variables: `EnvironmentVariableScope` IDisposable wrapper restores original value on dispose

## Fixtures and Factories

**Core generator test infrastructure (`SourceGeneratorTestHelper`):**
```csharp
// Compile inline C# with IoCTools generator applied
var result = SourceGeneratorTestHelper.CompileWithGenerator(
    sourceCode: "...",
    includeSystemReferences: true,
    analyzerBuildProperties: new Dictionary<string, string>
    {
        ["build_property.IoCToolsNoImplementationSeverity"] = "Warning"
    },
    additionalReferences: null);

// Access results
result.HasErrors                              // bool
result.Diagnostics                            // all diagnostics
result.GetDiagnosticsByCode("IOC001")         // filtered
result.GetConstructorSourceText("ClassName")  // generated constructor text
result.GetRequiredConstructorSource("Name")   // throws if missing
result.GetServiceRegistrationSource()         // registration extension source
result.GetRequiredServiceRegistrationSource() // throws if missing
result.Compilation                            // Roslyn Compilation object
```

**Fluent `ServiceBuilder` for source generation:**
```csharp
var source = EnhancedTestUtilities.CreateService()
    .WithName("MyService")
    .InNamespace("Test")
    .WithService("Scoped")
    .Implements("IMyInterface")
    .WithInjectField("ILogger<MyService>", "_logger")
    .Build();
```

**Fluent `ConfigurationBuilder` for configuration tests:**
```csharp
var config = EnhancedTestUtilities.CreateConfiguration()
    .AddValue("Database:ConnectionString", "Server=localhost")
    .AddValue("Database:Timeout", 30)
    .Build();
```

**`CreateServiceTemplate` factory:**
```csharp
var source = SourceGeneratorTestHelper.CreateServiceTemplate(
    "MyService",
    dependencies: new[] { "IServiceA", "IServiceB" },
    attributes: new[] { "Scoped" });
```

**Location:**
- Infrastructure code: `IoCTools.Generator.Tests/SourceGeneratorTestHelper.cs` and `EnhancedTestUtilities.cs`
- CLI infrastructure: `IoCTools.Tools.Cli.Tests/Infrastructure/`
- TestProject fixtures: `IoCTools.Tools.Cli.Tests/TestProjects/{ProjectName}/`

## Coverage

**Requirements:** No enforced minimum threshold in project files.

**Collection:** `coverlet.collector` installed in generator test project.

**View Coverage:**
```bash
cd IoCTools.Generator.Tests && dotnet test --collect:"XPlat Code Coverage"
```

## Test Types

**Unit Tests (pure logic):**
- `LifetimeCompatibilityCheckerTests.cs` — tests static utility methods directly
- `DiagnosticDescriptorFactoryTests.cs` — tests descriptor caching/severity logic
- `TypeHelpersTests.cs`, `AttributeParserTests.cs`, `HintNameBuilderTests.cs` — utility unit tests
- No DI container, no compilation — just method calls and assertions

**Source Generator Integration Tests (majority):**
- Compile inline C# source with the real `DependencyInjectionGenerator` applied
- Verify generated output (constructor content, registration content)
- Verify diagnostics emitted (codes, severity, count)
- Examples: `ConstructorGenerationTests.cs`, `InheritanceTests.cs`, `BackgroundServiceTests.cs`, `RegisterAsInstanceSharingTests.cs`

**Runtime Integration Tests:**
- Use `SourceGeneratorTestHelper.CreateRuntimeContext()` to emit actual assemblies
- Use `BuildServiceProvider()` to construct DI container and resolve services
- Verify real service resolution, constructor injection, and runtime behavior
- Examples: `ConfigurationInjectionRuntimeTests.cs`, `InstanceSharingRuntimeValidationTests.cs`

**CLI Integration Tests:**
- `CliTestHost.RunAsync()` invokes `Program.Main()` in-process with captured stdout/stderr
- Tests verify exit codes, stdout content, and file system artifacts
- Serialized via `[Collection("CLI Execution")]` + `SemaphoreSlim(1,1)` gate (CLI is not thread-safe)
- Examples: `CliServicesCommandTests.cs`, `CliFieldsCommandTests.cs`, `CliConfigAuditCommandTests.cs`

## Common Patterns

**Diagnostic assertion:**
```csharp
var diags = result.GetDiagnosticsByCode("IOC044");
diags.Should().ContainSingle();
diags[0].Severity.Should().Be(DiagnosticSeverity.Warning);
```

**No-diagnostic assertion:**
```csharp
result.GetDiagnosticsByCode("IOC080").Should().BeEmpty();
```

**Compilation success assertion with detailed failure message:**
```csharp
result.HasErrors.Should().BeFalse(
    "Compilation errors: {0}",
    string.Join(", ", result.Diagnostics
        .Where(d => d.Severity == DiagnosticSeverity.Error)
        .Select(d => d.GetMessage())));
```

**Generated content pattern — accepting namespace-qualified OR simple names:**
```csharp
// Accept either form since generator may or may not qualify
var hasCorrectConstructor =
    constructorText.Contains("public SimpleService(ITestService service)") ||
    constructorText.Contains("public SimpleService(Test.ITestService service)");
hasCorrectConstructor.Should().BeTrue($"Constructor signature not found. Generated: {constructorText}");
```

**Generated content using Regex for precision:**
```csharp
var constructorRegex = new Regex(
    @"public\s+DerivedController\s*\(\s*IBaseService\s+baseService\s*,\s*IDerivedService\s+derivedService\s*\)\s*:\s*base\s*\(\s*baseService\s*\)");
constructorRegex.IsMatch(constructorContent).Should()
    .BeTrue("Constructor doesn't match expected pattern. Actual: {0}", constructorContent);
```

**Parametrized tests with `[Theory]` + `[InlineData]`:**
```csharp
[Theory]
[InlineData("Error", DiagnosticSeverity.Error)]
[InlineData("Warning", DiagnosticSeverity.Warning)]
[InlineData("Info", DiagnosticSeverity.Info)]
[InlineData("Hidden", DiagnosticSeverity.Hidden)]
public void MSBuildDiagnostics_NoImplementationSeverity_ConfiguresCorrectly(
    string severityValue, DiagnosticSeverity expectedSeverity) { ... }
```

**Parametrized tests for enum value coverage:**
```csharp
[Theory]
[InlineData("int")]
[InlineData("string")]
[InlineData("System.Guid")]
[InlineData("System.Nullable<int>")]
public void DependsOn_PrimitiveLike_ProducesIOC044(string typeName) { ... }
```

**MSBuild property injection for generator tests:**
```csharp
var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode, true,
    new Dictionary<string, string>
    {
        ["build_property.IoCToolsNoImplementationSeverity"] = "Warning",
        ["build_property.IoCToolsDisableDiagnostics"] = "true"
    });
```

**Async CLI tests:**
```csharp
[Fact]
public async Task ServicesCommand_SummarizesRegistrations_AndConfiguration()
{
    var tempDir = TestPaths.CreateTempDirectory();
    var stubDir = TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "GeneratorStubs");
    using var scope = new EnvironmentVariableScope("IOC_TOOLS_GENERATOR_STUB", stubDir);

    var result = await CliTestHost.RunAsync("services", "--project", RegistrationProject, "--output", tempDir);

    result.ExitCode.Should().Be(0);
    result.Stdout.Should().Contain("Service Registrations:");
}
```

**Fluent validator (from `EnhancedTestUtilities`):**
```csharp
EnhancedTestUtilities.Validate(result)
    .ShouldCompile()
    .ShouldHaveConstructorFor("MyService")
    .ShouldHaveServiceRegistration()
    .ShouldNotHaveDiagnostic("IOC001")
    .ShouldContainInRegistration("AddScoped<IMyService, MyService>");
```

---

*Testing analysis: 2026-03-21*
