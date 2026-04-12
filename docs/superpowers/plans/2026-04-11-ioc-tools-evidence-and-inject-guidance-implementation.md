# IoCTools Evidence And Inject Guidance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the approved `1.5.0` IoCTools evidence bundle, structured validator/suppression JSON, compatibility-only `[Inject]` guidance, and aligned release/docs metadata without breaking existing users.

**Architecture:** Extend the existing CLI command router and parser with one compositional `evidence` command instead of inventing a new analysis pipeline. Reuse current project loading, field inspection, registration summary, diagnostic runner, validator inspection, compare/profile/config-audit surfaces, then add small DTO/printer helpers to produce stable text and JSON contracts. Keep migration pressure non-breaking by surfacing explicit guidance in diagnostics, evidence output, and docs while preserving runtime support for `[Inject]` and `InjectConfiguration`.

**Tech Stack:** .NET 9 CLI host, Roslyn compilation inspection, xUnit, FluentAssertions, existing IoCTools generator diagnostics, markdown docs, NuGet package metadata.

---

### Task 1: Add the `evidence` command contract and CLI composition surface

**Files:**
- Create: `IoCTools.Tools.Cli/Utilities/EvidenceModels.cs`
- Create: `IoCTools.Tools.Cli/Utilities/EvidencePrinter.cs`
- Modify: `IoCTools.Tools.Cli/Program.cs`
- Modify: `IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs`
- Modify: `IoCTools.Tools.Cli/Utilities/UsagePrinter.cs`
- Test: `IoCTools.Tools.Cli.Tests/CliEvidenceCommandTests.cs`

- [ ] **Step 1: Write the failing parser and help tests**

```csharp
[Fact]
public void ParseEvidence_RequiresProject_And_AcceptsTypeSettingsBaselineOutput()
{
    var parse = CommandLineParser.ParseEvidence(new[]
    {
        "--project", "/tmp/app.csproj",
        "--type", "MyApp.BillingService",
        "--settings", "/tmp/appsettings.json",
        "--baseline", "/tmp/baseline",
        "--output", "/tmp/out",
        "--json"
    });

    parse.Success.Should().BeTrue();
    parse.Value!.Common.ProjectPath.Should().Be("/tmp/app.csproj");
    parse.Value.TypeName.Should().Be("MyApp.BillingService");
    parse.Value.SettingsPath.Should().Be("/tmp/appsettings.json");
    parse.Value.BaselineDirectory.Should().Be("/tmp/baseline");
    parse.Value.OutputDirectory.Should().Be("/tmp/out");
    parse.Value.Common.Json.Should().BeTrue();
}

[Fact]
public async Task Help_Includes_Evidence_Command()
{
    var result = await CliTestHost.RunAsync("help");

    result.ExitCode.Should().Be(0);
    result.Stdout.Should().Contain("dotnet ioc-tools evidence --project <csproj>");
}
```

- [ ] **Step 2: Run the targeted CLI tests and confirm the new tests fail for the missing command**

Run: `env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter "FullyQualifiedName~CliEvidenceCommandTests|FullyQualifiedName~Help_Includes_Evidence_Command" --logger "console;verbosity=minimal"`

Expected: FAIL with missing `ParseEvidence`, missing `evidence` usage text, or unsupported command routing.

- [ ] **Step 3: Add the new command option record, parser, and usage line**

```csharp
internal sealed record EvidenceCommandOptions(
    CommonOptions Common,
    string? TypeName,
    string? SettingsPath,
    string? BaselineDirectory,
    string? OutputDirectory);

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

    return ParseResult<EvidenceCommandOptions>.Ok(new EvidenceCommandOptions(common, typeName, settings, baseline, output));
}
```

- [ ] **Step 4: Add the failing end-to-end command test for text mode**

