# IoCTools Evidence And Inject Guidance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the `1.5.0` IoCTools release pass with a new `ioc-tools evidence` command, richer machine-readable validator/suppress output, compatibility-only `[Inject]` guidance, aligned docs, and release-ready package metadata.

**Architecture:** Add a new top-level CLI evidence command that composes structured report models instead of scraping human text. Reuse the real Roslyn/MSBuild project load path, expose report-building seams from existing CLI utilities, add compatibility diagnostics and migration hints for `[Inject]`/`[InjectConfiguration]`, and then align all docs and packages to the same `1.5.0` story.

**Tech Stack:** C#/.NET 8 CLI tooling, Roslyn source generators/analyzers, xUnit + FluentAssertions, MSBuild workspace loading, NuGet packaging.

---

## File Map

- Create: `IoCTools.Tools.Cli/Utilities/EvidenceModels.cs`
  Responsibility: shared structured payload records for evidence, suppress JSON, validator JSON, and compare/config audit summaries where reuse is helpful.
- Create: `IoCTools.Tools.Cli/Utilities/EvidenceBundleBuilder.cs`
  Responsibility: orchestrate service inspection, diagnostics, config audit, validator inspection, artifact snapshot/compare, and migration hint generation for `ioc-tools evidence`.
- Create: `IoCTools.Tools.Cli/Utilities/EvidencePrinter.cs`
  Responsibility: render the evidence bundle in human-readable sections and JSON mode.
- Create: `IoCTools.Tools.Cli/Utilities/AuthoringPatternInspector.cs`
  Responsibility: inspect source symbols for `[Inject]`, `InjectConfiguration`, `[DependsOn]`, and `[DependsOnConfiguration]` usage so evidence can emit migration hints without parsing console text.
- Modify: `IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs`
  Responsibility: add `ParseEvidence(...)`, normalize evidence switches, and define `EvidenceCommandOptions`.
- Modify: `IoCTools.Tools.Cli/Program.cs`
  Responsibility: dispatch `evidence`, call the evidence builder, and preserve existing CLI behavior.
- Modify: `IoCTools.Tools.Cli/Utilities/UsagePrinter.cs`
  Responsibility: surface `evidence` and updated suppress usage in help text.
- Modify: `IoCTools.Tools.Cli/Utilities/ConfigAuditPrinter.cs`
  Responsibility: expose a reusable `ConfigAuditReport` builder so `config-audit` and `evidence` share the same logic.
- Modify: `IoCTools.Tools.Cli/Utilities/CompareRunner.cs`
  Responsibility: expose structured artifact snapshot/compare summaries instead of print-only behavior.
- Modify: `IoCTools.Tools.Cli/Utilities/SuppressPrinter.cs`
  Responsibility: emit structured JSON with rule metadata, filter metadata, live-mode metadata, and append results.
- Modify: `IoCTools.Tools.Cli/Utilities/ValidatorInspector.cs`
  Responsibility: expose structured lifetime-trace data for `validator-graph --why --json`.
- Modify: `IoCTools.Tools.Cli/Utilities/ValidatorPrinter.cs`
  Responsibility: emit structured validator graph JSON and structured `--why` JSON while preserving text output.
- Create: `IoCTools.Tools.Cli.Tests/CliEvidenceCommandTests.cs`
  Responsibility: integration coverage for the new `evidence` command in text and JSON modes.
- Create: `IoCTools.Tools.Cli.Tests/CliSuppressCommandTests.cs`
  Responsibility: integration coverage for structured suppress JSON and append metadata.
- Modify: `IoCTools.Tools.Cli.Tests/ValidatorCommandTests.cs`
  Responsibility: unit coverage for structured validator graph and `--why` JSON payloads.
- Create: `IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/EvidenceProject.csproj`
  Responsibility: real project fixture with services, config bindings, validators, and compatibility-only authoring patterns.
- Create: `IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/Services/BillingServices.cs`
  Responsibility: exercise `[DependsOn]`, `[Inject]`, `[DependsOnConfiguration]`, and `InjectConfiguration` in one project.
- Create: `IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/Validators/OrderValidators.cs`
  Responsibility: exercise validator discovery, composition graph edges, and direct/injected composition.
- Create: `IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/FluentValidation/FluentValidationStubs.cs`
  Responsibility: minimal compile-time FluentValidation surface for validator inspection without external package coupling.
- Create: `IoCTools.Generator.Tests/InjectCompatibilityGuidanceTests.cs`
  Responsibility: targeted coverage for new compatibility diagnostics and wording.
- Modify: `IoCTools.Generator.Tests/InjectUsageDiagnosticTests.cs`
  Responsibility: preserve IOC035 behavior while aligning message expectations with the new guidance posture.
- Modify: `IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticRules.cs`
  Responsibility: expose entry points for compatibility guidance validators.
- Modify: `IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticsRunner.cs`
  Responsibility: invoke the new compatibility validators in the normal diagnostics flow.
- Create: `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/InjectCompatibilityValidator.cs`
  Responsibility: report new non-breaking compatibility diagnostics for `[Inject]` and `InjectConfiguration`.
- Modify: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs`
  Responsibility: refine IOC028/IOC035/IOC070 wording and add any new `[Inject]` compatibility descriptor(s).
- Modify: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/DependencyDiagnostics.cs`
  Responsibility: refine IOC044 wording so `[DependsOnConfiguration]` is the paved road.
- Modify: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/ConfigurationDiagnostics.cs`
  Responsibility: add any new `InjectConfiguration` compatibility descriptor(s) and align existing wording.
- Modify: `IoCTools.Tools.Cli/Utilities/DiagnosticCatalog.cs`
  Responsibility: keep CLI diagnostic metadata aligned with any new or renamed descriptors.
- Modify: `IoCTools.Abstractions/IoCTools.Abstractions.csproj`
  Responsibility: align package/version metadata to `1.5.0`.
- Modify: `IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj`
  Responsibility: align package/version metadata to `1.5.0`.
- Modify: `IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj`
  Responsibility: align tool package version/description to `1.5.0`.
- Modify: `README.md`
  Responsibility: update feature list, command list, and authoring guidance.
- Modify: `CHANGELOG.md`
  Responsibility: retcon `1.5.0` release notes to include evidence, structured JSON, and compatibility guidance.
- Modify: `docs/cli-reference.md`
  Responsibility: document `evidence`, richer suppress JSON, and structured validator output.
- Modify: `docs/attributes.md`
  Responsibility: explicitly say never use `[Inject]` or `InjectConfiguration` in new code.
- Modify: `docs/getting-started.md`
  Responsibility: remove casual `[Inject]` guidance from the happy path.
- Modify: `docs/migration.md`
  Responsibility: route configuration migration to `[DependsOnConfiguration]` instead of `InjectConfiguration`.
- Modify: `docs/testing.md`
  Responsibility: update examples and helpers to prefer `[DependsOn]` / `[DependsOnConfiguration]`.
- Modify: `docs/diagnostics.md`
  Responsibility: document any new compatibility diagnostics and update IOC028/IOC035/IOC044/IOC070 wording.

## Task 1: Add The `evidence` Command Contract And Fixture Project

**Files:**
- Create: `IoCTools.Tools.Cli.Tests/CliEvidenceCommandTests.cs`
- Create: `IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/EvidenceProject.csproj`
- Create: `IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/Services/BillingServices.cs`
- Create: `IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/Validators/OrderValidators.cs`
- Create: `IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/FluentValidation/FluentValidationStubs.cs`
- Modify: `IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs`
- Modify: `IoCTools.Tools.Cli/Program.cs`
- Modify: `IoCTools.Tools.Cli/Utilities/UsagePrinter.cs`

- [ ] **Step 1: Create the real CLI fixture project used by evidence tests**

```xml
<!-- IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/EvidenceProject.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\IoCTools.Generator\IoCTools.Generator\IoCTools.Generator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include="..\..\..\IoCTools.Abstractions\Annotations\*.cs" Link="Annotations\%(FileName)%(Extension)" />
    <Compile Include="..\..\..\IoCTools.Abstractions\Enumerations\*.cs" Link="Enumerations\%(FileName)%(Extension)" />
  </ItemGroup>
