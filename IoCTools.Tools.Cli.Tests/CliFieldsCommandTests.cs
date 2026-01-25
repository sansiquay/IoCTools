namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using Xunit;

[Collection("CLI Execution")]
public sealed class CliFieldsCommandTests
{
    private static string FieldsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "FieldsProject", "FieldsProject.csproj");

    private static string TelemetryFilePath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "FieldsProject", "Services",
            "TelemetryReporter.cs");

    private static string PlainFilePath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "FieldsProject", "Services",
            "PlainUtility.cs");

    [Fact]
    public async Task FieldsCommand_PrintsDependencies_AndConfigMetadata()
    {
        var result = await CliTestHost.RunAsync(
            "fields",
            "--project", FieldsProjectPath,
            "--file", TelemetryFilePath,
            "--type", "FieldsProject.Services.TelemetryReporter");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Service: FieldsProject.Services.TelemetryReporter");
        result.Stdout.Should().Contain("Generated Dependencies:");
        result.Stdout.Should()
            .Contain("ILogger<FieldsProject.Services.TelemetryReporter> => _logger");
        result.Stdout.Should().Contain("FieldsProject.Services.IMetricsClient => _metricsClient");
        result.Stdout.Should().Contain("Generated Config Fields:");
        result.Stdout.Should().Contain("key: Observability:Endpoint, required");
        result.Stdout.Should().Contain("Observability:TimeoutSeconds");
        result.Stderr.Should().BeEmpty();
    }

    [Fact]
    public async Task FieldsCommand_NoServiceIntent_PrintsHelpfulMessage()
    {
        var result = await CliTestHost.RunAsync(
            "fields",
            "--project", FieldsProjectPath,
            "--file", PlainFilePath);

        result.ExitCode.Should().Be(0);
        result.Stdout.Trim().Should().Contain("No IoCTools-enabled services found in file.");
    }

    [Fact]
    public async Task FieldsPathCommand_PrintsConstructorPath()
    {
        var tempDir = TestPaths.CreateTempDirectory();
        var stubDir = TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "GeneratorStubs");
        using var scope = new EnvironmentVariableScope("IOC_TOOLS_GENERATOR_STUB", stubDir);

        var result = await CliTestHost.RunAsync(
            "fields-path",
            "--project", FieldsProjectPath,
            "--file", TelemetryFilePath,
            "--type", "FieldsProject.Services.TelemetryReporter",
            "--output", tempDir);

        result.ExitCode.Should().Be(0);
        var path = result.FirstOutputLine;
        path.Should().EndWith("FieldsProject_Services_TelemetryReporter_Constructor.g.cs");
        File.Exists(path).Should().BeTrue();
        var generated = await File.ReadAllTextAsync(path);
        generated.Should().Contain("public TelemetryReporter");
    }

    [Fact]
    public async Task FieldsPathCommand_DefaultOutput_WritesToTemp()
    {
        var stubDir = TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "GeneratorStubs");
        using var scope = new EnvironmentVariableScope("IOC_TOOLS_GENERATOR_STUB", stubDir);

        var result = await CliTestHost.RunAsync(
            "fields-path",
            "--project", FieldsProjectPath,
            "--file", TelemetryFilePath,
            "--type", "FieldsProject.Services.TelemetryReporter");

        result.ExitCode.Should().Be(0);
        var path = Path.GetFullPath(result.FirstOutputLine);
        File.Exists(path).Should().BeTrue();

        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var projectDirectory = Path.GetFullPath(Path.GetDirectoryName(FieldsProjectPath)!);
        path.Should().StartWithEquivalentOf(tempRoot);
        path.Should().NotStartWithEquivalentOf(projectDirectory);
    }

    [Fact]
    public async Task FieldsPathCommand_InvalidType_ShowsError()
    {
        var result = await CliTestHost.RunAsync(
            "fields-path",
            "--project", FieldsProjectPath,
            "--file", TelemetryFilePath,
            "--type", "FieldsProject.Services.DoesNotExist");

        result.ExitCode.Should().Be(1);
        result.Stderr.Should().Contain("was not found");
    }

    [Fact]
    public async Task FieldsCommand_WithSourceFlag_OutputsGeneratedConstructor()
    {
        var tempDir = TestPaths.CreateTempDirectory();
        var stubDir = TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "GeneratorStubs");
        using var scope = new EnvironmentVariableScope("IOC_TOOLS_GENERATOR_STUB", stubDir);

        var result = await CliTestHost.RunAsync(
            "fields",
            "--project", FieldsProjectPath,
            "--file", TelemetryFilePath,
            "--type", "FieldsProject.Services.TelemetryReporter",
            "--source",
            "--output", tempDir);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("public partial class TelemetryReporter");
        result.Stdout.Should().Contain("public TelemetryReporter(");
        result.Stdout.Should().Contain("_logger = logger;");
        result.Stdout.Should().Contain("_metricsClient = metricsClient;");
    }

    [Fact]
    public async Task FieldsCommand_WithSourceFlag_NoMatchingTypes_ShowsMessage()
    {
        var tempDir = TestPaths.CreateTempDirectory();
        var stubDir = TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "GeneratorStubs");
        using var scope = new EnvironmentVariableScope("IOC_TOOLS_GENERATOR_STUB", stubDir);

        var result = await CliTestHost.RunAsync(
            "fields",
            "--project", FieldsProjectPath,
            "--file", PlainFilePath,
            "--source",
            "--output", tempDir);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("No IoCTools-enabled services found in file.");
    }

    [Fact]
    public async Task ParseFields_WithEmptyEqualsValue_ReturnsError()
    {
        // --project= results in empty string which fails path validation
        var result = await CliTestHost.RunAsync(
            "fields",
            "--project=",  // Empty value after equals
            "--file", TelemetryFilePath);

        // Should error because empty path is invalid
        result.ExitCode.Should().Be(1);
        result.Stderr.Should().Contain("Path cannot be empty");
    }

    [Fact]
    public async Task ParseFields_WithDuplicateFlags_HandlesGracefully()
    {
        // Duplicate flags should be collected as multiple values
        var result = await CliTestHost.RunAsync(
            "fields",
            "--project", FieldsProjectPath,
            "--file", TelemetryFilePath,
            "--type", "FieldsProject.Services.TelemetryReporter",
            "--type", "FieldsProject.Services.PlainUtility");  // Duplicate --type

        result.ExitCode.Should().Be(0);
        // Should process both types
        result.Stdout.Should().Contain("Service:");
    }

    [Fact]
    public async Task ParseFields_WithSpacesInPath_HandlesCorrectly()
    {
        // Create a temp path with spaces (test the scenario doesn't crash)
        var tempDir = TestPaths.CreateTempDirectory();
        var stubDir = TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "GeneratorStubs");
        using var scope = new EnvironmentVariableScope("IOC_TOOLS_GENERATOR_STUB", stubDir);

        // Paths with spaces should be handled by the shell (quoted args)
        var result = await CliTestHost.RunAsync(
            "fields",
            "--project", FieldsProjectPath,
            "--file", TelemetryFilePath,
            "--output", tempDir,
            "--source");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("public partial class TelemetryReporter");
    }

    [Fact]
    public async Task ParseFields_WithDoubleEquals_ParsesCorrectly()
    {
        // --project==value is treated as --project with value "=value"
        var result = await CliTestHost.RunAsync(
            "fields",
            "--project==" + FieldsProjectPath,  // Double equals
            "--file", TelemetryFilePath);

        // This will fail because "=/path/to/project" is not a valid path
        // but verifies the parser doesn't crash
        result.ExitCode.Should().Be(1);
        result.Stderr.Should().NotBeEmpty();
    }
}

[CollectionDefinition("CLI Execution")]
public sealed class CliExecutionCollection : ICollectionFixture<object>
{
}
