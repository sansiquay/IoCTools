namespace IoCTools.Generator.Tests;

/// <summary>
///     Regression coverage for analyzer routing by project type. Production graph/style
///     diagnostics should not appear in test projects, while codegen correctness diagnostics
///     and TDIAG diagnostics keep their explicit scopes.
/// </summary>
public sealed class TestProjectAnalyzerScopeTests
{
    private static Dictionary<string, string> TestProjectProperties() => new(StringComparer.Ordinal)
    {
        ["build_property.IsTestProject"] = "true"
    };

    [Fact]
    public void ProductionOnly_RawConfigurationDependency_SuppressedInTestProject()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

[Scoped]
[DependsOn<IConfiguration>]
public partial class ConfigConsumer { }
";

        SourceGeneratorTestHelper.CompileWithGenerator(source)
            .GetDiagnosticsByCode("IOC079")
            .Should().ContainSingle();

        SourceGeneratorTestHelper.CompileWithGenerator(source, analyzerBuildProperties: TestProjectProperties())
            .GetDiagnosticsByCode("IOC079")
            .Should().BeEmpty();
    }

    [Fact]
    public void ProductionOnly_MissingDependencyGraph_SuppressedInTestProject()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

public interface IMissingDependency { }

[Scoped]
[DependsOn<IMissingDependency>]
public partial class NeedsMissingDependency { }
";

        SourceGeneratorTestHelper.CompileWithGenerator(source)
            .GetDiagnosticsByCode("IOC001")
            .Should().ContainSingle();

        SourceGeneratorTestHelper.CompileWithGenerator(source, analyzerBuildProperties: TestProjectProperties())
            .GetDiagnosticsByCode("IOC001")
            .Should().BeEmpty();
    }

    [Fact]
    public void ProductionOnly_CircularDependencyGraph_SuppressedInTestProject()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

[Scoped]
[DependsOn<ServiceB>]
public partial class ServiceA { }

[Scoped]
[DependsOn<ServiceA>]
public partial class ServiceB { }
";

        SourceGeneratorTestHelper.CompileWithGenerator(source)
            .GetDiagnosticsByCode("IOC003")
            .Should().NotBeEmpty();

        SourceGeneratorTestHelper.CompileWithGenerator(source, analyzerBuildProperties: TestProjectProperties())
            .GetDiagnosticsByCode("IOC003")
            .Should().BeEmpty();
    }

    [Fact]
    public void ProductionOnly_ConstructorOpportunity_SuppressedInTestProject()
    {
        const string source = @"
public interface IDependency { }

public class PlainService
{
    public PlainService(IDependency dependency) { }
}
";

        SourceGeneratorTestHelper.CompileWithGenerator(source)
            .GetDiagnosticsByCode("IOC068")
            .Should().ContainSingle();

        SourceGeneratorTestHelper.CompileWithGenerator(source, analyzerBuildProperties: TestProjectProperties())
            .GetDiagnosticsByCode("IOC068")
            .Should().BeEmpty();
    }

    [Fact]
    public void CodegenCorrectness_MissingPartial_StillReportsInTestProject()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

[DependsOn<IDependency>]
[Scoped]
public class MissingPartialService { }

public interface IDependency { }
";

        SourceGeneratorTestHelper.CompileWithGenerator(source, analyzerBuildProperties: TestProjectProperties())
            .GetDiagnosticsByCode("IOC080")
            .Should().ContainSingle();
    }
}
