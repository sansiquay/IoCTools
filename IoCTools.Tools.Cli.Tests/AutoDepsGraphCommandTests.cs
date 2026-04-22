namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using Xunit;

/// <summary>
/// Phase 7 Task 7.3 — <c>graph</c> emits source markers per edge, JSON adds an
/// <c>autoDeps</c> block alongside the registration list, and <c>--hide-auto-deps</c> /
/// <c>--only-auto-deps</c> filter the output accordingly.
/// </summary>
[Collection("CLI Execution")]
public sealed class AutoDepsGraphCommandTests
{
    private static string AutoDepsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "AutoDepsProject", "AutoDepsProject.csproj");

    [Fact]
    public async Task Graph_json_includes_source_field_on_registrations()
    {
        var result = await CliTestHost.RunAsync(
            "graph",
            "--project", AutoDepsProjectPath,
            "--format", "json");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("\"source\": \"explicit\"");
    }

    [Fact]
    public async Task Graph_json_emits_autoDeps_block()
    {
        var result = await CliTestHost.RunAsync(
            "graph",
            "--project", AutoDepsProjectPath,
            "--format", "json");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("\"autoDeps\"");
        result.Stdout.Should().Contain("auto-builtin:ILogger");
    }

    [Fact]
    public async Task Graph_hide_auto_deps_removes_autoDeps_entries()
    {
        var result = await CliTestHost.RunAsync(
            "graph",
            "--project", AutoDepsProjectPath,
            "--format", "json",
            "--hide-auto-deps");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().NotContain("auto-builtin");
    }

    [Fact]
    public async Task Graph_only_auto_deps_removes_explicit_registrations()
    {
        var result = await CliTestHost.RunAsync(
            "graph",
            "--project", AutoDepsProjectPath,
            "--format", "json",
            "--only-auto-deps");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("auto-builtin:ILogger");
        // With --only-auto-deps the registrations array is empty; the autoDeps array carries the
        // provenance-only payload the flag is designed to surface.
        result.Stdout.Should().Contain("\"registrations\": []");
    }

    [Fact]
    public async Task Graph_plantuml_emits_dotted_edge_for_auto_deps()
    {
        var result = await CliTestHost.RunAsync(
            "graph",
            "--project", AutoDepsProjectPath);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("..>");
        result.Stdout.Should().Contain("auto-builtin:ILogger");
        result.Stdout.Should().Contain("Legend:");
    }
}