```csharp
[Fact]
public async Task Evidence_TextMode_Prints_Correlated_Sections()
{
    var result = await CliTestHost.RunAsync(
        "evidence",
        "--project", FieldsProjectPath,
        "--type", "FieldsProject.Services.TelemetryReporter");

    result.ExitCode.Should().Be(0);
    result.Stdout.Should().Contain("Project");
    result.Stdout.Should().Contain("Services");
    result.Stdout.Should().Contain("Type Evidence");
    result.Stdout.Should().Contain("Diagnostics");
    result.Stdout.Should().Contain("Migration Hints");
}
```

- [ ] **Step 5: Implement the evidence orchestration with reusable models/printer**

```csharp
private static async Task<int> RunEvidenceAsync(string[] args, CancellationToken token)
{
    var parse = CommandLineParser.ParseEvidence(args);
    if (!parse.Success)
        return UsagePrinter.ExitWithError(parse.Error);

    var options = parse.Value!;
    var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
    await using var context = await ProjectContext.CreateAsync(options.Common, token);

    var bundle = await EvidencePrinter.BuildAsync(context, options, token);
    EvidencePrinter.Write(bundle, output);
    output.ReportTiming("evidence command completed");

    return bundle.Diagnostics.HasErrors ? 1 : 0;
}
```

- [ ] **Step 6: Re-run the evidence command tests and make them pass**

Run: `env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~CliEvidenceCommandTests --logger "console;verbosity=minimal"`

Expected: PASS with text-mode evidence output covering the required sections.

### Task 2: Tighten machine-readable validator and suppression contracts

**Files:**
- Modify: `IoCTools.Tools.Cli/Utilities/ValidatorInspector.cs`
- Modify: `IoCTools.Tools.Cli/Utilities/ValidatorPrinter.cs`
- Modify: `IoCTools.Tools.Cli/Utilities/SuppressPrinter.cs`
- Modify: `IoCTools.Tools.Cli/Utilities/DiagnosticCatalog.cs`
- Test: `IoCTools.Tools.Cli.Tests/ValidatorCommandTests.cs`
- Test: `IoCTools.Tools.Cli.Tests/CliExtendedCommandTests.cs`

- [ ] **Step 1: Write failing JSON contract tests for `validator-graph --json`, `validator-graph --why --json`, and `suppress --json`**

```csharp
[Fact]
public void WriteGraph_JsonMode_Emits_Resolved_Node_Metadata()
{
    var validators = new[]
    {
        new ValidatorInfo("TestApp.OrderValidator", "Order", "Scoped",
            new[] { new CompositionEdgeInfo("AddressValidator", "SetValidator", false) }),
        new ValidatorInfo("TestApp.AddressValidator", "Address", "Scoped", Array.Empty<CompositionEdgeInfo>())
    };

    var json = CaptureConsoleOutput(() => ValidatorPrinter.WriteGraph(validators, CreateJsonOutput()));

    json.Should().Contain("\"validator\": \"TestApp.OrderValidator\"");
    json.Should().Contain("\"resolved\":");
    json.Should().Contain("\"isDirect\": false");
}

[Fact]
public void WriteWhy_JsonMode_Emits_Structured_Explanation()
{
    var json = CaptureConsoleOutput(() => ValidatorPrinter.WriteWhy("OrderValidator", validators, CreateJsonOutput()));

    json.Should().Contain("\"validator\": \"TestApp.OrderValidator\"");
    json.Should().Contain("\"steps\":");
    json.Should().Contain("\"reason\":");
}
```

- [ ] **Step 2: Run the targeted validator/suppress tests and confirm the JSON shape is too weak**

Run: `env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter "FullyQualifiedName~ValidatorCommandTests|FullyQualifiedName~Suppress" --logger "console;verbosity=minimal"`

Expected: FAIL because the current JSON payloads are missing structured explanation metadata and suppression selection/risk details.

- [ ] **Step 3: Add structured validator explanation and graph DTOs**