</Project>
```

```csharp
// IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/Services/BillingServices.cs
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace EvidenceProject.Services;

public interface IBillingService { }
public interface IClock { DateTimeOffset UtcNow { get; } }
public interface IAuditSink { void Write(string message); }

[Scoped]
[RegisterAs<IBillingService>]
[DependsOn<ILogger<BillingService>, IAuditSink>]
[DependsOnConfiguration<string>("Billing:BaseUrl", Required = true)]
public partial class BillingService : IBillingService
{
    [Inject] private readonly IClock _clock;
    [InjectConfiguration("Billing:RetryCount")] private readonly int _retryCount;
}
```

```csharp
// IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/FluentValidation/FluentValidationStubs.cs
namespace FluentValidation;

public abstract class AbstractValidator<T>
{
    protected RuleBuilder<T, TProperty> RuleFor<TProperty>(System.Linq.Expressions.Expression<Func<T, TProperty>> expression) => new();
}

public sealed class RuleBuilder<T, TProperty>
{
    public RuleBuilder<T, TProperty> NotEmpty() => this;
    public RuleBuilder<T, TProperty> SetValidator<TValidator>(TValidator validator) => this;
    public RuleBuilder<T, TProperty> Include<TValidator>(TValidator validator) => this;
}
```

- [ ] **Step 2: Write failing CLI tests for command recognition and usage output**

```csharp
// IoCTools.Tools.Cli.Tests/CliEvidenceCommandTests.cs
namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;
using Infrastructure;
using Xunit;

[Collection("CLI Execution")]
public sealed class CliEvidenceCommandTests
{
    private static string EvidenceProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "EvidenceProject", "EvidenceProject.csproj");

    [Fact]
    public async Task Help_Prints_Evidence_Command()
    {
        var result = await CliTestHost.RunAsync("help");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("dotnet ioc-tools evidence --project <csproj>");
    }

    [Fact]
    public async Task Evidence_Without_Project_Returns_Usage_Error()
    {
        var result = await CliTestHost.RunAsync("evidence");

        result.ExitCode.Should().Be(1);
        result.Stderr.Should().Contain("--project is required.");
    }
}
```

- [ ] **Step 3: Run the focused CLI evidence tests and confirm they fail for the missing command**

Run: `dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~CliEvidenceCommandTests --verbosity minimal`

Expected: FAIL with `Unknown command 'evidence'` or missing usage text.

- [ ] **Step 4: Add parse, usage, and dispatch support for the new command**

```csharp
// IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs
internal static ParseResult<EvidenceCommandOptions> ParseEvidence(string[] args)
{
    if (!TryCollectOptions(args, out var map, out var error))
        return ParseResult<EvidenceCommandOptions>.Fail(error);

    if (!map.TryGetValue("project", out var projectValues))
        return ParseResult<EvidenceCommandOptions>.Fail("--project is required.");

    var common = BuildCommon(projectValues[^1], map);
    var typeName = map.TryGetValue("type", out var typeValues) ? typeValues[^1] : null;
    var settings = map.TryGetValue("settings", out var settingsValues) ? NormalizePath(settingsValues[^1]) : null;
    var baseline = map.TryGetValue("baseline", out var baselineValues) ? NormalizePath(baselineValues[^1]) : null;
    var output = map.TryGetValue("output", out var outputValues) ? NormalizePath(outputValues[^1]) : null;

    return ParseResult<EvidenceCommandOptions>.Ok(
        new EvidenceCommandOptions(common, typeName, settings, baseline, output));
}

internal sealed record EvidenceCommandOptions(
    CommonOptions Common,
    string? TypeName,
    string? SettingsPath,
    string? BaselineDirectory,
    string? OutputDirectory);
```

```csharp
// IoCTools.Tools.Cli/Program.cs
return command switch
{
    "evidence" => await RunEvidenceAsync(remaining, cts.Token),
    "fields" => await RunFieldsAsync(remaining, cts.Token),
    "services" => await RunServicesAsync(remaining, cts.Token),
    _ => UsagePrinter.ExitUnknown(command)
};

private static async Task<int> RunEvidenceAsync(string[] args, CancellationToken token)
{
    var parse = CommandLineParser.ParseEvidence(args);
    if (!parse.Success)
        return UsagePrinter.ExitWithError(parse.Error);

    var options = parse.Value!;
    var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
    output.WriteLine("Evidence command scaffolded.");
    await Task.CompletedTask;
    return 0;
}
```

```csharp
// IoCTools.Tools.Cli/Utilities/UsagePrinter.cs
Console.WriteLine("  dotnet ioc-tools evidence --project <csproj> [--type Namespace.Service] [--settings appsettings.json] [--baseline <dir>] [--output <dir>]");
```

- [ ] **Step 5: Re-run the focused CLI evidence tests and make sure the contract now exists**

Run: `dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~CliEvidenceCommandTests --verbosity minimal`

Expected: PASS for `Help_Prints_Evidence_Command` and `Evidence_Without_Project_Returns_Usage_Error`.

- [ ] **Step 6: Commit the command shell and test fixture**

```bash
git add IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs \
        IoCTools.Tools.Cli/Program.cs \
        IoCTools.Tools.Cli/Utilities/UsagePrinter.cs \
        IoCTools.Tools.Cli.Tests/CliEvidenceCommandTests.cs \
        IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject
git commit -m "feat: add evidence command shell"
```

## Task 2: Implement Correlated Evidence Bundles

**Files:**
- Create: `IoCTools.Tools.Cli/Utilities/EvidenceModels.cs`
- Create: `IoCTools.Tools.Cli/Utilities/AuthoringPatternInspector.cs`
- Create: `IoCTools.Tools.Cli/Utilities/EvidenceBundleBuilder.cs`
- Create: `IoCTools.Tools.Cli/Utilities/EvidencePrinter.cs`
- Modify: `IoCTools.Tools.Cli/Program.cs`
- Modify: `IoCTools.Tools.Cli/Utilities/ConfigAuditPrinter.cs`
- Modify: `IoCTools.Tools.Cli/Utilities/CompareRunner.cs`
- Modify: `IoCTools.Tools.Cli.Tests/CliEvidenceCommandTests.cs`
- Modify: `IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/Validators/OrderValidators.cs`

- [ ] **Step 1: Expand the evidence tests to demand the real payload**

```csharp
// IoCTools.Tools.Cli.Tests/CliEvidenceCommandTests.cs
[Fact]
public async Task Evidence_Json_Correlates_Project_Services_Diagnostics_Validators_And_MigrationHints()
{
    var outputDir = TestPaths.CreateTempDirectory();
    var settingsPath = Path.Combine(outputDir, "appsettings.json");
    await File.WriteAllTextAsync(settingsPath, """
    {
      "Billing": {
        "BaseUrl": "https://billing.example.test"
      }
    }
    """);

    var result = await CliTestHost.RunAsync(
        "evidence",
        "--project", EvidenceProjectPath,
        "--settings", settingsPath,
        "--output", outputDir,
        "--json");

    result.ExitCode.Should().Be(0);
    result.Stdout.Should().Contain("\"project\"");
    result.Stdout.Should().Contain("\"services\"");
    result.Stdout.Should().Contain("\"typeEvidence\"");
    result.Stdout.Should().Contain("\"diagnostics\"");
    result.Stdout.Should().Contain("\"configuration\"");
    result.Stdout.Should().Contain("\"validators\"");
    result.Stdout.Should().Contain("\"artifacts\"");
    result.Stdout.Should().Contain("\"migrationHints\"");
    result.Stdout.Should().Contain("DependsOnConfiguration");
}

[Fact]
public async Task Evidence_Text_Mode_Prints_Named_Sections()
{
    var result = await CliTestHost.RunAsync(
        "evidence",
        "--project", EvidenceProjectPath,
        "--type", "EvidenceProject.Services.BillingService");

    result.ExitCode.Should().Be(0);
    result.Stdout.Should().Contain("Project");
    result.Stdout.Should().Contain("Services");
    result.Stdout.Should().Contain("Type Evidence");
    result.Stdout.Should().Contain("Migration Hints");
}
```

```csharp
// IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/Validators/OrderValidators.cs
using FluentValidation;
using IoCTools.Abstractions.Annotations;

namespace EvidenceProject.Validators;

public sealed record Address(string Line1);
public sealed record Order(Address Address);

[Scoped]
public partial class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator() => RuleFor(x => x.Line1).NotEmpty();
}

[Scoped]
public partial class OrderValidator : AbstractValidator<Order>
{
    [Inject] private readonly AddressValidator _addressValidator;

    public OrderValidator()
        => RuleFor(x => x.Address).SetValidator(_addressValidator);
}
```

- [ ] **Step 2: Run the focused evidence tests and confirm the scaffold fails**

Run: `dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~CliEvidenceCommandTests --verbosity minimal`

Expected: FAIL because the scaffold does not emit the required sections or migration hints.

- [ ] **Step 3: Add reusable structured report models and source-pattern inspection**

```csharp
// IoCTools.Tools.Cli/Utilities/EvidenceModels.cs
internal sealed record EvidenceBundle(
    EvidenceProjectSummary Project,
    EvidenceServiceSummary Services,
    IReadOnlyList<EvidenceTypeDetail>? TypeEvidence,
    IReadOnlyList<DiagnosticSummary>? Diagnostics,
    ConfigAuditReport? Configuration,
    ValidatorEvidenceReport? Validators,
    ArtifactCompareReport? Artifacts,
    IReadOnlyList<MigrationHint> MigrationHints);

internal sealed record EvidenceProjectSummary(
    string ProjectPath,
    string Configuration,
    string? Framework);

internal sealed record EvidenceServiceSummary(
    int RegisteredServiceCount,
    int ConfigurationBindingCount,
    int ConditionalRegistrationCount);

internal sealed record EvidenceTypeDetail(
    string TypeName,
    string FilePath,
    IReadOnlyList<EvidenceDependencyDetail> Dependencies,
    IReadOnlyList<EvidenceConfigurationDetail> Configuration);

internal sealed record EvidenceDependencyDetail(
    string TypeName,
    string FieldName,
    string Source,
    bool IsExternal);

internal sealed record EvidenceConfigurationDetail(
    string TypeName,
    string FieldName,
    string Key,
    bool Required,
    bool SupportsReloading,
    string Source);

internal sealed record ValidatorEvidenceReport(
    int ValidatorCount,
    int RootValidatorCount,
    IReadOnlyList<ValidatorEvidenceDetail> Validators);

internal sealed record ValidatorEvidenceDetail(
    string Validator,
    string ModelType,
    string? Lifetime,
    int CompositionEdgeCount);

internal sealed record ConfigAuditReport(
    int RequiredBindings,
    int SettingsKeysDiscovered,
    IReadOnlyList<string> AllKeys,
    IReadOnlyList<string> MissingKeys,
    string? SettingsPath);

internal sealed record ArtifactCompareReport(
    string OutputDirectory,
    IReadOnlyList<string> SnapshotFiles,
    string? BaselineDirectory,
    IReadOnlyList<string> ChangedFiles);

internal sealed record AuthoringPatternReport(
    string TypeName,
    IReadOnlyList<string> InjectFields,
    IReadOnlyList<string> InjectConfigurationFields);

internal sealed record MigrationHint(
    string TypeName,
    string Source,
    string Message,
    string SuggestedReplacement);
```

```csharp
// IoCTools.Tools.Cli/Utilities/AuthoringPatternInspector.cs
internal static class AuthoringPatternInspector
{
    public static async Task<IReadOnlyList<AuthoringPatternReport>> DiscoverAsync(Project project, CancellationToken token)
    {
        var reports = new List<AuthoringPatternReport>();

        foreach (var document in project.Documents.Where(d => d.SupportsSyntaxTree))
        {
            var root = await document.GetSyntaxRootAsync(token);
            var semanticModel = await document.GetSemanticModelAsync(token);
            if (root is null || semanticModel is null) continue;

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(declaration, token) is not INamedTypeSymbol symbol) continue;

                var injectFields = symbol.GetMembers().OfType<IFieldSymbol>()
                    .Where(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"))
                    .Select(field => field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();

                var injectConfigurationFields = symbol.GetMembers().OfType<IFieldSymbol>()
                    .Where(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectConfigurationAttribute"))
                    .Select(field => field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();

                if (injectFields.Length == 0 && injectConfigurationFields.Length == 0) continue;

                reports.Add(new AuthoringPatternReport(
                    symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty),
                    injectFields,
                    injectConfigurationFields));
            }
        }

        return reports;
    }
}
```

- [ ] **Step 4: Refactor existing CLI utilities so evidence can reuse real data, not console text**

```csharp
// IoCTools.Tools.Cli/Utilities/ConfigAuditPrinter.cs
internal static ConfigAuditReport BuildReport(IReadOnlyList<ServiceFieldReport> reports, string? settingsPath)
{
    var requiredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var report in reports)
    foreach (var cfg in report.ConfigurationFields)
    {
        var key = string.IsNullOrWhiteSpace(cfg.ConfigurationKey)
            ? InferSectionKeyFromTypeName(cfg.TypeName)
            : cfg.ConfigurationKey!;
        requiredKeys.Add(key);
    }

