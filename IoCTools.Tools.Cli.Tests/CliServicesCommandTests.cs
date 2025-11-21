using FluentAssertions;
using IoCTools.Tools.Cli.Tests.Infrastructure;
using Xunit;

namespace IoCTools.Tools.Cli.Tests;

[Collection("CLI Execution")]
public sealed class CliServicesCommandTests
{
    private static string RegistrationProject =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "RegistrationProject", "RegistrationProject.csproj");

    private static string EmptyProject =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "EmptyProject", "EmptyProject.csproj");

    [Fact]
    public async Task ServicesCommand_SummarizesRegistrations_AndConfiguration()
    {
        var tempDir = TestPaths.CreateTempDirectory();
        var stubDir = TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "GeneratorStubs");
        using var scope = new EnvironmentVariableScope("IOC_TOOLS_GENERATOR_STUB", stubDir);

        var result = await CliTestHost.RunAsync(
            "services",
            "--project", RegistrationProject,
            "--output", tempDir);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Service Registrations:");
        result.Stdout.Should().Contain("[Singleton] IAnalyticsProcessor => AnalyticsProcessor");
        result.Stdout.Should().Contain("Configuration Bindings:");
        result.Stdout.Should().Contain("Configure<NotificationOptions>()");
        result.Stdout.Should().Contain("string.Equals(configuration[\"Features:EnableBackground\"], \"true\"");
    }

    [Fact]
    public async Task ServicesCommand_NoGeneratedExtension_PrintsMessage()
    {
        var result = await CliTestHost.RunAsync(
            "services",
            "--project", EmptyProject);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("No generated registration extension was produced for this project.");
    }

    [Fact]
    public async Task ServicesPathCommand_PrintsExtensionPath()
    {
        var tempDir = TestPaths.CreateTempDirectory();
        var stubDir = TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "GeneratorStubs");
        using var scope = new EnvironmentVariableScope("IOC_TOOLS_GENERATOR_STUB", stubDir);

        var result = await CliTestHost.RunAsync(
            "services-path",
            "--project", RegistrationProject,
            "--output", tempDir);

        result.ExitCode.Should().Be(0);
        var path = result.FirstOutputLine;
        path.Should().EndWith("ServiceRegistrations_RegistrationProject.g.cs");
        File.Exists(path).Should().BeTrue();
        var contents = await File.ReadAllTextAsync(path);
        contents.Should().Contain("public static class GeneratedServiceCollectionExtensions");
    }
}