```csharp
internal sealed record ValidatorWhyStep(
    string Kind,
    string Target,
    string Method,
    bool IsDirect,
    string? Lifetime);

internal sealed record ValidatorWhyExplanation(
    string Validator,
    string? Lifetime,
    string Reason,
    IReadOnlyList<ValidatorWhyStep> Steps);
```

- [ ] **Step 4: Implement contract-rich JSON emission for validator graph and suppression**

```csharp
if (output.IsJson)
{
    output.WriteJson(new
    {
        rules = filtered.Select(e => new
        {
            id = e.Id,
            title = e.Title,
            category = e.Category,
            defaultSeverity = e.DefaultSeverity,
            suppressedSeverity = "none",
            selectionReason = BuildSelectionReason(e, severityFilter, codeFilter, liveDiagnosticIds),
            isErrorByDefault = string.Equals(e.DefaultSeverity, "Error", StringComparison.OrdinalIgnoreCase),
            riskNote = BuildRiskNote(e, codeFilter)
        }),
        editorconfig = content
    });
    return 0;
}
```

- [ ] **Step 5: Re-run the targeted JSON contract tests and make them pass**

Run: `env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter "FullyQualifiedName~ValidatorCommandTests|FullyQualifiedName~CliExtendedCommandTests" --logger "console;verbosity=minimal"`

Expected: PASS with stable JSON payload assertions.

### Task 3: Add migration hints and non-breaking guidance for `[Inject]` and `InjectConfiguration`

**Files:**
- Modify: `IoCTools.Tools.Cli/Utilities/EvidenceModels.cs`
- Modify: `IoCTools.Tools.Cli/Utilities/EvidencePrinter.cs`
- Modify: `IoCTools.Generator.Tests/DiagnosticSuggestionTests.cs`
- Modify: `IoCTools.Generator.Tests/ComprehensiveConfigurationInjectionTests.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Descriptors/DiagnosticDescriptors.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/DependencyUsageValidator.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/ConfigurationInjectionValidator.cs`

- [ ] **Step 1: Write failing tests proving compatibility-only guidance appears without breaking builds**