    var discoveredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (!string.IsNullOrWhiteSpace(settingsPath) && File.Exists(settingsPath))
    {
        using var stream = File.OpenRead(settingsPath);
        using var doc = JsonDocument.Parse(stream);
        Flatten(doc.RootElement, discoveredKeys, string.Empty);
    }

    var allKeys = requiredKeys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray();
    var missingKeys = (discoveredKeys.Count == 0
            ? requiredKeys
            : requiredKeys.Where(key => !discoveredKeys.Contains(key)))
        .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return new ConfigAuditReport(allKeys.Length, discoveredKeys.Count, allKeys, missingKeys, settingsPath);
}

public static void Write(IReadOnlyList<ServiceFieldReport> reports, string? settingsPath, OutputContext output)
{
    var report = BuildReport(reports, settingsPath);
    if (output.IsJson)
    {
        output.WriteJson(report);
        return;
    }

    output.WriteLine("Configuration audit:");
    output.WriteLine($"  Required bindings: {report.RequiredBindings}");
    if (report.SettingsKeysDiscovered > 0)
        output.WriteLine($"  Settings keys discovered: {report.SettingsKeysDiscovered}");

    if (report.MissingKeys.Count == 0)
    {
        output.WriteLine("  All keys present in provided settings.");
        return;
    }

    output.WriteLine("  Missing keys:");
    foreach (var key in report.MissingKeys)
        output.WriteLine($"    - {key}");
}
```

```csharp
// IoCTools.Tools.Cli/Utilities/CompareRunner.cs
internal static ArtifactCompareReport BuildReport(GeneratorArtifactWriter artifacts, string outputDir, string? baselineDir)
{
    var snapshotFiles = Directory.GetFiles(artifacts.OutputRoot, "*.g.cs", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileName)
        .OfType<string>()
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var changedFiles = new List<string>();
    if (!string.IsNullOrWhiteSpace(baselineDir) && Directory.Exists(baselineDir))
    {
        var baselineMap = Directory.GetFiles(baselineDir, "*.g.cs", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path) is not null)
            .ToDictionary(path => Path.GetFileName(path)!, File.ReadAllText, StringComparer.OrdinalIgnoreCase);

        var newMap = Directory.GetFiles(artifacts.OutputRoot, "*.g.cs", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path) is not null)
            .ToDictionary(path => Path.GetFileName(path)!, File.ReadAllText, StringComparer.OrdinalIgnoreCase);

        foreach (var fileName in baselineMap.Keys.Union(newMap.Keys).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            baselineMap.TryGetValue(fileName, out var oldText);
            newMap.TryGetValue(fileName, out var newText);
            if (!string.Equals(oldText, newText, StringComparison.Ordinal))
                changedFiles.Add(fileName);
        }
    }

    return new ArtifactCompareReport(artifacts.OutputRoot, snapshotFiles, baselineDir, changedFiles);
}
```

- [ ] **Step 5: Build and print the full evidence bundle**

```csharp
// IoCTools.Tools.Cli/Utilities/EvidenceBundleBuilder.cs
internal static class EvidenceBundleBuilder
{
    public static async Task<EvidenceBundle> BuildAsync(ProjectContext context, EvidenceCommandOptions options, CancellationToken token)
    {
        var serviceInspector = new ServiceFieldInspector(context.Project);
        var fieldReports = await serviceInspector.GetFieldReportsAsync(null, Array.Empty<string>(), token);
        var authoringPatterns = await AuthoringPatternInspector.DiscoverAsync(context.Project, token);
        var diagnostics = await DiagnosticRunner.RunAsync(context, token);
        var validators = ValidatorInspector.DiscoverValidators(context.Compilation);
        var configReport = ConfigAuditPrinter.BuildReport(fieldReports, options.SettingsPath);
        var artifactsWriter = await GeneratorArtifactWriter.CreateAsync(context, options.OutputDirectory, token);

        var registrationSummary = new RegistrationSummary("<missing>", Array.Empty<RegistrationRecord>());
        var extensionHint = HintNameBuilder.GetExtensionHint(context.Project);
        if (artifactsWriter.TryGetFile(extensionHint, out var extensionPath))
            registrationSummary = RegistrationSummaryBuilder.Build(extensionPath!);

        var filteredReports = string.IsNullOrWhiteSpace(options.TypeName)
            ? fieldReports
            : fieldReports.Where(report => TypeFilterUtility.Matches(report.TypeName, options.TypeName!)).ToArray();

        var typeEvidence = filteredReports.Select(report => new EvidenceTypeDetail(
            report.TypeName,
            report.FilePath,
            report.DependencyFields.Select(field => new EvidenceDependencyDetail(
                field.TypeName,
                field.FieldName,
                field.Source,
                field.IsExternal)).ToArray(),
            report.ConfigurationFields.Select(field => new EvidenceConfigurationDetail(
                field.TypeName,
                field.FieldName,
                field.ConfigurationKey ?? "<inferred>",
                field.Required == true,
                field.SupportsReloading == true,
                field.Source)).ToArray())).ToArray();

        var migrationHints = authoringPatterns
            .SelectMany(pattern =>
                pattern.InjectFields.Select(typeName => new MigrationHint(
                        pattern.TypeName,
                        "[Inject]",
                        $"Never use [Inject] in new code on {pattern.TypeName}; replace {typeName} with [DependsOn<{typeName}>].",
                        $"[DependsOn<{typeName}>]"))
                    .Concat(pattern.InjectConfigurationFields.Select(typeName => new MigrationHint(
                        pattern.TypeName,
                        "InjectConfiguration",
                        $"Never use InjectConfiguration in new code on {pattern.TypeName}; prefer [DependsOnConfiguration<{typeName}>] or [DependsOnOptions<{typeName}>].",
                        $"[DependsOnConfiguration<{typeName}>](...)"))))
            .OrderBy(hint => hint.TypeName, StringComparer.Ordinal)
            .ToArray();

        ArtifactCompareReport? artifacts = null;
        if (options.OutputDirectory is not null || options.BaselineDirectory is not null)
            artifacts = CompareRunner.BuildReport(artifactsWriter, artifactsWriter.OutputRoot, options.BaselineDirectory);

        return new EvidenceBundle(
            new EvidenceProjectSummary(context.Project.FilePath ?? options.Common.ProjectPath, options.Common.Configuration, options.Common.Framework),
            new EvidenceServiceSummary(
                registrationSummary.Records.Count(record => record.Kind == RegistrationKind.Service),
                registrationSummary.Records.Count(record => record.Kind == RegistrationKind.Configuration),
                registrationSummary.Records.Count(record => record.IsConditional)),
            typeEvidence,
            diagnostics,
            configReport,
            new ValidatorEvidenceReport(
                validators.Count,
                ValidatorInspector.BuildCompositionTree(validators).Count,
                validators.Select(validator => new ValidatorEvidenceDetail(
                    validator.FullName,
                    validator.ModelType,
                    validator.Lifetime,
                    validator.CompositionEdges.Count)).ToArray()),
            artifacts,
            migrationHints);
    }
}
```

```csharp
// IoCTools.Tools.Cli/Utilities/EvidencePrinter.cs
internal static class EvidencePrinter
{
    public static void Write(EvidenceBundle bundle, OutputContext output)
    {
        if (output.IsJson)
        {
            output.WriteJson(bundle);
            return;
        }

        output.WriteLine("Project");
        output.WriteLine($"  Path: {bundle.Project.ProjectPath}");
        output.WriteLine($"  Configuration: {bundle.Project.Configuration}");
        output.WriteLine("Services");
        output.WriteLine($"  Registered services: {bundle.Services.RegisteredServiceCount}");
        output.WriteLine($"  Configuration bindings: {bundle.Services.ConfigurationBindingCount}");
        output.WriteLine("Type Evidence");
        foreach (var type in bundle.TypeEvidence ?? Array.Empty<EvidenceTypeDetail>())
            output.WriteLine($"  - {type.TypeName} ({type.Dependencies.Count} dependencies, {type.Configuration.Count} config bindings)");
        output.WriteLine("Migration Hints");
        foreach (var hint in bundle.MigrationHints)
            output.WriteLine($"  - {hint.Message}");
    }
}
```

```csharp
// IoCTools.Tools.Cli/Program.cs
private static async Task<int> RunEvidenceAsync(string[] args, CancellationToken token)
{
    var parse = CommandLineParser.ParseEvidence(args);
    if (!parse.Success)
        return UsagePrinter.ExitWithError(parse.Error);

    var options = parse.Value!;
    var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
    await using var context = await ProjectContext.CreateAsync(options.Common, token);
    var bundle = await EvidenceBundleBuilder.BuildAsync(context, options, token);
    EvidencePrinter.Write(bundle, output);
    output.ReportTiming("evidence command completed");
    return bundle.Diagnostics?.Any(d => d.Severity == "Error") == true ? 1 : 0;
}
```

- [ ] **Step 6: Re-run the evidence tests and make sure the bundle is now real**

Run: `dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~CliEvidenceCommandTests --verbosity minimal`

Expected: PASS with JSON containing all required sections and text mode containing named sections.

- [ ] **Step 7: Commit the evidence bundle implementation**

```bash
git add IoCTools.Tools.Cli/Utilities/EvidenceModels.cs \
        IoCTools.Tools.Cli/Utilities/AuthoringPatternInspector.cs \
        IoCTools.Tools.Cli/Utilities/EvidenceBundleBuilder.cs \
        IoCTools.Tools.Cli/Utilities/EvidencePrinter.cs \
        IoCTools.Tools.Cli/Utilities/ConfigAuditPrinter.cs \
        IoCTools.Tools.Cli/Utilities/CompareRunner.cs \
        IoCTools.Tools.Cli/Program.cs \
        IoCTools.Tools.Cli.Tests/CliEvidenceCommandTests.cs \
        IoCTools.Tools.Cli.Tests/TestProjects/EvidenceProject/Validators/OrderValidators.cs
