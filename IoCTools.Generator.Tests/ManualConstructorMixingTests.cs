namespace IoCTools.Generator.Tests;


public class ManualConstructorMixingTests
{
    [Fact]
    public void DependsOnWithManualConstructor_EmitsIOC041_AndSuppressesUnusedWarnings()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IDiagnosticsSettings { }
public interface IDeltaDbContext { }
public interface ILogger { }

[DependsOn<IDeltaDbContext, ILogger, IDiagnosticsSettings>]
public sealed class HttpRequestRepository
{
    private readonly IDiagnosticsSettings _diagnosticsSettings;
    private readonly ILogger _logger;

    public HttpRequestRepository(IDeltaDbContext deltaDbContext, ILogger logger, IDiagnosticsSettings diagnosticsSettings)
    {
        _logger = logger;
        _diagnosticsSettings = diagnosticsSettings;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var conflicts = result.GetDiagnosticsByCode("IOC041");
        conflicts.Should().ContainSingle();
        conflicts[0].Severity.Should().Be(DiagnosticSeverity.Error);

        // Upstream unused-dependency warnings should be suppressed when manual constructors are present.
        result.GetDiagnosticsByCode("IOC039").Should().BeEmpty();
    }

    [Fact]
    public void InjectFieldWithManualConstructor_ProducesIOC041()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IClock { }

public partial class ClockService
{
    [Inject] private readonly IClock _clock;

    public ClockService(IClock clock) => _clock = clock;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var conflicts = result.GetDiagnosticsByCode("IOC041");
        conflicts.Should().ContainSingle();
        conflicts[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void PrimaryConstructor_WithDependsOn_ProducesIOC041()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

[DependsOn<ILogger>]
public partial record PrimaryCtorService(ILogger logger)
{
    public string Value => logger.ToString();
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var conflicts = result.GetDiagnosticsByCode("IOC041");
        conflicts.Should().ContainSingle();
        conflicts[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void NoIoCDependenciesWithManualConstructor_DoesNotReportIOC041()
    {
        var source = @"
namespace Test;

public class PlainService
{
    public PlainService(int seed) => Seed = seed;

    public int Seed { get; }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC041").Should().BeEmpty();
    }

    [Fact]
    public void GeneratedConstructor_DoesNotProduceIOC041()
    {
        // When IoCTools generates a constructor for [Inject] fields,
        // it should NOT trigger IOC041 (manual constructor conflict).
        // This is a regression test for the bug where generated constructors
        // in .g.cs files were incorrectly flagged as manual.
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IClock { }

public partial class ClockService
{
    [Inject] private readonly IClock _clock;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should generate constructor successfully with no conflicts
        result.GetDiagnosticsByCode("IOC041").Should().BeEmpty("Generated constructor should not trigger IOC041");

        // Verify the constructor was actually generated
        var constructorSource = result.GetConstructorSource("ClockService");
        constructorSource.Should().NotBeNull("Constructor should be generated for ClockService");
    }
}
