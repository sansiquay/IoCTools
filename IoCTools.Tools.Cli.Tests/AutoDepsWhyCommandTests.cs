namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using Xunit;

/// <summary>
/// Phase 7 Task 7.2 — <c>why</c> must emit a structured source-attribution block when
/// the queried dependency is sourced from the auto-deps resolver (built-in, universal,
/// profile, or transitive) rather than an explicit <c>[DependsOn]</c>.
/// </summary>
[Collection("CLI Execution")]
public sealed class AutoDepsWhyCommandTests
{
    private static string AutoDepsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "AutoDepsProject", "AutoDepsProject.csproj");

    private static string FieldsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "FieldsProject", "FieldsProject.csproj");

    [Fact]
    public async Task Why_auto_builtin_ilogger_emits_source_block()
    {
        var result = await CliTestHost.RunAsync(
            "why",
            "--project", AutoDepsProjectPath,
            "--type", "AutoDepsProject.Services.OrderService",
            "--dependency", "ILogger");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("source: auto-builtin:ILogger");
        result.Stdout.Should().Contain("disable detection: IoCToolsAutoDetectLogger=false");
        result.Stdout.Should().Contain("[NoAutoDepOpen(typeof(ILogger<>))]");
    }

    [Fact]
    public async Task Why_explicit_dependency_omits_source_block()
    {
        var result = await CliTestHost.RunAsync(
            "why",
            "--project", FieldsProjectPath,
            "--type", "FieldsProject.Services.TelemetryReporter",
            "--dependency", "IMetricsClient");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Source: DependsOn");
        result.Stdout.Should().NotContain("source: auto-");
    }

    [Fact]
    public async Task Why_with_hide_auto_deps_suppresses_auto_rows()
    {
        var result = await CliTestHost.RunAsync(
            "why",
            "--project", AutoDepsProjectPath,
            "--type", "AutoDepsProject.Services.OrderService",
            "--dependency", "ILogger",
            "--hide-auto-deps");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("No generated dependency matched");
    }

    [Fact]
    public async Task Why_with_only_auto_deps_keeps_only_auto_rows()
    {
        // Query for IPaymentService (explicit via DependsOn) with --only-auto-deps — should be suppressed
        var result = await CliTestHost.RunAsync(
            "why",
            "--project", AutoDepsProjectPath,
            "--type", "AutoDepsProject.Services.OrderService",
            "--dependency", "IPaymentService",
            "--only-auto-deps");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("No generated dependency matched");
    }

    [Fact]
    public async Task Why_json_output_includes_source_tag()
    {
        var result = await CliTestHost.RunAsync(
            "why",
            "--project", AutoDepsProjectPath,
            "--type", "AutoDepsProject.Services.OrderService",
            "--dependency", "ILogger",
            "--json");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("\"sourceTag\"");
        result.Stdout.Should().Contain("auto-builtin:ILogger");
    }
}