git commit -m "feat: add evidence bundle command"
```

## Task 3: Harden Validator JSON Output

**Files:**
- Modify: `IoCTools.Tools.Cli/Utilities/ValidatorInspector.cs`
- Modify: `IoCTools.Tools.Cli/Utilities/ValidatorPrinter.cs`
- Modify: `IoCTools.Tools.Cli.Tests/ValidatorCommandTests.cs`

- [ ] **Step 1: Write failing validator JSON tests for structured graph and structured why output**

```csharp
// IoCTools.Tools.Cli.Tests/ValidatorCommandTests.cs
[Fact]
public void WriteGraph_JsonMode_Emits_Roots_And_Edges()
{
    var validators = new[]
    {
        new ValidatorInfo("TestApp.OrderValidator", "Order", "Scoped",
            new[] { new CompositionEdgeInfo("AddressValidator", "SetValidator", false) }),
        new ValidatorInfo("TestApp.AddressValidator", "Address", "Scoped", Array.Empty<CompositionEdgeInfo>())
    };

    var output = CaptureConsoleOutput(() => ValidatorPrinter.WriteGraph(validators, CreateJsonOutput()));

    output.Should().Contain("\"roots\"");
    output.Should().Contain("\"compositionMethod\"");
    output.Should().Contain("\"resolvedValidator\"");
}

[Fact]
public void WriteWhy_JsonMode_Emits_Structured_Reasons()
{
    var validators = new[]
    {
        new ValidatorInfo("TestApp.OrderValidator", "Order", "Scoped",
            new[] { new CompositionEdgeInfo("AddressValidator", "SetValidator", false) }),
        new ValidatorInfo("TestApp.AddressValidator", "Address", "Scoped", Array.Empty<CompositionEdgeInfo>())
    };

    var output = CaptureConsoleOutput(() => ValidatorPrinter.WriteWhy("OrderValidator", validators, CreateJsonOutput()));

    output.Should().Contain("\"validator\"");
    output.Should().Contain("\"lifetime\"");
    output.Should().Contain("\"reasons\"");
    output.Should().Contain("\"compositionMethod\"");
}
```

- [ ] **Step 2: Run the validator unit tests and confirm the current JSON contract is too thin**

Run: `dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~ValidatorCommandTests --verbosity minimal`

Expected: FAIL because current JSON is an array for graph mode and a free-form explanation string for `--why`.

- [ ] **Step 3: Return structured lifetime traces from `ValidatorInspector`**

```csharp
// IoCTools.Tools.Cli/Utilities/ValidatorInspector.cs
internal sealed record ValidatorLifetimeTrace(
    string Validator,
    string? Lifetime,
    bool Found,
    IReadOnlyList<ValidatorLifetimeReason> Reasons);

internal sealed record ValidatorLifetimeReason(
    string ChildValidator,
    string? ChildLifetime,
    string CompositionMethod,
    bool IsDirect,
    string Message);

