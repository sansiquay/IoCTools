namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using Xunit;

/// <summary>
/// Phase 7 Task 7.4 — <c>explain</c> narrates auto-dep provenance in a dedicated
/// "Auto-dependencies:" section, and the cross-command flags suppress / isolate it.
/// </summary>
[Collection("CLI Execution")]
public sealed class AutoDepsExplainCommandTests
{
    private static string AutoDepsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "AutoDepsProject", "AutoDepsProject.csproj");

    [Fact]
    public async Task Explain_emits_auto_dependencies_section_with_narrative()
    {
        var result = await CliTestHost.RunAsync(
            "explain",
            "--project", AutoDepsProjectPath,
            "--type", "AutoDepsProject.Services.OrderService");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Auto-dependencies:");
        result.Stdout.Should().Contain("is provided by built-in ILogger detection");
    }

    [Fact]
    public async Task Explain_with_hide_auto_deps_suppresses_section()
    {
        var result = await CliTestHost.RunAsync(
            "explain",
            "--project", AutoDepsProjectPath,
            "--type", "AutoDepsProject.Services.OrderService",
            "--hide-auto-deps");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().NotContain("Auto-dependencies:");
        result.Stdout.Should().NotContain("built-in ILogger detection");
        result.Stdout.Should().Contain("Dependencies:"); // explicit section still printed
    }

    [Fact]
    public async Task Explain_with_only_auto_deps_hides_explicit_and_config()
    {
        var result = await CliTestHost.RunAsync(
            "explain",
            "--project", AutoDepsProjectPath,
            "--type", "AutoDepsProject.Services.OrderService",
            "--only-auto-deps");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Auto-dependencies:");
        result.Stdout.Should().NotContain("Dependencies:\n");
        result.Stdout.Should().NotContain("Configuration:\n");
    }

    [Fact]
    public async Task Explain_json_payload_splits_explicit_and_auto()
    {
        var result = await CliTestHost.RunAsync(
            "explain",
            "--project", AutoDepsProjectPath,
            "--type", "AutoDepsProject.Services.OrderService",
            "--json");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("\"autoDependencies\"");
        result.Stdout.Should().Contain("auto-builtin:ILogger");
    }
}
