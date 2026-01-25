namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using Xunit;

[Collection("CLI Execution")]
public sealed class MultiTargetingTests
{
    private static string MultiTargetProject =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "MultiTargetProject",
            "MultiTargetProject.csproj");

    [Fact]
    public async Task MultiTargetProject_WithoutFramework_ThrowsHelpfulError()
    {
        var result = await CliTestHost.RunAsync(
            "services",
            "--project", MultiTargetProject);

        result.ExitCode.Should().Be(1);
        result.Stderr.Should().Contain("targets multiple frameworks");
        result.Stderr.Should().Contain("net8.0");
        result.Stderr.Should().Contain("net9.0");
        result.Stderr.Should().Contain("--framework");
    }

    [Fact]
    public async Task MultiTargetProject_WithFramework_Succeeds()
    {
        var result = await CliTestHost.RunAsync(
            "services",
            "--project", MultiTargetProject,
            "--framework", "net8.0");

        result.ExitCode.Should().Be(0);
        result.Stderr.Should().BeEmpty();
    }

    [Fact]
    public async Task MultiTargetProject_WithOtherFramework_Succeeds()
    {
        var result = await CliTestHost.RunAsync(
            "services",
            "--project", MultiTargetProject,
            "--framework", "net9.0");

        result.ExitCode.Should().Be(0);
        result.Stderr.Should().BeEmpty();
    }
}