public static ValidatorLifetimeTrace TraceLifetimeReport(string validatorName, IReadOnlyList<ValidatorInfo> validators)
{
    var byName = validators.ToDictionary(v => v.FullName, StringComparer.Ordinal);
    var shortNameMap = validators.ToDictionary(v => v.FullName.Split('.').Last(), v => v.FullName, StringComparer.Ordinal);

    if (!byName.TryGetValue(validatorName, out var target) &&
        !(shortNameMap.TryGetValue(validatorName, out var fullName) && byName.TryGetValue(fullName, out target)))
    {
        target = validators.FirstOrDefault(v =>
            v.FullName.EndsWith("." + validatorName, StringComparison.Ordinal) ||
            v.FullName.Equals(validatorName, StringComparison.OrdinalIgnoreCase));
    }

    if (target is null)
        return new ValidatorLifetimeTrace(validatorName, null, false, Array.Empty<ValidatorLifetimeReason>());

    var reasons = new List<ValidatorLifetimeReason>();
    foreach (var edge in target.CompositionEdges)
    {
        var childFullName = byName.ContainsKey(edge.ChildValidatorType)
            ? edge.ChildValidatorType
            : shortNameMap.GetValueOrDefault(edge.ChildValidatorType);
        byName.TryGetValue(childFullName ?? string.Empty, out var child);

        reasons.Add(new ValidatorLifetimeReason(
            child?.FullName ?? edge.ChildValidatorType,
            child?.Lifetime,
            edge.CompositionMethod,
            edge.IsDirect,
            $"{target.FullName} composes {child?.FullName ?? edge.ChildValidatorType} via {edge.CompositionMethod}" +
            (edge.IsDirect ? " (direct instantiation)" : " (injected)")));
    }

    return new ValidatorLifetimeTrace(target.FullName, target.Lifetime, true, reasons);
}
```

- [ ] **Step 4: Update `ValidatorPrinter` JSON mode to emit explicit graph/why payloads**

```csharp
// IoCTools.Tools.Cli/Utilities/ValidatorPrinter.cs
public static void WriteGraph(IReadOnlyList<ValidatorInfo> validators, OutputContext output)
{
    var tree = ValidatorInspector.BuildCompositionTree(validators);

    if (output.IsJson)
    {
        output.WriteJson(new
        {
            validatorCount = validators.Count,
            roots = tree.Select(BuildJsonNode)
        });
        return;
    }

    foreach (var root in tree)
        PrintTreeNode(root, output, string.Empty, true);
}

public static void WriteWhy(string validatorName, IReadOnlyList<ValidatorInfo> validators, OutputContext output)
{
    var report = ValidatorInspector.TraceLifetimeReport(validatorName, validators);

    if (output.IsJson)
    {
        output.WriteJson(report);
        return;
    }

    if (!report.Found)
    {
        output.WriteLine($"Validator '{validatorName}' not found.");
        return;
    }

    if (report.Reasons.Count == 0)
    {
        output.WriteLine($"{report.Validator} is {report.Lifetime} (set directly via the lifetime attribute).");
        return;
    }

    output.WriteLine($"{report.Validator} is {report.Lifetime} because:");
    foreach (var reason in report.Reasons)
        output.WriteLine($"  - {reason.Message}");
}
```

- [ ] **Step 5: Re-run the validator tests and make sure both human and JSON modes still work**

Run: `dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~ValidatorCommandTests --verbosity minimal`

Expected: PASS.

- [ ] **Step 6: Commit the validator JSON hardening**

```bash
git add IoCTools.Tools.Cli/Utilities/ValidatorInspector.cs \
        IoCTools.Tools.Cli/Utilities/ValidatorPrinter.cs \
        IoCTools.Tools.Cli.Tests/ValidatorCommandTests.cs
git commit -m "feat: add structured validator json output"
```

## Task 4: Harden Suppress JSON Output

**Files:**
- Create: `IoCTools.Tools.Cli.Tests/CliSuppressCommandTests.cs`
- Modify: `IoCTools.Tools.Cli/Utilities/SuppressPrinter.cs`
- Modify: `IoCTools.Tools.Cli/Utilities/UsagePrinter.cs`

- [ ] **Step 1: Write failing suppress tests for structured JSON and append metadata**

```csharp
// IoCTools.Tools.Cli.Tests/CliSuppressCommandTests.cs
namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;
using Infrastructure;
using Xunit;

[Collection("CLI Execution")]
public sealed class CliSuppressCommandTests
{
    private static string RegistrationProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "RegistrationProject", "RegistrationProject.csproj");

    [Fact]
    public async Task Suppress_Json_Emits_Structured_Metadata()
    {
        var result = await CliTestHost.RunAsync(
            "suppress",
            "--project", RegistrationProjectPath,
            "--codes", "IOC035,IOC053",
            "--json");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("\"filters\"");
        result.Stdout.Should().Contain("\"rules\"");
        result.Stdout.Should().Contain("\"selectedByCode\"");
        result.Stdout.Should().Contain("\"editorconfig\"");
    }

    [Fact]
    public async Task Suppress_Output_File_Reports_Skipped_Rules()
    {
        var tempEditorConfig = Path.Combine(TestPaths.CreateTempDirectory(), ".editorconfig");
        await File.WriteAllTextAsync(tempEditorConfig, "dotnet_diagnostic.IOC035.severity = none" + Environment.NewLine);

        var result = await CliTestHost.RunAsync(
            "suppress",
            "--project", RegistrationProjectPath,
            "--codes", "IOC035,IOC053",
            "--output", tempEditorConfig,
            "--json");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("\"existingRulesSkipped\": 1");
    }
}
```

- [ ] **Step 2: Run the suppress tests and confirm the current JSON is insufficient**

Run: `dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~CliSuppressCommandTests --verbosity minimal`

Expected: FAIL because current JSON only returns flat rules and raw editorconfig text.

- [ ] **Step 3: Introduce a real suppression report model and keep text mode unchanged**

```csharp
// IoCTools.Tools.Cli/Utilities/SuppressPrinter.cs
internal sealed record SuppressReport(
    SuppressFilters Filters,
    IReadOnlyList<SuppressRule> Rules,
    string EditorConfig,
    string? OutputPath,
    int ExistingRulesSkipped,
    bool UsedLiveDiagnostics);

internal sealed record SuppressRule(
    string Id,
    string Title,
    string Category,
    string DefaultSeverity,
    bool SelectedBySeverity,
    bool SelectedByCode,
    bool ExplicitErrorOverride,
    string EditorConfigLine);
```

- [ ] **Step 4: Build JSON and file-append behavior from the structured report**

```csharp
// IoCTools.Tools.Cli/Utilities/SuppressPrinter.cs
public static int Write(SuppressCommandOptions options, OutputContext output, IReadOnlyList<string>? liveDiagnosticIds = null)
{
    var report = BuildReport(options, liveDiagnosticIds);

    if (report.Rules.Count == 0)
    {
        output.WriteError("No diagnostics match the specified filters.");
        return 1;
    }

    if (report.OutputPath is not null)
        return AppendToFile(report, output);

    if (output.IsJson)
    {
        output.WriteJson(report);
        return 0;
    }

    Console.Write(report.EditorConfig);
    return 0;
}
```

- [ ] **Step 5: Re-run the suppress tests and make sure live/file metadata is preserved**

Run: `dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~CliSuppressCommandTests --verbosity minimal`

Expected: PASS.

- [ ] **Step 6: Commit the suppress JSON hardening**

```bash
git add IoCTools.Tools.Cli/Utilities/SuppressPrinter.cs \
        IoCTools.Tools.Cli/Utilities/UsagePrinter.cs \
        IoCTools.Tools.Cli.Tests/CliSuppressCommandTests.cs
