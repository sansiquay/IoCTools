namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using Xunit;

/// <summary>
/// Phase 7 Task 7.6 — <c>doctor</c> emits a preflight section ahead of the regular
/// generator-diagnostic dump covering dead profiles, broken auto-dep types, and stale
/// <c>AutoDepsApply</c> rules.
/// </summary>
[Collection("CLI Execution")]
public sealed class AutoDepsDoctorCommandTests
{
    private static string DoctorProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "DoctorAutoDepsProject", "DoctorAutoDepsProject.csproj");

    [Fact]
    public async Task Doctor_reports_dead_profile_in_preflight()
    {
        var result = await CliTestHost.RunAsync(
            "doctor",
            "--project", DoctorProjectPath);

        result.Stdout.Should().Contain("Preflight:");
        result.Stdout.Should().Contain("OrphanProfile");
        result.Stdout.Should().Contain("declared but never attached");
    }

    [Fact]
    public async Task Doctor_json_output_has_preflight_array()
    {
        var result = await CliTestHost.RunAsync(
            "doctor",
            "--project", DoctorProjectPath,
            "--json");

        result.Stdout.Should().Contain("\"preflight\"");
        result.Stdout.Should().Contain("AutoDeps.DeadProfile");
    }
}
