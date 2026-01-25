namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using Xunit;

[Collection("CLI Execution")]
public sealed class CliConfigAuditCommandTests
{
    private static string FieldsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "FieldsProject", "FieldsProject.csproj");

    [Fact]
    public async Task ConfigAuditCommand_WithNoSettings_PrintsAllRequiredKeys()
    {
        var result = await CliTestHost.RunAsync(
            "config-audit",
            "--project", FieldsProjectPath);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Configuration audit:");
        result.Stdout.Should().Contain("Required bindings: 2");
        result.Stdout.Should().Contain("Observability:Endpoint");
        result.Stdout.Should().Contain("Observability:TimeoutSeconds");
        result.Stderr.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfigAuditCommand_WithCompleteSettings_AllKeysPresent()
    {
        var tempSettings = Path.Combine(Path.GetTempPath(), $"appsettings.{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(tempSettings, """
            {
              "Observability": {
                "Endpoint": "https://metrics.example.com",
                "TimeoutSeconds": 30
              }
            }
            """);

            var result = await CliTestHost.RunAsync(
                "config-audit",
                "--project", FieldsProjectPath,
                "--settings", tempSettings);

            result.ExitCode.Should().Be(0);
            result.Stdout.Should().Contain("Configuration audit:");
            result.Stdout.Should().Contain("All keys present in provided settings.");
            result.Stdout.Should().NotContain("Missing keys:");
        }
        finally
        {
            if (File.Exists(tempSettings))
                File.Delete(tempSettings);
        }
    }

    [Fact]
    public async Task ConfigAuditCommand_WithPartialSettings_DetectsMissingKeys()
    {
        var tempSettings = Path.Combine(Path.GetTempPath(), $"appsettings.{Guid.NewGuid()}.json");
        try
        {
            // Create settings file missing TimeoutSeconds
            File.WriteAllText(tempSettings, """
            {
              "Observability": {
                "Endpoint": "https://metrics.example.com"
              }
            }
            """);

            var result = await CliTestHost.RunAsync(
                "config-audit",
                "--project", FieldsProjectPath,
                "--settings", tempSettings);

            result.ExitCode.Should().Be(0);
            result.Stdout.Should().Contain("Configuration audit:");
            result.Stdout.Should().Contain("Required bindings: 2");
            result.Stdout.Should().Contain("Settings keys discovered: 1");
            result.Stdout.Should().Contain("Missing keys:");
            result.Stdout.Should().Contain("Observability:TimeoutSeconds");
        }
        finally
        {
            if (File.Exists(tempSettings))
                File.Delete(tempSettings);
        }
    }

    [Fact]
    public async Task ConfigAuditCommand_WithNoConfigBindings_PrintsHelpfulMessage()
    {
        var emptyProjectPath =
            TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "EmptyProject", "EmptyProject.csproj");

        var result = await CliTestHost.RunAsync(
            "config-audit",
            "--project", emptyProjectPath);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("No configuration bindings found in project.");
        // Build warnings may appear in stderr, only check for actual errors
        result.Stderr.Should().NotContain("error");
    }

    [Fact]
    public async Task ConfigAuditCommand_WithInvalidSettingsPath_PrintsErrorAndContinues()
    {
        var invalidPath = "/nonexistent/path/appsettings.json";

        var result = await CliTestHost.RunAsync(
            "config-audit",
            "--project", FieldsProjectPath,
            "--settings", invalidPath);

        // Command should still succeed but warn about settings file
        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Configuration audit:");
        // When settings file is missing, all keys should be reported as missing
        result.Stdout.Should().Contain("Missing keys:");
    }

    [Fact]
    public async Task ConfigAuditCommand_WithNestedSettingsFormat_DetectsAllKeys()
    {
        var tempSettings = Path.Combine(Path.GetTempPath(), $"appsettings.{Guid.NewGuid()}.json");
        try
        {
            // Create settings with nested structure matching .NET configuration flattening
            File.WriteAllText(tempSettings, """
            {
              "Observability": {
                "Endpoint": "https://metrics.example.com",
                "TimeoutSeconds": 30
              },
              "OtherSection": {
                "Value": "should be ignored"
              }
            }
            """);

            var result = await CliTestHost.RunAsync(
                "config-audit",
                "--project", FieldsProjectPath,
                "--settings", tempSettings);

            result.ExitCode.Should().Be(0);
            result.Stdout.Should().Contain("Settings keys discovered: 3");
            result.Stdout.Should().Contain("All keys present in provided settings.");
        }
        finally
        {
            if (File.Exists(tempSettings))
                File.Delete(tempSettings);
        }
    }
}
