namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using Xunit;

/// <summary>
/// Phase 7 Task 7.7 — <c>suppress</c> must surface the new 1.6 IOC095-IOC105 auto-deps
/// diagnostics in its output, both via the default severity filter and via explicit --codes.
/// </summary>
[Collection("CLI Execution")]
public sealed class AutoDepsSuppressCommandTests
{
    private static string AutoDepsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "AutoDepsProject", "AutoDepsProject.csproj");

    [Fact]
    public async Task Suppress_default_severity_includes_new_autoDeps_codes()
    {
        var result = await CliTestHost.RunAsync(
            "suppress",
            "--project", AutoDepsProjectPath);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("IoCTools.AutoDeps");
        result.Stdout.Should().Contain("dotnet_diagnostic.IOC096");
        result.Stdout.Should().Contain("dotnet_diagnostic.IOC097");
        result.Stdout.Should().Contain("dotnet_diagnostic.IOC098");
        result.Stdout.Should().Contain("dotnet_diagnostic.IOC099");
        result.Stdout.Should().Contain("dotnet_diagnostic.IOC105");
    }

    [Fact]
    public async Task Suppress_default_severity_includes_inject_deprecation_warning()
    {
        var result = await CliTestHost.RunAsync(
            "suppress",
            "--project", AutoDepsProjectPath);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("IoCTools.Usage");
        result.Stdout.Should().Contain("dotnet_diagnostic.IOC095");
        result.Stdout.Should().Contain("[Inject] is deprecated");
    }

    [Fact]
    public async Task Suppress_with_error_codes_annotates_explicit_override()
    {
        var result = await CliTestHost.RunAsync(
            "suppress",
            "--project", AutoDepsProjectPath,
            "--codes", "IOC100,IOC101,IOC102,IOC103,IOC104");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("dotnet_diagnostic.IOC100");
        result.Stdout.Should().Contain("dotnet_diagnostic.IOC104");
        // Error-severity codes selected via --codes must render the verify-this-is-intentional
        // risk note so suppressing an error-level rule is a deliberate action.
        result.Stdout.Should().Contain("suppressed explicitly");
    }
}
