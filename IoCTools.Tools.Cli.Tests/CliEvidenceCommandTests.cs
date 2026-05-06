namespace IoCTools.Tools.Cli.Tests;

using System.Linq;
using System.Text.Json;

using FluentAssertions;

using Infrastructure;

using IoCTools.Tools.Cli.CommandLine;

using Xunit;
using Xunit.Sdk;

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

    private static string FixtureEvidenceProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "FixtureEvidence.TestsProject",
            "FixtureEvidence.TestsProject.csproj");

    private static string FixtureEvidenceProductionProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "FixtureEvidence.ProductionProject",
            "FixtureEvidence.ProductionProject.csproj");

    private static async Task<string> CreateFixtureEvidenceExtraManualMockTestAsync()
    {
        var testProjectDirectory = Path.GetDirectoryName(FixtureEvidenceProjectPath)!;
        var sourcePath = Path.Combine(testProjectDirectory, $"OrderServiceExtraManualMockTests_{Guid.NewGuid():N}.cs");

        await File.WriteAllTextAsync(
            sourcePath,
            """
using FixtureEvidence.TestsProject.Services;
using IoCTools.Testing.Annotations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

[Cover<OrderService>]
public partial class OrderServiceExtraManualMockTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<IPricingEngine> _pricingEngineMock = new();
    private readonly Mock<ILogger<OrderService>> _loggerMock = new();
    private readonly Mock<IDisposable> _extraMock = new();

    [Fact]
    public void Smoke()
    {
        Assert.NotNull(_extraMock);
    }
}
""");

        return sourcePath;
    }

    private static async Task<string> CreateFixtureEvidenceExplicitConstructionTestAsync()
    {
        var testProjectDirectory = Path.GetDirectoryName(FixtureEvidenceProjectPath)!;
        var sourcePath = Path.Combine(testProjectDirectory, $"ExplicitConstructionServiceTests_{Guid.NewGuid():N}.cs");

        await File.WriteAllTextAsync(
            sourcePath,
            """
using FixtureEvidence.ProductionProject.Services;
using Moq;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

public class ExplicitConstructionServiceTests
{
    private readonly Mock<IProdRepository> _repository = new();
    private readonly Mock<IProdGateway> _gateway = new();

    private ProductionPreferenceService CreateSut() => new(
        _repository.Object,
        _gateway.Object);

    [Fact]
    public void Smoke()
    {
        Assert.NotNull(CreateSut());
    }
}
""");

        return sourcePath;
    }

    private static async Task<string> CreateFixtureEvidenceCommandHandlerConstructionTestAsync()
    {
        var testProjectDirectory = Path.GetDirectoryName(FixtureEvidenceProjectPath)!;
        var sourcePath = Path.Combine(testProjectDirectory, $"ApplyThingCommandHandlerTests_{Guid.NewGuid():N}.cs");

        await File.WriteAllTextAsync(
            sourcePath,
            """
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

public interface IThingWriter
{
}

public sealed class ApplyThingCommand
{
    public ApplyThingCommand(Guid id)
    {
    }
}

public sealed class ApplyThingCommandHandler
{
    public ApplyThingCommandHandler(IThingWriter writer, ILogger<ApplyThingCommandHandler> logger)
    {
    }
}

public class ApplyThingCommandHandlerTests
{
    private readonly Mock<IThingWriter> _writer = new();

    private ApplyThingCommandHandler CreateSut() => new(
        _writer.Object,
        NullLogger<ApplyThingCommandHandler>.Instance);

    [Fact]
    public void Smoke()
    {
        var command = new ApplyThingCommand(Guid.NewGuid());
        Assert.NotNull(command);
        Assert.NotNull(CreateSut());
    }
}
""");

        return sourcePath;
    }

    private static async Task<string> CreateFixtureEvidenceClockBackedTestAsync()
    {
        var testProjectDirectory = Path.GetDirectoryName(FixtureEvidenceProjectPath)!;
        var sourcePath = Path.Combine(testProjectDirectory, $"ClockBackedHandlerTests_{Guid.NewGuid():N}.cs");

        await File.WriteAllTextAsync(
            sourcePath,
            """
using System;
using Moq;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; } = DateTimeOffset.Parse("2026-05-05T00:00:00Z");
}

public interface IClockBackedRepository
{
}

public sealed class ClockBackedHandler
{
    public ClockBackedHandler(IClockBackedRepository repository, IClock clock)
    {
    }
}

public class ClockBackedHandlerTests
{
    private readonly Mock<IClockBackedRepository> _repository = new();
    private readonly TestClock _clock = new();

    private ClockBackedHandler CreateSut() => new(_repository.Object, _clock);

    [Fact]
    public void Smoke()
    {
        Assert.NotNull(CreateSut());
    }
}
""");

        return sourcePath;
    }

    private static async Task<string> CreateFixtureEvidenceTestSupportConstructionAsync()
    {
        var testProjectDirectory = Path.GetDirectoryName(FixtureEvidenceProjectPath)!;
        var sourcePath = Path.Combine(testProjectDirectory, $"TestSchedulerSupportTests_{Guid.NewGuid():N}.cs");

        await File.WriteAllTextAsync(
            sourcePath,
            """
using FixtureEvidence.TestSupportProject;
using Moq;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

public sealed class TestSchedulerSupportTests
{
    private readonly Mock<TestClock> _clock = new();

    [Fact]
    public void Smoke()
    {
        var scheduler = new TestScheduler(_clock.Object);
        Assert.NotNull(scheduler);
    }
}
""");

        return sourcePath;
    }

    private static async Task<string> CreateFixtureEvidenceCoverOnlyTestAsync()
    {
        var testProjectDirectory = Path.GetDirectoryName(FixtureEvidenceProjectPath)!;
        var sourcePath = Path.Combine(testProjectDirectory, $"OrderServiceCoverOnlyTests_{Guid.NewGuid():N}.cs");

        await File.WriteAllTextAsync(
            sourcePath,
            """
using FixtureEvidence.TestsProject.Services;
using IoCTools.Testing.Annotations;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

[Cover<OrderService>]
public partial class OrderServiceCoverOnlyTests
{
    [Fact]
    public void Smoke()
    {
        Assert.NotNull(Sut);
    }
}
""");

        return sourcePath;
    }

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

        var lookupRegistration = GetRegistration(
            registrations,
            "OpenGenericProject.Services.IOpenGenericLookup<>");
        var repositoryRegistration = GetRegistration(
            registrations,
            "OpenGenericProject.Services.IOpenGenericRepository<>");
        var concreteRegistration = GetRegistration(
            registrations,
            "OpenGenericProject.Services.OpenGenericRepository<>");

        lookupRegistration.GetProperty("implementationType").GetString()
            .Should().Contain("OpenGenericRepository<>");
        lookupRegistration.GetProperty("lifetime").GetString().Should().Be("Scoped");
        lookupRegistration.GetProperty("usesFactory").GetBoolean().Should().BeFalse();

        repositoryRegistration.GetProperty("implementationType").GetString()
            .Should().Contain("OpenGenericRepository<>");
        repositoryRegistration.GetProperty("lifetime").GetString().Should().Be("Scoped");
        repositoryRegistration.GetProperty("usesFactory").GetBoolean().Should().BeFalse();

        concreteRegistration.GetProperty("implementationType").GetString()
            .Should().Contain("OpenGenericRepository<>");
        concreteRegistration.GetProperty("lifetime").GetString().Should().Be("Scoped");
        concreteRegistration.GetProperty("usesFactory").GetBoolean().Should().BeFalse();

        static JsonElement GetRegistration(JsonElement[] registrations, string serviceType)
        {
            return registrations.Single(registration =>
                Normalize(registration.GetProperty("serviceType").GetString()) == serviceType);
        }

        static string Normalize(string? value)
        {
            return value?.Replace("global::", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        }
    }

    [Fact]
    public void Parses_TestFixtures_Flag()
    {
        var parse = CommandLineParser.ParseEvidence(new[]
        {
            "--project", FieldsProjectPath,
            "--test-fixtures",
            "--production-project", FieldsProjectPath
        });

        if (!parse.Success)
            throw new Xunit.Sdk.XunitException($"Parse failed: {parse.Error}");
        parse.Success.Should().BeTrue();
        parse.Value!.TestFixtures.Should().BeTrue();
        parse.Value.ProductionProjectPath.Should().EndWith("FieldsProject.csproj");
    }

    [Fact]
    public async Task Evidence_WithTestFixtures_RunsSuccessfully()
    {
        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", FieldsProjectPath,
            "--test-fixtures");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Fixture Migration Evidence", "test-fixtures flag should produce fixture evidence section");
    }

    [Fact]
    public async Task Evidence_WithTestFixtures_Json_IncludesFixtureEvidenceSection()
    {
        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", FieldsProjectPath,
            "--test-fixtures",
            "--json");

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("\"fixtureEvidence\"", "JSON output should include fixtureEvidence key");
    }

    [Fact]
    public async Task Evidence_WithTestFixtures_Json_HasClassifications()
    {
        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", FixtureEvidenceProjectPath,
            "--test-fixtures",
            "--json");

        result.ExitCode.Should().Be(0, $"Evidence failed. Stderr: {result.Stderr}");

        using var payload = JsonDocument.Parse(result.Stdout);
        var fe = payload.RootElement.GetProperty("fixtureEvidence");
        fe.TryGetProperty("Classifications", out var classificationsEl).Should().BeTrue();
        var classifications = classificationsEl.EnumerateArray().ToArray();
        classifications.Length.Should().Be(6);

        var kinds = classifications.Select(c => c.GetProperty("Classification").GetString()).ToHashSet();

        kinds.Should().Contain("partial-migration");
        kinds.Should().Contain("semantic-harness");
        kinds.Should().Contain("unknown-review");
        fe.GetProperty("TotalTestClasses").GetInt32().Should().Be(6);
        fe.GetProperty("SafeCount").GetInt32().Should().Be(0);
        fe.GetProperty("PartialCount").GetInt32().Should().Be(2);
        fe.GetProperty("UnknownCount").GetInt32().Should().Be(2);
        fe.GetProperty("SemanticHarnessCount").GetInt32().Should().Be(2);

        var notificationService = classifications.Single(c => c.GetProperty("TestClass").GetString()!.EndsWith("NotificationServicePartialTests"));
        notificationService.GetProperty("Classification").GetString().Should().Be("partial-migration");
        notificationService.GetProperty("Reason").GetString().Should().Contain("Class already uses [Cover<T>]");
    }

    [Fact]
    public async Task Evidence_WithTestFixtures_ProductionProject_PrefersProductionServiceMatch()
    {
        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", FixtureEvidenceProjectPath,
            "--test-fixtures",
            "--production-project", FixtureEvidenceProductionProjectPath,
            "--json");

        result.ExitCode.Should().Be(0, $"Evidence failed. Stderr: {result.Stderr}");

        using var payload = JsonDocument.Parse(result.Stdout);
        var classification = payload.RootElement
            .GetProperty("fixtureEvidence")
            .GetProperty("Classifications")
            .EnumerateArray()
            .Single(c => c.GetProperty("TestClass").GetString()!.EndsWith("ProductionPreferenceServiceTests"));

        classification.GetProperty("Classification").GetString().Should().Be("safe-migration");
        classification.GetProperty("ServiceType").GetString()
            .Should().Be("FixtureEvidence.ProductionProject.Services.ProductionPreferenceService");
    }

    [Fact]
    public async Task Evidence_WithTestFixtures_PrefersExplicitCreateSutConstruction()
    {
        var sourcePath = await CreateFixtureEvidenceExplicitConstructionTestAsync();

        try
        {
            var result = await CliTestHost.RunAsync(
                "evidence",
                "--project", FixtureEvidenceProjectPath,
                "--test-fixtures",
                "--production-project", FixtureEvidenceProductionProjectPath,
                "--json");

            result.ExitCode.Should().Be(0, $"Evidence failed. Stderr: {result.Stderr}");

            using var payload = JsonDocument.Parse(result.Stdout);
            var classification = payload.RootElement
                .GetProperty("fixtureEvidence")
                .GetProperty("Classifications")
                .EnumerateArray()
                .Single(c => c.GetProperty("TestClass").GetString()!.EndsWith("ExplicitConstructionServiceTests"));

            classification.GetProperty("Classification").GetString().Should().Be("safe-migration");
            classification.GetProperty("ServiceType").GetString()
                .Should().Be("FixtureEvidence.ProductionProject.Services.ProductionPreferenceService");
            classification.GetProperty("Reason").GetString().Should().NotContain("potential service matches");
        }
        finally
        {
            if (File.Exists(sourcePath))
                File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task Evidence_WithTestFixtures_PrefersHandlerOverConstructedCommandPayload()
    {
        var sourcePath = await CreateFixtureEvidenceCommandHandlerConstructionTestAsync();

        try
        {
            var result = await CliTestHost.RunAsync(
                "evidence",
                "--project", FixtureEvidenceProjectPath,
                "--test-fixtures",
                "--json");

            result.ExitCode.Should().Be(0, $"Evidence failed. Stderr: {result.Stderr}");

            using var payload = JsonDocument.Parse(result.Stdout);
            var classification = payload.RootElement
                .GetProperty("fixtureEvidence")
                .GetProperty("Classifications")
                .EnumerateArray()
                .Single(c => c.GetProperty("TestClass").GetString()!.EndsWith("ApplyThingCommandHandlerTests"));

            classification.GetProperty("Classification").GetString().Should().Be("safe-migration");
            classification.GetProperty("ServiceType").GetString()
                .Should().Be("FixtureEvidence.TestsProject.Tests.ApplyThingCommandHandler");
        }
        finally
        {
            if (File.Exists(sourcePath))
                File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task Evidence_WithTestFixtures_TreatsIClockAsFixtureProvided()
    {
        var sourcePath = await CreateFixtureEvidenceClockBackedTestAsync();

        try
        {
            var result = await CliTestHost.RunAsync(
                "evidence",
                "--project", FixtureEvidenceProjectPath,
                "--test-fixtures",
                "--json");

            result.ExitCode.Should().Be(0, $"Evidence failed. Stderr: {result.Stderr}");

            using var payload = JsonDocument.Parse(result.Stdout);
            var classification = payload.RootElement
                .GetProperty("fixtureEvidence")
                .GetProperty("Classifications")
                .EnumerateArray()
                .Single(c => c.GetProperty("TestClass").GetString()!.EndsWith("ClockBackedHandlerTests"));

            classification.GetProperty("Classification").GetString().Should().Be("safe-migration");
            classification.GetProperty("ServiceType").GetString()
                .Should().Be("FixtureEvidence.TestsProject.Tests.ClockBackedHandler");
        }
        finally
        {
            if (File.Exists(sourcePath))
                File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task Evidence_WithTestFixtures_ProductionProject_IgnoresTestSupportConstruction()
    {
        var sourcePath = await CreateFixtureEvidenceTestSupportConstructionAsync();

        try
        {
            var result = await CliTestHost.RunAsync(
                "evidence",
                "--project", FixtureEvidenceProjectPath,
                "--test-fixtures",
                "--production-project", FixtureEvidenceProductionProjectPath,
                "--json");

            result.ExitCode.Should().Be(0, $"Evidence failed. Stderr: {result.Stderr}");

            using var payload = JsonDocument.Parse(result.Stdout);
            var classification = payload.RootElement
                .GetProperty("fixtureEvidence")
                .GetProperty("Classifications")
                .EnumerateArray()
                .Single(c => c.GetProperty("TestClass").GetString()!.EndsWith("TestSchedulerSupportTests"));

            classification.GetProperty("Classification").GetString().Should().Be("unknown-review");
            classification.GetProperty("ServiceType").GetString().Should().BeNull();
            classification.GetProperty("Reason").GetString().Should().Contain("No matching service");
        }
        finally
        {
            if (File.Exists(sourcePath))
                File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task Evidence_WithTestFixtures_DoesNotTreatGeneratedMocksAsManualMocks()
    {
        var sourcePath = await CreateFixtureEvidenceCoverOnlyTestAsync();

        try
        {
            var result = await CliTestHost.RunAsync(
                "evidence",
                "--project", FixtureEvidenceProjectPath,
                "--test-fixtures",
                "--json");

            result.ExitCode.Should().Be(0, $"Evidence failed. Stderr: {result.Stderr}");

            using var payload = JsonDocument.Parse(result.Stdout);
            var classification = payload.RootElement
                .GetProperty("fixtureEvidence")
                .GetProperty("Classifications")
                .EnumerateArray()
                .Single(c => c.GetProperty("TestClass").GetString()!.EndsWith("OrderServiceCoverOnlyTests"));

            classification.GetProperty("Classification").GetString().Should().Be("unknown-review");
            classification.GetProperty("Reason").GetString().Should().Contain("No manual Mock<T> fields");
        }
        finally
        {
            if (File.Exists(sourcePath))
                File.Delete(sourcePath);
        }
    }

    [Fact]
    public async Task Evidence_WithTestFixtures_ReportsPartialMigration_WhenExtraManualMocksExist()
    {
        var extraSourcePath = await CreateFixtureEvidenceExtraManualMockTestAsync();

        try
        {
            var result = await CliTestHost.RunAsync(
                "evidence",
                "--project", FixtureEvidenceProjectPath,
                "--test-fixtures",
                "--json");

            result.ExitCode.Should().Be(0, $"Evidence failed. Stderr: {result.Stderr}");

            using var payload = JsonDocument.Parse(result.Stdout);
            var fe = payload.RootElement.GetProperty("fixtureEvidence");
            var classification = fe.GetProperty("Classifications").EnumerateArray()
                .Single(c => c.GetProperty("TestClass").GetString()!.EndsWith("OrderServiceExtraManualMockTests"));

            classification.GetProperty("Classification").GetString().Should().Be("partial-migration");
            classification.GetProperty("Reason").GetString().Should().Contain("manual mock(s) remain");
            classification.GetProperty("ManualMocks").EnumerateArray()
                .Select(e => e.GetString())
                .Should()
                .Contain(s => s!.Contains("IDisposable", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(extraSourcePath))
                File.Delete(extraSourcePath);
        }
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
