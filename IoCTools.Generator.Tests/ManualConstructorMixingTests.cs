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

    [Fact]
    public void GeneratedConstructor_WithIncrementalCompilation_DoesNotProduceIOC041()
    {
        // Regression test for bug where generated constructors in .g.cs files
        // were incorrectly flagged as manual during incremental compilation.
        // This test simulates a real build scenario where:
        // 1. First compilation generates .g.cs files and writes them to disk
        // 2. Second compilation reads those .g.cs files and must correctly
        //    identify them as generated (not manual) constructors
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IClock { }

public partial class ClockService
{
    [Inject] private readonly IClock _clock;
}
";

        // Run incremental compilation: first pass generates files, second pass reads them from disk
        var (firstPass, secondPass) = SourceGeneratorTestHelper.CompileWithIncrementalGeneration(source);

        // First pass: should generate constructor with no conflicts
        firstPass.GetDiagnosticsByCode("IOC041").Should().BeEmpty("First pass: Generated constructor should not trigger IOC041");
        firstPass.GetConstructorSource("ClockService").Should().NotBeNull("First pass: Constructor should be generated");

        // Second pass: when .g.cs files exist on disk, should still not report IOC041
        // This is the key test - the bug manifested in the second pass when the generator
        // saw its own generated .g.cs file and incorrectly flagged it as "manual"
        secondPass.GetDiagnosticsByCode("IOC041").Should().BeEmpty("Second pass: Generated constructor from .g.cs should not trigger IOC041");

        // Verify constructor generation still works in second pass
        secondPass.GetConstructorSource("ClockService").Should().NotBeNull("Second pass: Constructor should be present");
    }
}
