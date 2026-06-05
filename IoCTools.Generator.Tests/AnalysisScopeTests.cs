namespace IoCTools.Generator.Tests;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
///     Framework-level coverage for the diagnostic <see cref="AnalysisScope" /> model and the
///     <see cref="DiagnosticGate" /> that gates emission against it. These tests exercise the
///     gate mechanism in isolation; per-rule behaviour under the gate (IOC081/082/086) lives in
///     <c>ManualRegistrationCarveOutTests</c>.
///     <para>
///         The gate is the single seam where production-only or test-only diagnostics consult
///         the consuming project's <c>IsTestProject</c> MSBuild signal. Individual analyzers
///         declare their scope once (in <see cref="DiagnosticGate" />'s registry) and never
///         re-implement test-project detection.
///     </para>
/// </summary>
public class AnalysisScopeTests
{
    private static readonly DiagnosticDescriptor BothScopedSample =
        DiagnosticDescriptors.OpenGenericHostedServiceSkipped; // IOC073: AnalysisScope.Both

    private static readonly DiagnosticDescriptor ProductionScopedSample =
        DiagnosticDescriptors.ManualRegistrationDuplicatesIoCTools; // IOC081: AnalysisScope.Production

    public static TheoryData<string> ProductionOnlyDiagnosticIds => new()
    {
        "IOC001", "IOC002", "IOC003", "IOC005", "IOC006", "IOC008", "IOC009",
        "IOC012", "IOC013", "IOC015", "IOC027", "IOC032", "IOC033",
        "IOC034", "IOC035", "IOC039", "IOC040", "IOC042", "IOC043",
        "IOC046", "IOC047", "IOC052", "IOC053", "IOC054", "IOC055",
        "IOC056", "IOC057", "IOC058", "IOC059", "IOC060", "IOC061",
        "IOC062", "IOC063", "IOC064", "IOC065", "IOC067", "IOC068",
        "IOC069", "IOC070", "IOC071", "IOC072", "IOC074", "IOC075",
        "IOC076", "IOC078", "IOC079", "IOC081", "IOC082", "IOC083",
        "IOC084", "IOC085", "IOC086", "IOC087", "IOC090", "IOC091",
        "IOC092", "IOC094", "IOC096", "IOC097", "IOC098", "IOC099",
        "IOC103", "IOC104", "IOC105", "IOC106", "IOC107", "IOC108",
        "IOC110", "IOC113"
    };

    public static TheoryData<string> TestOnlyDiagnosticIds => new()
    {
        "TDIAG01", "TDIAG02", "TDIAG03", "TDIAG04",
        "TDIAG05", "TDIAG06", "TDIAG07", "TDIAG08"
    };

    private static AnalyzerConfigOptionsProvider OptionsWith(string? isTestProjectValue)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (isTestProjectValue is not null) properties["build_property.IsTestProject"] = isTestProjectValue;
        return new InMemoryAnalyzerConfigOptionsProvider(properties);
    }

    [Fact]
    public void GetScope_DefaultsToBothForUnregisteredId()
    {
        DiagnosticGate.GetScope("IOC999_NeverRegistered").Should().Be(AnalysisScope.Both);
    }

    [Fact]
    public void GetScope_ReturnsProductionForIOC081()
    {
        DiagnosticGate.GetScope("IOC081").Should().Be(AnalysisScope.Production);
    }

    [Fact]
    public void GetScope_ReturnsProductionForIOC082()
    {
        DiagnosticGate.GetScope("IOC082").Should().Be(AnalysisScope.Production);
    }

    [Fact]
    public void GetScope_ReturnsProductionForIOC086()
    {
        DiagnosticGate.GetScope("IOC086").Should().Be(AnalysisScope.Production);
    }

    [Theory]
    [MemberData(nameof(ProductionOnlyDiagnosticIds))]
    public void GetScope_ReturnsProductionForProductionOnlyDiagnostics(string diagnosticId)
    {
        DiagnosticGate.GetScope(diagnosticId).Should().Be(AnalysisScope.Production);
        DiagnosticGate.ShouldReport(false, CreateDescriptor(diagnosticId)).Should().BeTrue();
        DiagnosticGate.ShouldReport(true, CreateDescriptor(diagnosticId)).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(TestOnlyDiagnosticIds))]
    public void GetScope_ReturnsTestForTestOnlyDiagnostics(string diagnosticId)
    {
        DiagnosticGate.GetScope(diagnosticId).Should().Be(AnalysisScope.Test);
        DiagnosticGate.ShouldReport(false, CreateDescriptor(diagnosticId)).Should().BeFalse();
        DiagnosticGate.ShouldReport(true, CreateDescriptor(diagnosticId)).Should().BeTrue();
    }

    [Fact]
    public void GetScope_ReturnsBothForBrokenCodegenDiagnostics()
    {
        // IOC073 (open-generic IHostedService) and IOC066 (inaccessible IHostedService) are broken
        // codegen everywhere — they must not be quietly suppressed in test projects.
        DiagnosticGate.GetScope("IOC073").Should().Be(AnalysisScope.Both);
        DiagnosticGate.GetScope("IOC066").Should().Be(AnalysisScope.Both);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("FALSE")]
    [InlineData("not-a-bool")]
    public void IsTestProject_TreatsMissingOrFalsyValuesAsProduction(string? value)
    {
        DiagnosticGate.IsTestProject(OptionsWith(value)).Should().BeFalse();
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void IsTestProject_RecognizesTrueRegardlessOfCase(string value)
    {
        DiagnosticGate.IsTestProject(OptionsWith(value)).Should().BeTrue();
    }

    [Fact]
    public void ShouldReport_BothScoped_AlwaysFires()
    {
        DiagnosticGate.ShouldReport(OptionsWith(null), BothScopedSample).Should().BeTrue();
        DiagnosticGate.ShouldReport(OptionsWith("true"), BothScopedSample).Should().BeTrue();
        DiagnosticGate.ShouldReport(OptionsWith("false"), BothScopedSample).Should().BeTrue();
    }

    [Fact]
    public void ShouldReport_ProductionScoped_FiresWhenNotTestProject()
    {
        DiagnosticGate.ShouldReport(OptionsWith(null), ProductionScopedSample).Should().BeTrue();
        DiagnosticGate.ShouldReport(OptionsWith("false"), ProductionScopedSample).Should().BeTrue();
    }

    [Fact]
    public void ShouldReport_ProductionScoped_SuppressedWhenTestProject()
    {
        DiagnosticGate.ShouldReport(OptionsWith("true"), ProductionScopedSample).Should().BeFalse();
    }

    [Fact]
    public void ShouldReport_AcceptsBooleanShortcut()
    {
        // The boolean overload exists so call sites that resolve IsTestProject once for a batch
        // of diagnostics avoid re-walking GlobalOptions per emission.
        DiagnosticGate.ShouldReport(false, ProductionScopedSample).Should().BeTrue();
        DiagnosticGate.ShouldReport(true, ProductionScopedSample).Should().BeFalse();
        DiagnosticGate.ShouldReport(false, BothScopedSample).Should().BeTrue();
        DiagnosticGate.ShouldReport(true, BothScopedSample).Should().BeTrue();
    }

    [Fact]
    public void ShouldReport_TolerantsNullDescriptor()
    {
        DiagnosticGate.ShouldReport(OptionsWith("true"), null!).Should().BeTrue();
        DiagnosticGate.ShouldReport(true, null!).Should().BeTrue();
    }

    private static DiagnosticDescriptor CreateDescriptor(string id)
    {
        return new DiagnosticDescriptor(
            id,
            "title",
            "message",
            "category",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