git commit -m "feat: enrich suppress json output"
```

## Task 5: Add Compatibility Diagnostics, Update Docs, And Align `1.5.0` Versions

**Files:**
- Create: `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/InjectCompatibilityValidator.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticRules.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticsRunner.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/DependencyDiagnostics.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/ConfigurationDiagnostics.cs`
- Modify: `IoCTools.Tools.Cli/Utilities/DiagnosticCatalog.cs`
- Create: `IoCTools.Generator.Tests/InjectCompatibilityGuidanceTests.cs`
- Modify: `IoCTools.Generator.Tests/InjectUsageDiagnosticTests.cs`
- Modify: `IoCTools.Abstractions/IoCTools.Abstractions.csproj`
- Modify: `IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj`
- Modify: `IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/cli-reference.md`
- Modify: `docs/attributes.md`
- Modify: `docs/getting-started.md`
- Modify: `docs/migration.md`
- Modify: `docs/testing.md`
- Modify: `docs/diagnostics.md`

- [ ] **Step 1: Write failing generator tests for supported-but-noisy compatibility guidance**

```csharp
// IoCTools.Generator.Tests/InjectCompatibilityGuidanceTests.cs
namespace IoCTools.Generator.Tests;

public sealed class InjectCompatibilityGuidanceTests
{
    [Fact]
    public void Inject_Field_Emits_Compatibility_Diagnostic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace Test;
public interface IClock { }
[Scoped]
public partial class BillingService
{
    [Inject] private readonly IClock _clock;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode(""IOC095"").Should().ContainSingle();
        result.GetDiagnosticsByCode(""IOC095"")[0].GetMessage().Should().Contain(""never use [Inject]"");
        result.GetDiagnosticsByCode(""IOC095"")[0].GetMessage().Should().Contain(""DependsOn"");
    }

    [Fact]
    public void InjectConfiguration_Field_Emits_Compatibility_Diagnostic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace Test;
[Scoped]
public partial class BillingService
{
    [InjectConfiguration(""Billing:RetryCount"")] private readonly int _retryCount;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode(""IOC096"").Should().ContainSingle();
        result.GetDiagnosticsByCode(""IOC096"")[0].GetMessage().Should().Contain(""DependsOnConfiguration"");
    }
}
```

- [ ] **Step 2: Run the focused generator tests and confirm the compatibility diagnostics do not exist yet**

Run: `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter FullyQualifiedName~InjectCompatibilityGuidanceTests --verbosity minimal`

Expected: FAIL because `IOC095` and `IOC096` do not exist.

- [ ] **Step 3: Add non-breaking compatibility diagnostics and update existing wording**

```csharp
// IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs
public static readonly DiagnosticDescriptor InjectCompatibilityOnly = new(
    "IOC095",
    "Inject is compatibility-only",
    "Field '{0}' on class '{1}' uses [Inject]. Never use [Inject] in new code; prefer [DependsOn<{2}>] unless a compatibility constraint blocks migration.",
    "IoCTools.Registration",
    DiagnosticSeverity.Info,
    true,
    "Use [DependsOn] for new code. Keep [Inject] only to preserve compatibility until the 2.0 removal window.",
    "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc095");
```

```csharp
// IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/ConfigurationDiagnostics.cs
public static readonly DiagnosticDescriptor InjectConfigurationCompatibilityOnly = new(
    "IOC096",
    "InjectConfiguration is compatibility-only",
    "Field '{0}' on class '{1}' uses [InjectConfiguration]. Never use InjectConfiguration in new code; prefer [DependsOnConfiguration<{2}>] or [DependsOnOptions<{2}>].",
    "IoCTools.Configuration",
    DiagnosticSeverity.Info,
    true,
    "Use [DependsOnConfiguration] or [DependsOnOptions] for new code. Keep InjectConfiguration only to preserve compatibility until the 2.0 removal window.",
    "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc096");
