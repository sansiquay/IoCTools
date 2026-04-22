namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using IoCTools.Tools.Cli.CommandLine;

using Xunit;

/// <summary>
/// Parser-level coverage for the cross-command <c>--hide-auto-deps</c> /
/// <c>--only-auto-deps</c> flags on graph, why, explain, and evidence.
/// These tests exercise the CommandLineParser directly (for per-subcommand
/// parse shape) and the CLI entrypoint (for mutual-exclusion error surfacing).
/// </summary>
[Collection("CLI Execution")]
public sealed class CrossCommandFlagTests
{
    private static string FieldsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "FieldsProject", "FieldsProject.csproj");

    [Fact]
    public void ParseGraph_with_hide_auto_deps_sets_flag()
    {
        var parse = CommandLineParser.ParseGraph(new[]
        {
            "--project", "foo.csproj",
            "--hide-auto-deps"
        });

        parse.Success.Should().BeTrue();
        parse.Value.Should().NotBeNull();
        parse.Value!.AutoDepsFlags.HideAutoDeps.Should().BeTrue();
        parse.Value.AutoDepsFlags.OnlyAutoDeps.Should().BeFalse();
    }

    [Fact]
    public void ParseGraph_with_only_auto_deps_sets_flag()
    {
        var parse = CommandLineParser.ParseGraph(new[]
        {
            "--project", "foo.csproj",
            "--only-auto-deps"
        });

        parse.Success.Should().BeTrue();
        parse.Value!.AutoDepsFlags.OnlyAutoDeps.Should().BeTrue();
    }

    [Fact]
    public void ParseGraph_without_flags_defaults_to_empty()
    {
        var parse = CommandLineParser.ParseGraph(new[]
        {
            "--project", "foo.csproj"
        });

        parse.Success.Should().BeTrue();
        parse.Value!.AutoDepsFlags.HideAutoDeps.Should().BeFalse();
        parse.Value.AutoDepsFlags.OnlyAutoDeps.Should().BeFalse();
    }

    [Fact]
    public void ParseWhy_with_hide_auto_deps_sets_flag()
    {
        var parse = CommandLineParser.ParseWhy(new[]
        {
            "--project", "foo.csproj",
            "--type", "Svc",
            "--dependency", "IFoo",
            "--hide-auto-deps"
        });

        parse.Success.Should().BeTrue();
        parse.Value!.AutoDepsFlags.HideAutoDeps.Should().BeTrue();
    }

    [Fact]
    public void ParseWhy_with_both_flags_returns_parser_error()
    {
        var parse = CommandLineParser.ParseWhy(new[]
        {
            "--project", "foo.csproj",
            "--type", "Svc",
            "--dependency", "IFoo",
            "--hide-auto-deps",
            "--only-auto-deps"
        });

        parse.Success.Should().BeFalse();
        parse.Error.Should().Contain("mutually exclusive");
    }

    [Fact]
    public void ParseExplain_with_only_auto_deps_sets_flag()
    {
        var parse = CommandLineParser.ParseExplain(new[]
        {
            "--project", "foo.csproj",
            "--type", "Svc",
            "--only-auto-deps"
        });

        parse.Success.Should().BeTrue();
        parse.Value!.AutoDepsFlags.OnlyAutoDeps.Should().BeTrue();
    }

    [Fact]
    public void ParseExplain_with_both_flags_returns_parser_error()
    {
        var parse = CommandLineParser.ParseExplain(new[]
        {
            "--project", "foo.csproj",
            "--type", "Svc",
            "--hide-auto-deps",
            "--only-auto-deps"
        });

        parse.Success.Should().BeFalse();
        parse.Error.Should().Contain("mutually exclusive");
    }

    [Fact]
    public void ParseEvidence_with_hide_auto_deps_sets_flag()
    {
        var parse = CommandLineParser.ParseEvidence(new[]
        {
            "--project", "foo.csproj",
            "--hide-auto-deps"
        });

        parse.Success.Should().BeTrue();
        parse.Value!.AutoDepsFlags.HideAutoDeps.Should().BeTrue();
    }

    [Fact]
    public void ParseEvidence_with_both_flags_returns_parser_error()
    {
        var parse = CommandLineParser.ParseEvidence(new[]
        {
            "--project", "foo.csproj",
            "--hide-auto-deps",
            "--only-auto-deps"
        });

        parse.Success.Should().BeFalse();
        parse.Error.Should().Contain("mutually exclusive");
    }

    [Fact]
    public async Task Cli_graph_with_both_flags_exits_nonzero_with_mutually_exclusive_error()
    {
        var result = await CliTestHost.RunAsync(
            "graph",
            "--project", FieldsProjectPath,
            "--hide-auto-deps",
            "--only-auto-deps");

        result.ExitCode.Should().NotBe(0);
        (result.Stdout + result.Stderr).Should().Contain("mutually exclusive");
    }

    [Fact]
    public async Task Cli_graph_with_hide_auto_deps_runs_successfully()
    {
        var result = await CliTestHost.RunAsync(
            "graph",
            "--project", FieldsProjectPath,
            "--hide-auto-deps",
            "--format", "json");

        // Parser accepts the flag; behavior is unchanged in this task
        // (filtering wired in subsequent tasks). Must not regress existing runs.
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task Cli_why_with_hide_auto_deps_runs_successfully()
    {
        var result = await CliTestHost.RunAsync(
            "why",
            "--project", FieldsProjectPath,
            "--type", "FieldsProject.Services.TelemetryReporter",
            "--dependency", "ILogger",
            "--hide-auto-deps");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task Cli_explain_with_only_auto_deps_runs_successfully()
    {
        var result = await CliTestHost.RunAsync(
            "explain",
            "--project", FieldsProjectPath,
            "--type", "FieldsProject.Services.TelemetryReporter",
            "--only-auto-deps");

        result.ExitCode.Should().Be(0);
    }
}