```csharp
[Fact]
public void InjectField_DefaultPattern_Produces_DependsOn_Guidance()
{
    var result = SourceGeneratorTestHelper.CompileWithGenerator(@"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

[Scoped]
public partial class BillingService
{
    [Inject] private readonly ILogger<BillingService> _logger;
}");

    result.Diagnostics.Should().Contain(d =>
        d.Id == "IOC035" &&
        d.GetMessage().Contains("use [DependsOn]", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Run the targeted generator tests and verify the guidance wording is missing or too weak**

Run: `env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter "FullyQualifiedName~DiagnosticSuggestionTests|FullyQualifiedName~ConfigurationInjection" --logger "console;verbosity=minimal"`

Expected: FAIL because the current messages do not explicitly impose the new compatibility-only posture.

- [ ] **Step 3: Implement explicit migration hint extraction for the evidence command**

```csharp
private static IReadOnlyList<MigrationHint> BuildMigrationHints(IReadOnlyList<ServiceFieldReport> reports)
{
    return reports
        .SelectMany(report => report.DependencyFields
            .Where(field => string.Equals(field.Source, "Inject", StringComparison.OrdinalIgnoreCase))
            .Select(field => new MigrationHint(
                report.TypeName,
                "Inject",
                field.FieldName,
                $"Prefer [DependsOn<{field.TypeName}>] instead of [Inject].")))
        .ToList();
}
```

- [ ] **Step 4: Tighten diagnostic wording without changing severity or breaking support**

```csharp
private static readonly DiagnosticDescriptor InjectFieldCouldUseDependsOn =
    new(
        "IOC035",
        "Inject field could use DependsOn",
        "Field '{0}' uses [Inject]. [Inject] is compatibility-only; never use it in new code. Prefer [DependsOn<{1}>].",
        Categories.Dependency,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
```

- [ ] **Step 5: Re-run the targeted guidance tests and make them pass**

Run: `env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter "FullyQualifiedName~DiagnosticSuggestionTests|FullyQualifiedName~ConfigurationInjection" --logger "console;verbosity=minimal"`

Expected: PASS with explicit “never use `[Inject]` in new code” / “prefer `[DependsOnConfiguration]` or `[DependsOnOptions]`” messaging while existing scenarios still compile.

### Task 4: Align docs, samples, and `1.5.0` release metadata

**Files:**
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/getting-started.md`
- Modify: `docs/attributes.md`
- Modify: `docs/testing.md`
- Modify: `docs/diagnostics.md`
- Modify: `docs/migration.md`
- Modify: `docs/cli-reference.md`
- Modify: `IoCTools.Abstractions/IoCTools.Abstractions.csproj`
- Modify: `IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj`
- Modify: `IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj`
- Modify: `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorGenerator.Rendering.cs`

- [ ] **Step 1: Write failing documentation/release assertions in CLI tests where practical**

```csharp
[Fact]
public async Task Help_And_Readme_Reflect_Evidence_Command()
{
    var result = await CliTestHost.RunAsync("help");

    result.Stdout.Should().Contain("evidence");
    File.ReadAllText("README.md").Should().Contain("never use [Inject] in new code");
}
```

- [ ] **Step 2: Update the release line and generated-code version markers to `1.5.0`**

```xml
<Version>1.5.0</Version>
<PackageVersion>1.5.0</PackageVersion>
<AssemblyVersion>1.5.0.0</AssemblyVersion>
<FileVersion>1.5.0.0</FileVersion>
<PackageReleaseNotes>v1.5.0: add the evidence command, strengthen machine-readable validator and suppression output, and make [Inject]/InjectConfiguration compatibility-only guidance explicit.</PackageReleaseNotes>
```

- [ ] **Step 3: Retcon the docs so `[DependsOn]` / `[DependsOnConfiguration]` are the normal path everywhere**

```md
- Never use `[Inject]` in new code.
- Never use `InjectConfiguration` in new code.
- Use `[DependsOn]` for service dependencies.
- Use `[DependsOnConfiguration]` or `[DependsOnOptions]` for configuration dependencies.
- `[Inject]` and `InjectConfiguration` remain supported in `1.5.0` for compatibility-only migration scenarios.
```

- [ ] **Step 4: Re-run a light verification pass over the release/documentation touchpoints**

Run: `git diff --check`

Expected: PASS with no whitespace or patch formatting errors.

### Task 5: Full verification and release readiness

**Files:**
- Modify: `CHANGELOG.md`
- Modify: `README.md`
- Modify: `docs/cli-reference.md`
- Test: `IoCTools.Tools.Cli.Tests/CliEvidenceCommandTests.cs`
- Test: `IoCTools.Tools.Cli.Tests/ValidatorCommandTests.cs`
- Test: `IoCTools.Generator.Tests/DiagnosticSuggestionTests.cs`

- [ ] **Step 1: Run the focused CLI suite for the new command contracts**

Run: `env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --logger "console;verbosity=minimal"`

Expected: PASS with evidence, validator, suppress, and usage coverage green.

- [ ] **Step 2: Run the generator suite for migration guidance stability**

Run: `env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --logger "console;verbosity=minimal"`

Expected: PASS with compatibility-only guidance tests green.

- [ ] **Step 3: Run the full solution sequentially**

Run: `env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.sln --logger "console;verbosity=minimal" -m:1`

Expected: PASS across generator, CLI, testing, and FluentValidation projects.

- [ ] **Step 4: Build the CLI package on the release line**

Run: `env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj -c Release -o ./artifacts`

Expected: PASS with a `1.5.0` tool package artifact in `./artifacts`.

- [ ] **Step 5: Summarize the changed files and any follow-on IoCTools feature ideas**

```text
- list every changed file for the user’s active parallel work
- call out the exact verification commands that passed
- separate shipped 1.5.0 work from forward-looking ideas for 2.0+
```