```

```csharp
// IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/InjectCompatibilityValidator.cs
internal static class InjectCompatibilityValidator
{
    internal static void Validate(SourceProductionContext context, TypeDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol)
    {
        foreach (var field in classSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            var location = field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().GetLocation() ??
                           classDeclaration.GetLocation();

            if (field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InjectCompatibilityOnly,
                    location,
                    field.Name,
                    classSymbol.Name,
                    field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }

            if (field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectConfigurationAttribute"))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InjectConfigurationCompatibilityOnly,
                    location,
                    field.Name,
                    classSymbol.Name,
                    field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }
    }
}
```

```csharp
// IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticsRunner.cs
DiagnosticRules.ValidateInjectFieldPreferences(context, classDeclaration, classSymbol);
DiagnosticRules.ValidateInjectCompatibilityUsage(context, classDeclaration, classSymbol);
DiagnosticRules.ValidateConfigurationInjection(context, classDeclaration, classSymbol);
```

```csharp
// RegistrationDiagnostics.cs / DependencyDiagnostics.cs
public static readonly DiagnosticDescriptor RegisterAsRequiresService = new(
    "IOC028",
    "RegisterAs attribute requires service indicators",
    "Class '{0}' has [RegisterAs] but lacks a service indicator such as [Scoped], [Singleton], [Transient], [DependsOn], [DependsOnConfiguration], or another registration attribute.",
    "IoCTools.Registration",
    DiagnosticSeverity.Error,
    true,
    "Add a lifetime attribute and prefer [DependsOn]/[DependsOnConfiguration] for constructor intent. Keep [Inject] only for legacy compatibility.",
    "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc028");

public static readonly DiagnosticDescriptor DependsOnMissingLifetime = new(
    "IOC070",
    "DependsOn/Inject used without lifetime",
    "Class '{0}' declares constructor intent but has no lifetime attribute. Add [Scoped], [Singleton], or [Transient].",
    "IoCTools.Registration",
    DiagnosticSeverity.Warning,
    true,
    "Add a lifetime and prefer [DependsOn]/[DependsOnConfiguration] for new code. [Inject] remains compatibility-only in 1.5.0.",
    "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc070");

public static readonly DiagnosticDescriptor NonServiceDependencyType = new(
    "IOC044",
    "Dependency type is not a service",
    "Dependency '{0}' on class '{1}' is a primitive/value type or string. Use [DependsOnConfiguration<...>] for configuration values or depend on an interface/class service instead.",
    "IoCTools.Dependency",
    DiagnosticSeverity.Warning,
    true,
    "Reserve [DependsOn] for services. For configuration values, switch to [DependsOnConfiguration<...>] and keep InjectConfiguration only for legacy compatibility.",
    "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc044");
```

- [ ] **Step 4: Update the docs and package metadata to match the new posture**

```xml
<!-- IoCTools.Abstractions/IoCTools.Abstractions.csproj -->
<Version>1.5.0</Version>
<PackageVersion>1.5.0</PackageVersion>
<AssemblyVersion>1.5.0.0</AssemblyVersion>
<FileVersion>1.5.0.0</FileVersion>
<PackageReleaseNotes>v1.5.0: evidence bundles, structured CLI JSON, FluentValidation graph hardening, and compatibility-only guidance for [Inject]/InjectConfiguration.</PackageReleaseNotes>
```

```xml
<!-- IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj -->
<Version>1.5.0</Version>
<PackageVersion>1.5.0</PackageVersion>
<AssemblyVersion>1.5.0.0</AssemblyVersion>
<FileVersion>1.5.0.0</FileVersion>
```

```xml
<!-- IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj -->
<Version>1.5.0</Version>
<Description>Developer-facing CLI for IoCTools: evidence, fields, services, explain, graph, why, doctor, compare, profile, config-audit, suppress, validators.</Description>
```

```md
<!-- docs/attributes.md -->
### `[Inject]`

Compatibility-only escape hatch. Never use `[Inject]` in new code.
Use `[DependsOn<T>]` instead.

### `[InjectConfiguration(...)]`

Compatibility-only escape hatch. Never use `InjectConfiguration` in new code.
Use `[DependsOnConfiguration<T>]` or `[DependsOnOptions<T>]` instead.
```

```md
<!-- docs/migration.md -->
| Manual DI | IoCTools |
|-----------|----------|
| `IConfiguration[""key""]` | `[DependsOnConfiguration<string>(""key"")]` |
| Manual constructor field | `[DependsOn<T1, T2>]` |
| Legacy `[Inject]` | Migrate to `[DependsOn<T>]` |
```

- [ ] **Step 5: Run focused generator/CLI tests and doc hygiene checks**

Run: `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter FullyQualifiedName~InjectCompatibilityGuidanceTests --verbosity minimal`

Expected: PASS.

Run: `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter FullyQualifiedName~InjectUsageDiagnosticTests --verbosity minimal`

Expected: PASS.

Run: `dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~CliEvidenceCommandTests|FullyQualifiedName~CliSuppressCommandTests|FullyQualifiedName~ValidatorCommandTests --verbosity minimal`

Expected: PASS.

Run: `git diff --check`

Expected: no whitespace or merge-marker errors.

- [ ] **Step 6: Commit the compatibility guidance, docs, and version alignment**

```bash
git add IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/InjectCompatibilityValidator.cs \
        IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticRules.cs \
        IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticsRunner.cs \
        IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs \
        IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/DependencyDiagnostics.cs \
        IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/ConfigurationDiagnostics.cs \
        IoCTools.Tools.Cli/Utilities/DiagnosticCatalog.cs \
        IoCTools.Generator.Tests/InjectCompatibilityGuidanceTests.cs \
        IoCTools.Generator.Tests/InjectUsageDiagnosticTests.cs \
        IoCTools.Abstractions/IoCTools.Abstractions.csproj \
        IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj \
        IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj \
        README.md CHANGELOG.md docs/cli-reference.md docs/attributes.md docs/getting-started.md docs/migration.md docs/testing.md docs/diagnostics.md
git commit -m "feat: add inject compatibility guidance"
```

## Task 6: Run Full Verification And Rehearse The `1.5.0` Release Build

**Files:**
- Verify previously changed files only; do not commit generated `.nupkg` artifacts.

- [ ] **Step 1: Run the full CLI and generator test suites in release-shaped mode**

Run: `dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj -c Release --verbosity minimal`

Expected: PASS.

Run: `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj -c Release --verbosity minimal`

Expected: PASS.

- [ ] **Step 2: Run the adjacent validation suites most likely to catch integration regressions**

Run: `dotnet test IoCTools.FluentValidation.Tests/IoCTools.FluentValidation.Tests.csproj -c Release --verbosity minimal`

Expected: PASS.

Run: `dotnet test IoCTools.Testing.Tests/IoCTools.Testing.Tests.csproj -c Release --verbosity minimal`

Expected: PASS.

- [ ] **Step 3: Run the full solution test/build gate**

Run: `dotnet test IoCTools.sln -c Release --verbosity minimal`

Expected: PASS.

Run: `dotnet build IoCTools.sln -c Release --no-restore --verbosity minimal`

Expected: PASS.

- [ ] **Step 4: Pack the shipping artifacts and verify the version line**

Run: `dotnet pack IoCTools.Abstractions/IoCTools.Abstractions.csproj -c Release -o ./artifacts/release`

Expected: produce `IoCTools.Abstractions.1.5.0.nupkg`.

Run: `dotnet pack IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj -c Release -o ./artifacts/release`

Expected: produce `IoCTools.Generator.1.5.0.nupkg`.

Run: `dotnet pack IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj -c Release -o ./artifacts/release`

Expected: produce `IoCTools.Tools.Cli.1.5.0.nupkg`.

Run: `ls artifacts/release | rg "1\\.5\\.0"`

Expected: one line per package with `1.5.0` in the filename.

- [ ] **Step 5: Run final hygiene checks and commit the finished release pass**

Run: `git status --short`

Expected: only intentional tracked changes plus local artifacts you do not plan to commit.

Run: `git diff --check`

Expected: clean.

```bash
git add IoCTools.Abstractions/IoCTools.Abstractions.csproj \
        IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj \
        IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj \
        IoCTools.Tools.Cli \
        IoCTools.Tools.Cli.Tests \
        IoCTools.Generator/IoCTools.Generator \
        IoCTools.Generator.Tests \
        README.md CHANGELOG.md docs
git commit -m "release: prepare ioc tools 1.5.0"
```

## Verification

- `dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj -c Release`
- `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj -c Release`
- `dotnet test IoCTools.FluentValidation.Tests/IoCTools.FluentValidation.Tests.csproj -c Release`
- `dotnet test IoCTools.Testing.Tests/IoCTools.Testing.Tests.csproj -c Release`
- `dotnet test IoCTools.sln -c Release`
- `dotnet build IoCTools.sln -c Release --no-restore`
- `dotnet pack IoCTools.Abstractions/IoCTools.Abstractions.csproj -c Release -o ./artifacts/release`
- `dotnet pack IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj -c Release -o ./artifacts/release`
- `dotnet pack IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj -c Release -o ./artifacts/release`
- `git diff --check`

## Success Criteria

- `ioc-tools evidence` emits a correlated human-readable bundle and a machine-readable JSON bundle.
- Evidence bundles surface services, type evidence, diagnostics, config audit results, validator evidence, artifact evidence, and migration hints without scraping human console output.
- `validator-graph --json` and `validator-graph --why --json` emit structured data, not free-form strings.
- `suppress --json` emits structured rule metadata, filter metadata, append metadata, and the generated `.editorconfig` content.
- `[Inject]` and `InjectConfiguration` remain supported in `1.5.0`, but new diagnostics and docs clearly tell users never to use them in new code.
- `IoCTools.Abstractions`, `IoCTools.Generator`, and `IoCTools.Tools.Cli` all pack as `1.5.0`.
- All relevant test suites pass and the release pack step succeeds.
