namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using Xunit;

[Collection("CLI Execution")]
public sealed class CliExtendedCommandTests
{
    private static string FieldsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "FieldsProject", "FieldsProject.csproj");

    private static string RegistrationProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "RegistrationProject",
            "RegistrationProject.csproj");

    private static string TelemetryFilePath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "FieldsProject", "Services",
            "TelemetryReporter.cs");

    [Fact]
    public async Task Explain_Prints_Single_Service_Details()
    {
        var result = await CliTestHost.RunAsync(
            "explain",
            "--project", FieldsProjectPath,
            "--type", "FieldsProject.Services.TelemetryReporter");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Service: FieldsProject.Services.TelemetryReporter");
        result.Stdout.Should().Contain("Dependencies:");
        result.Stdout.Should().Contain("ILogger<FieldsProject.Services.TelemetryReporter>");
        result.Stdout.Should().Contain("Configuration:");
    }

    [Fact]
    public async Task Graph_Json_Format_Emits_Records()
    {
        var result = await CliTestHost.RunAsync(
            "graph",
            "--project", RegistrationProjectPath,
            "--format", "json");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("ServiceType");
        result.Stdout.Should().Contain("ImplementationType");
    }

    [Fact]
    public async Task Why_Finds_Dependency()
    {
        var result = await CliTestHost.RunAsync(
            "why",
            "--project", FieldsProjectPath,
            "--type", "FieldsProject.Services.TelemetryReporter",
            "--dependency", "ILogger");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Field: _logger");
        result.Stdout.Should().Contain("ILogger<FieldsProject.Services.TelemetryReporter>");
    }

    [Fact]
    public async Task Doctor_NoDiagnostics_Succeeds()
    {
        var emptyProject = TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "EmptyProject",
            "EmptyProject.csproj");

        var result = await CliTestHost.RunAsync(
            "doctor",
            "--project", emptyProject,
            "--fixable-only");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("No diagnostics");
    }

    [Fact]
    public async Task Compare_Writes_Snapshot_And_Reports_Changes()
    {
        var tempDir = TestPaths.CreateTempDirectory();
        var baseline = TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "GeneratorStubs");

        var result = await CliTestHost.RunAsync(
            "compare",
            "--project", RegistrationProjectPath,
            "--output", tempDir,
            "--baseline", baseline);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Snapshot written");
        result.Stdout.Should().Contain("Comparing baseline");
    }

    [Fact]
    public async Task Profile_Prints_Timing()
    {
        var result = await CliTestHost.RunAsync(
            "profile",
            "--project", FieldsProjectPath,
            "--type", "FieldsProject.Services.TelemetryReporter");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Generator warm");
    }

    [Fact]
    public async Task ConfigAudit_WithMissingSettings_ListsMissingKeys()
    {
        var result = await CliTestHost.RunAsync(
            "config-audit",
            "--project", FieldsProjectPath);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Configuration audit");
        result.Stdout.Should().Contain("Missing keys");
    }
}
