namespace IoCTools.Tools.Cli.Tests;

using System.Text.Json;

using FluentAssertions;

using Infrastructure;

using IoCTools.Tools.Cli.CommandLine;

using Xunit;

[Collection("CLI Execution")]
public sealed class CliEvidenceCommandTests
{
    private static string FieldsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "FieldsProject", "FieldsProject.csproj");

    private static string RegistrationProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "RegistrationProject",
            "RegistrationProject.csproj");

    private static string OpenGenericProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "OpenGenericProject",
            "OpenGenericProject.csproj");

    private static string GeneratorStubDirectory =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "GeneratorStubs");

    [Fact]
    public void ParseEvidence_WithFullShape_BindsExpectedOptions()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), "evidence.appsettings.json");
        var baselineDirectory = Path.Combine(Path.GetTempPath(), "evidence-baseline");
        var outputDirectory = Path.Combine(Path.GetTempPath(), "evidence-output");

        var parse = CommandLineParser.ParseEvidence(new[]
        {
            "--project", FieldsProjectPath,
            "--type", "FieldsProject.Services.TelemetryReporter",
            "--settings", settingsPath,
            "--baseline", baselineDirectory,
            "--output", outputDirectory,
            "--json"
        });

        parse.Success.Should().BeTrue();
        parse.Value.Should().NotBeNull();
        parse.Value!.Common.ProjectPath.Should().Be(Path.GetFullPath(FieldsProjectPath));
        parse.Value.TypeName.Should().Be("FieldsProject.Services.TelemetryReporter");
        parse.Value.SettingsPath.Should().Be(Path.GetFullPath(settingsPath));
        parse.Value.BaselineDirectory.Should().Be(Path.GetFullPath(baselineDirectory));
        parse.Value.OutputDirectory.Should().Be(Path.GetFullPath(outputDirectory));
        parse.Value.Common.Json.Should().BeTrue();
    }

    [Fact]
    public void ParseEvidence_WithoutProject_Fails()
    {
        var parse = CommandLineParser.ParseEvidence(new[] { "--type", "FieldsProject.Services.TelemetryReporter" });

        parse.Success.Should().BeFalse();
        parse.Error.Should().Be("--project is required.");
    }

    [Fact]
    public async Task Help_Includes_Evidence_Command()
    {
        var result = await CliTestHost.RunAsync("help");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("dotnet ioc-tools evidence --project <csproj>");
    }

    [Fact]
    public async Task Evidence_JsonMode_Emits_Correlated_Payload_And_MigrationHints()
    {
        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", RegistrationProjectPath,
            "--json");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("\"project\"");
        result.Stdout.Should().Contain("\"services\"");
        result.Stdout.Should().Contain("\"diagnostics\"");
        result.Stdout.Should().Contain("\"configuration\"");
        result.Stdout.Should().Contain("\"artifacts\"");
        result.Stdout.Should().Contain("\"migrationHints\"");
        result.Stdout.Should().Contain("[DependsOn]");
        result.Stdout.Should().Contain("[DependsOnConfiguration]");
    }

    [Fact]
    public async Task Evidence_JsonMode_WithBaseline_Emits_ArtifactFingerprints_And_CompareDeltas()
    {
        var baselineDirectory = TestPaths.CreateTempDirectory();
        var outputDirectory = TestPaths.CreateTempDirectory();
        var stubPath = Path.Combine(GeneratorStubDirectory, "ServiceRegistrations_RegistrationProject.g.cs");
        var baselinePath = Path.Combine(baselineDirectory, Path.GetFileName(stubPath));
        var baselineSource = await File.ReadAllTextAsync(stubPath);
        await File.WriteAllTextAsync(
            baselinePath,
            baselineSource.Replace(
                "services.AddScoped<IRegistrationService, RegistrationService>();",
                "services.AddSingleton<IRegistrationService, RegistrationService>();",
                StringComparison.Ordinal));

        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", RegistrationProjectPath,
            "--baseline", baselineDirectory,
            "--output", outputDirectory,
            "--json");

        result.ExitCode.Should().Be(0);
        using var payload = JsonDocument.Parse(result.Stdout);

        var artifacts = payload.RootElement.GetProperty("artifacts");
        var generatedArtifacts = artifacts.GetProperty("generatedArtifacts");
        generatedArtifacts.GetArrayLength().Should().BeGreaterThan(0);

        var registrationArtifact = generatedArtifacts.EnumerateArray()
            .Single(artifact => artifact.GetProperty("artifactId").GetString() == "ServiceRegistrations_RegistrationProject.g.cs");

        registrationArtifact.GetProperty("fileName").GetString().Should().Be("ServiceRegistrations_RegistrationProject.g.cs");
        registrationArtifact.GetProperty("fingerprint").GetString().Should().MatchRegex("^[a-f0-9]{64}$");
        registrationArtifact.GetProperty("path").GetString().Should().NotBeNullOrWhiteSpace();

        var compare = artifacts.GetProperty("compare");
        var deltas = compare.GetProperty("deltas");
        deltas.GetArrayLength().Should().BeGreaterThan(0);

        var registrationDelta = deltas.EnumerateArray()
            .Single(delta => delta.GetProperty("artifactId").GetString() == "ServiceRegistrations_RegistrationProject.g.cs");

        registrationDelta.GetProperty("status").GetString().Should().Be("changed");
        registrationDelta.GetProperty("baselineFingerprint").GetString().Should().MatchRegex("^[a-f0-9]{64}$");
        registrationDelta.GetProperty("currentFingerprint").GetString().Should().MatchRegex("^[a-f0-9]{64}$");
        registrationDelta.GetProperty("baselinePath").GetString().Should().Be(baselinePath);
        registrationDelta.GetProperty("currentPath").GetString().Should().Be(Path.Combine(outputDirectory, "ServiceRegistrations_RegistrationProject.g.cs"));
    }

    [Fact]
    public async Task Evidence_TextMode_WithType_Prints_Compact_Review_Packet()
    {
        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", FieldsProjectPath,
            "--type", "FieldsProject.Services.TelemetryReporter");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Project");
        result.Stdout.Should().Contain("Services");
        result.Stdout.Should().Contain("Type Evidence");
        result.Stdout.Should().Contain("Diagnostics");
        result.Stdout.Should().Contain("Configuration");
    }

    [Fact]
    public async Task Evidence_JsonMode_Includes_OpenGeneric_Registration()
    {
        var stubDirectory = TestPaths.CreateTempDirectory();
        var stubPath = Path.Combine(stubDirectory, "ServiceRegistrations_OpenGenericProject.g.cs");
        await File.WriteAllTextAsync(
            stubPath,
            """
            #nullable enable
            namespace OpenGenericProject.Extensions.Generated;

            using Microsoft.Extensions.DependencyInjection;
            using OpenGenericProject.Services;

            public static partial class GeneratedServiceCollectionExtensions
            {
                public static IServiceCollection AddOpenGenericProjectRegisteredServices(this IServiceCollection services)
                {
                     services.AddScoped(typeof(global::OpenGenericProject.Services.OpenGenericRepository<>));
                     services.AddScoped(typeof(global::OpenGenericProject.Services.IOpenGenericRepository<>), provider => provider.GetRequiredService(typeof(global::OpenGenericProject.Services.OpenGenericRepository<>)));
                     services.AddScoped(typeof(global::OpenGenericProject.Services.IOpenGenericLookup<>), provider => provider.GetRequiredService(typeof(global::OpenGenericProject.Services.OpenGenericRepository<>)));
                     AddOpenGenericProjectFluentValidationServices(services);
                     return services;
                }

                static partial void AddOpenGenericProjectFluentValidationServices(IServiceCollection services);
            }
            """);

        using var scope = new EnvironmentVariableScope("IOC_TOOLS_GENERATOR_STUB", stubDirectory);
        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", OpenGenericProjectPath,
            "--json");

        result.ExitCode.Should().Be(0);

        using var payload = JsonDocument.Parse(result.Stdout);
        var registrations = payload.RootElement
            .GetProperty("services")
            .GetProperty("registrations")
            .EnumerateArray()
            .ToArray();

        var openGenericRegistration = registrations.Single(registration =>
            registration.GetProperty("serviceType").GetString()!.Contains("IOpenGenericLookup<>",
                StringComparison.Ordinal));

        openGenericRegistration.GetProperty("implementationType").GetString()
            .Should().Contain("OpenGenericRepository<>");
        openGenericRegistration.GetProperty("lifetime").GetString().Should().Be("Scoped");
        openGenericRegistration.GetProperty("usesFactory").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Suppress_JsonMode_Emits_Metadata_Rich_Rules()
    {
        var result = await CliTestHost.RunAsync(
            "suppress",
            "--project", FieldsProjectPath,
            "--codes", "IOC035,IOC092",
            "--json");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("\"selectionReason\"");
        result.Stdout.Should().Contain("\"isErrorByDefault\"");
        result.Stdout.Should().Contain("\"riskNote\"");
        result.Stdout.Should().Contain("\"editorconfig\"");
        result.Stdout.Should().Contain("\"id\": \"IOC035\"");
        result.Stdout.Should().Contain("\"id\": \"IOC092\"");
    }
}
