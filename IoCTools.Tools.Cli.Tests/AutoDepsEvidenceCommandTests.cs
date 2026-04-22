namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using Xunit;

/// <summary>
/// Phase 7 Task 7.5 — <c>evidence --type</c> includes a per-service
/// "Auto-dependencies" block with columns Type | Source Tag | Suppress With,
/// and the cross-command flags suppress / isolate it.
/// </summary>
[Collection("CLI Execution")]
public sealed class AutoDepsEvidenceCommandTests
{
    private static string AutoDepsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "AutoDepsProject", "AutoDepsProject.csproj");

    [Fact]
    public async Task Evidence_emits_auto_dependencies_block_with_suppress_hint()
    {
        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", AutoDepsProjectPath,
            "--type", "AutoDepsProject.Services.OrderService");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Auto-dependencies:");
        result.Stdout.Should().Contain("Type | Source Tag | Suppress With");
        result.Stdout.Should().Contain("auto-builtin:ILogger");
        result.Stdout.Should().Contain("[NoAutoDepOpen(typeof(ILogger<>))]");
    }

    [Fact]
    public async Task Evidence_with_hide_auto_deps_suppresses_block()
    {
        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", AutoDepsProjectPath,
            "--type", "AutoDepsProject.Services.OrderService",
            "--hide-auto-deps");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().NotContain("Auto-dependencies:");
    }

    [Fact]
    public async Task Evidence_with_only_auto_deps_hides_explicit_deps()
    {
        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", AutoDepsProjectPath,
            "--type", "AutoDepsProject.Services.OrderService",
            "--only-auto-deps");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Auto-dependencies:");
        result.Stdout.Should().Contain("Dependencies:\n    (none)");
    }

    [Fact]
    public async Task Evidence_json_includes_autoDeps_array()
    {
        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", AutoDepsProjectPath,
            "--type", "AutoDepsProject.Services.OrderService",
            "--json");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("\"autoDeps\"");
        result.Stdout.Should().Contain("auto-builtin:ILogger");
        result.Stdout.Should().Contain("\"suppress\"");
    }
}
