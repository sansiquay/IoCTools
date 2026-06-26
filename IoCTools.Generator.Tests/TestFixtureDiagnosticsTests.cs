namespace IoCTools.Generator.Tests;

using System.Reflection;
using Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Tests for test fixture analyzer diagnostics (TDIAG01 through TDIAG08)
/// These tests validate the diagnostic descriptors are properly configured.
/// The actual diagnostic emission is handled by IoCTools.Testing generator
/// and TestFixtureAnalyzer in the main generator pipeline.
/// </summary>
public class TestFixtureDiagnosticsTests
{
    [Fact]
    public void TDIAG_Descriptors_Have_Correct_Ids()
    {
        // Assert
        DiagnosticDescriptors.ManualMockField.Id.Should().Be("TDIAG01");
        DiagnosticDescriptors.ManualSutConstruction.Id.Should().Be("TDIAG02");
        DiagnosticDescriptors.CouldUseFixture.Id.Should().Be("TDIAG03");
        DiagnosticDescriptors.ServiceMissingConstructor.Id.Should().Be("TDIAG04");
        DiagnosticDescriptors.TestClassNotPartial.Id.Should().Be("TDIAG05");
        DiagnosticDescriptors.FixtureMemberCollision.Id.Should().Be("TDIAG06");
        DiagnosticDescriptors.SetupAfterSutAccess.Id.Should().Be("TDIAG07");
        DiagnosticDescriptors.CouldUseCoverAttribute.Id.Should().Be("TDIAG08");
        DiagnosticDescriptors.ForceMockNonVirtual.Id.Should().Be("TDIAG09");
    }

    [Fact]
    public void TDIAG09_ForceMockNonVirtual_Has_Correct_Properties()
    {
        // Act & Assert
        var descriptor = DiagnosticDescriptors.ForceMockNonVirtual;
        descriptor.Id.Should().Be("TDIAG09");
        descriptor.Title.ToString().Should().Contain("ForceMock");
        descriptor.Category.Should().Be("IoCTools.Testing");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        descriptor.IsEnabledByDefault.Should().BeTrue();
        descriptor.Description.ToString().Should().Contain("virtual");
    }

    [Fact]
    public void TDIAG01_ManualMockField_Has_Correct_Properties()
    {
        // Act & Assert
        var descriptor = DiagnosticDescriptors.ManualMockField;
        descriptor.Id.Should().Be("TDIAG01");
        descriptor.Title.ToString().Should().Contain("Manual Mock");
        descriptor.Category.Should().Be("IoCTools.Testing");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Info);
        descriptor.IsEnabledByDefault.Should().BeTrue();
        descriptor.Description.ToString().Should().Contain("Cover<TService>");
    }

    [Fact]
    public void TDIAG02_ManualSutConstruction_Has_Correct_Properties()
    {
        // Act & Assert
        var descriptor = DiagnosticDescriptors.ManualSutConstruction;
        descriptor.Id.Should().Be("TDIAG02");
        descriptor.Title.ToString().Should().Contain("Manual SUT");
        descriptor.Category.Should().Be("IoCTools.Testing");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Info);
        descriptor.Description.ToString().Should().Contain("CreateSut()");
    }

    [Fact]
    public void TDIAG03_CouldUseFixture_Has_Correct_Properties()
    {
        // Act & Assert
        var descriptor = DiagnosticDescriptors.CouldUseFixture;
        descriptor.Id.Should().Be("TDIAG03");
        descriptor.Title.ToString().Should().Contain("could use");
        descriptor.Category.Should().Be("IoCTools.Testing");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Info);
        descriptor.Description.ToString().Should().Contain("Cover<TService>");
    }

    [Fact]
    public void TDIAG04_ServiceMissingConstructor_Has_Correct_Properties()
    {
        // Act & Assert
        var descriptor = DiagnosticDescriptors.ServiceMissingConstructor;
        descriptor.Id.Should().Be("TDIAG04");
        descriptor.Title.ToString().Should().Contain("no generated constructor");
        descriptor.Category.Should().Be("IoCTools.Testing");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.Description.ToString().Should().Contain("partial");
    }

    [Fact]
    public void TDIAG05_TestClassNotPartial_Has_Correct_Properties()
    {
        // Act & Assert
        var descriptor = DiagnosticDescriptors.TestClassNotPartial;
        descriptor.Id.Should().Be("TDIAG05");
        descriptor.Title.ToString().Should().Contain("partial");
        descriptor.Category.Should().Be("IoCTools.Testing");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.Description.ToString().Should().Contain("partial");
    }

    [Fact]
    public void TDIAG06_FixtureMemberCollision_Has_Correct_Properties()
    {
        // Act & Assert
        var descriptor = DiagnosticDescriptors.FixtureMemberCollision;
        descriptor.Id.Should().Be("TDIAG06");
        descriptor.Title.ToString().Should().Contain("collision");
        descriptor.Category.Should().Be("IoCTools.Testing");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void All_TDIAG_Descriptors_Share_Help_Link_Pattern()
    {
        // Act & Assert
        var descriptors = new[]
        {
            DiagnosticDescriptors.ManualMockField,
            DiagnosticDescriptors.ManualSutConstruction,
            DiagnosticDescriptors.CouldUseFixture,
            DiagnosticDescriptors.ServiceMissingConstructor,
            DiagnosticDescriptors.TestClassNotPartial,
            DiagnosticDescriptors.FixtureMemberCollision,
            DiagnosticDescriptors.SetupAfterSutAccess,
            DiagnosticDescriptors.CouldUseCoverAttribute,
            DiagnosticDescriptors.ForceMockNonVirtual
        };

        foreach (var descriptor in descriptors)
        {
            descriptor.Id.Should().StartWith("TDIAG");
            descriptor.HelpLinkUri.Should().NotBeNullOrEmpty();
            descriptor.HelpLinkUri.Should().Contain("diagnostics.md");
        }
    }

    [Fact]
    public void All_TDIAG_Descriptors_Have_IoCTools_Testing_Category()
    {
        // Act & Assert
        var descriptors = new[]
        {
            DiagnosticDescriptors.ManualMockField,
            DiagnosticDescriptors.ManualSutConstruction,
            DiagnosticDescriptors.CouldUseFixture,
            DiagnosticDescriptors.ServiceMissingConstructor,
            DiagnosticDescriptors.TestClassNotPartial,
            DiagnosticDescriptors.FixtureMemberCollision,
            DiagnosticDescriptors.SetupAfterSutAccess,
            DiagnosticDescriptors.CouldUseCoverAttribute,
            DiagnosticDescriptors.ForceMockNonVirtual
        };

        foreach (var descriptor in descriptors)
        {
            descriptor.Category.Should().Be("IoCTools.Testing");
        }
    }

    #region TDIAG02 Emission Tests

    private static GeneratorTestResult CompileWithCover(
        string source,
        bool isTestProject = true,
        MetadataReference[]? additionalMetadataReferences = null)
    {
        var iocTestingAssembly = typeof(IoCTools.Testing.Annotations.CoverAttribute<>).Assembly;
        MetadataReference[] additionalMetadata = { MetadataReference.CreateFromFile(iocTestingAssembly.Location) };

        // Also need Moq for Mock<T> resolution
        try
        {
            var moqAssembly = Assembly.Load("Moq");
            additionalMetadata = additionalMetadata.Concat(new[] { MetadataReference.CreateFromFile(moqAssembly.Location) }).ToArray();
        }
        catch { }

        try
        {
            var loggingAssembly = Assembly.Load("Microsoft.Extensions.Logging.Abstractions");
            additionalMetadata = additionalMetadata.Concat(new[] { MetadataReference.CreateFromFile(loggingAssembly.Location) }).ToArray();
        }
        catch { }

        additionalMetadata = additionalMetadata
            .Concat(new[] { MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location) })
            .ToArray();

        if (additionalMetadataReferences is { Length: > 0 })
        {
            additionalMetadata = additionalMetadata
                .Concat(additionalMetadataReferences)
                .ToArray();
        }

        return SourceGeneratorTestHelper.CompileWithGenerator(
            source,
            analyzerBuildProperties: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.IsTestProject"] = isTestProject ? "true" : "false"
            },
            additionalMetadataReferences: additionalMetadata);
    }

    [Fact]
    public void TDIAG01_DoesNotReportLoggerMock_WhenCoverUsesNullLogger()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;
using Microsoft.Extensions.Logging;
using Moq;

[DependsOn<ILogger<MyService>>]
public partial class MyService { }

[Cover<MyService>(Logger = FixtureLoggerProfile.NullLogger)]
public partial class MyServiceTests
{
    private readonly Mock<ILogger<MyService>> _logger = new();
}";

        var result = CompileWithCover(source);
        result.Diagnostics.Where(d => d.Id == "TDIAG01").Should().BeEmpty();
    }

    [Fact]
    public void TDIAG03_DetectsSingleIoCToolsService_ForManualMocks()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Moq;
using Xunit;

public interface IDep { }

[DependsOn<IDep>]
public partial class MyService { }

public class MyServiceTests
{
    private readonly Mock<IDep> _dep = new();

    [Fact]
    public void Test() { }
}";

        var result = CompileWithCover(source);
        var tdiag03 = result.Diagnostics.Where(d => d.Id == "TDIAG03").ToList();

        tdiag03.Should().ContainSingle();
        tdiag03[0].GetMessage().Should().Contain("MyService");
    }

    [Fact]
    public void TDIAG03_DoesNotTreatTestClassDefaultConstructor_AsServiceMatch()
    {
        var source = @"
using Moq;
using Xunit;

public interface IDep { }

public class ManualOnlyTests
{
    private readonly Mock<IDep> _dep = new();

    [Fact]
    public void Test() { }
}";

        var result = CompileWithCover(source);
        result.Diagnostics.Where(d => d.Id == "TDIAG03").Should().BeEmpty();
    }

    [Fact]
    public void TDIAG03_DoesNotSuggestOtherTestClasses_AsCoverTargets()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Moq;
using Xunit;

public interface IDep { }

[DependsOn<IDep>]
public partial class HelperTests { }

public class MyServiceTests
{
    private readonly Mock<IDep> _dep = new();

    [Fact]
    public void Test() { }
}";

        var result = CompileWithCover(source);
        result.Diagnostics.Where(d => d.Id == "TDIAG03").Should().BeEmpty();
    }

    [Fact]
    public void TDIAG08_DoesNotSuggestCover_ForConstructedTestClass()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Xunit;

public interface IDep { }

[Scoped]
public partial class HelperTests
{
    public HelperTests(IDep dep) { }
}

public class ManualTests
{
    [Fact]
    public void Test()
    {
        var dep = new Moq.Mock<IDep>().Object;
        var sut = new HelperTests(dep);
    }
}";

        var result = CompileWithCover(source);
        result.Diagnostics.Where(d => d.Id == "TDIAG08").Should().BeEmpty();
    }

    [Fact]
    public void TDIAG04_AllowsDependsOnOnlyServiceIntent()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public interface IDep { }

[DependsOn<IDep>]
public partial class MyService { }

[Cover<MyService>]
public partial class MyServiceTests { }";

        var result = CompileWithCover(source);
        result.Diagnostics.Where(d => d.Id == "TDIAG04").Should().BeEmpty();
    }

    [Fact]
    public void TDIAG04_AllowsMetadataReferencedGeneratedService()
    {
        var productionSource = @"
using IoCTools.Abstractions.Annotations;

namespace Prod;

public interface IDep { }

[DependsOn<IDep>]
public partial class MyService { }";

        var productionResult = SourceGeneratorTestHelper.CompileWithGenerator(
            productionSource,
            analyzerBuildProperties: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.IsTestProject"] = "false"
            });
        productionResult.CompilationDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();

        using var stream = new MemoryStream();
        var emitResult = productionResult.Compilation.Emit(stream);
        emitResult.Success.Should().BeTrue(string.Join(Environment.NewLine, emitResult.Diagnostics));
        var productionReference = MetadataReference.CreateFromImage(stream.ToArray());

        var testSource = @"
using IoCTools.Testing.Annotations;

namespace Prod.Tests;

[Cover<Prod.MyService>]
public partial class MyServiceTests { }";

        var result = CompileWithCover(testSource, additionalMetadataReferences: new[] { productionReference });
        result.Diagnostics.Where(d => d.Id == "TDIAG04").Should().BeEmpty();
    }

    [Fact]
    public void TDIAG02_DetectsExplicitNewService_InMethodBody()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public interface IDep { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

[Cover<MyService>]
public partial class MyServiceTests
{
    [Fact]
    public void Test()
    {
        var dep = new Mock<IDep>().Object;
        var sut = new MyService(dep);
    }
}";

        var result = CompileWithCover(source);
        var tdiag02 = result.Diagnostics.Where(d => d.Id == "TDIAG02").ToList();
        tdiag02.Should().ContainSingle("explicit new Service(...) should emit TDIAG02");
    }

    [Fact]
    public void TDIAG02_DetectsTargetTypedNew_InMethodBody()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public interface IDep { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

[Cover<MyService>]
public partial class MyServiceTests
{
    [Fact]
    public void Test()
    {
        var dep = new Mock<IDep>().Object;
        MyService sut = new(dep);
    }
}";

        var result = CompileWithCover(source);
        var tdiag02 = result.Diagnostics.Where(d => d.Id == "TDIAG02").ToList();
        tdiag02.Should().ContainSingle("target-typed new(...) should emit TDIAG02");
    }

    [Fact]
    public void TDIAG02_DetectsHelperReturningNewService()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public interface IDep { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

[Cover<MyService>]
public partial class MyServiceTests
{
    private static MyService CreateHelper() => new(new Mock<IDep>().Object);

    [Fact]
    public void Test()
    {
        var sut = CreateHelper();
    }
}";

        var result = CompileWithCover(source);
        var tdiag02 = result.Diagnostics.Where(d => d.Id == "TDIAG02").ToList();
        tdiag02.Should().NotBeEmpty("helper returning new Service(...) should emit TDIAG02");
    }

    [Fact]
    public void TDIAG02_DetectsExpressionBodiedCreateSut()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public interface IDep { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

[Cover<MyService>]
public partial class MyServiceTests
{
    private MyService CreateSut() => new(new Mock<IDep>().Object);

    [Fact]
    public void Test()
    {
        var sut = CreateSut();
    }
}";

        var result = CompileWithCover(source);
        var tdiag02 = result.Diagnostics.Where(d => d.Id == "TDIAG02").ToList();
        tdiag02.Should().NotBeEmpty("expression-bodied CreateSut returning new Service(...) should emit TDIAG02");
    }

    [Fact]
    public void TDIAG02_DetectsBlockBodiedCreateHandler()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public interface IDep { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

[Cover<MyService>]
public partial class MyServiceTests
{
    private MyService CreateHandler()
    {
        var dep = new Mock<IDep>().Object;
        return new MyService(dep);
    }

    [Fact]
    public void Test()
    {
        var sut = CreateHandler();
    }
}";

        var result = CompileWithCover(source);
        var tdiag02 = result.Diagnostics.Where(d => d.Id == "TDIAG02").ToList();
        tdiag02.Should().NotBeEmpty("block-bodied CreateHandler returning new Service(...) should emit TDIAG02");
    }

    #endregion

    #region TDIAG07

    [Fact]
    public void TDIAG07_SetupAfterSutAccess_Has_Correct_Properties()
    {
        // Act & Assert
        var descriptor = DiagnosticDescriptors.SetupAfterSutAccess;
        descriptor.Id.Should().Be("TDIAG07");
        descriptor.Title.ToString().Should().Contain("Sut");
        descriptor.Category.Should().Be("IoCTools.Testing");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        descriptor.IsEnabledByDefault.Should().BeTrue();
        descriptor.Description.ToString().Should().Contain("Setup");
    }

    [Fact]
    public void TDIAG07_DetectsFixtureHelperAfterSutAccess()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public interface IDep { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

[Cover<MyService>]
public partial class MyServiceTests
{
    [Fact]
    public void Test()
    {
        var sut = Sut;
        SetupDep(m => { });
    }
}";

        var result = CompileWithCover(source);
        var tdiag07 = result.Diagnostics.Where(d => d.Id == "TDIAG07").ToList();
        tdiag07.Should().NotBeEmpty("Setup* call after Sut access should emit TDIAG07");
    }

    [Fact]
    public void TDIAG07_DoesNotFire_WhenSetupCalledBeforeSut()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public interface IDep { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

[Cover<MyService>]
public partial class MyServiceTests
{
    [Fact]
    public void Test()
    {
        SetupDep(m => { });
        var sut = Sut;
    }
}";

        var result = CompileWithCover(source);
        var tdiag07 = result.Diagnostics.Where(d => d.Id == "TDIAG07").ToList();
        tdiag07.Should().BeEmpty("Setup* before Sut access should not emit TDIAG07");
    }

    [Fact]
    public void TDIAG07_DetectsConfigureCallAfterSut()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public class MyOpts { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

public interface IDep { }

[Cover<MyService>]
public partial class MyServiceTests
{
    [Fact]
    public void Test()
    {
        var sut = Sut;
        ConfigureSomething(o => { });
    }
}";

        var result = CompileWithCover(source);
        var tdiag07 = result.Diagnostics.Where(d => d.Id == "TDIAG07").ToList();
        tdiag07.Should().NotBeEmpty("Configure* call after Sut access should emit TDIAG07");
    }

    [Fact]
    public void TDIAG07_DoesNotFire_ForMoqSetupAfterSut()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public interface IDep { string Name { get; } }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

[Cover<MyService>]
public partial class MyServiceTests
{
    [Fact]
    public void Test()
    {
        var sut = Sut;
        DepMock.Setup(x => x.Name).Returns(""ok"");
    }
}";

        var result = CompileWithCover(source);
        var tdiag07 = result.Diagnostics.Where(d => d.Id == "TDIAG07").ToList();
        tdiag07.Should().BeEmpty("Moq .Setup after Sut is not a generated fixture helper call");
    }

    [Fact]
    public void TDIAG07_DoesNotFire_ForUserHarnessHelperAfterSut()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public interface IDep { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

[Cover<MyService>]
public partial class MyServiceTests
{
    [Fact]
    public void Test()
    {
        var sut = Sut;
        UseSemanticHarness();
    }

    private void UseSemanticHarness() { }
}";

        var result = CompileWithCover(source);
        var tdiag07 = result.Diagnostics.Where(d => d.Id == "TDIAG07").ToList();
        tdiag07.Should().BeEmpty("user-declared harness helpers are not generated fixture setup calls");
    }

    #endregion

    #region TDIAG08

    [Fact]
    public void TDIAG08_CouldUseCoverAttribute_Has_Correct_Properties()
    {
        // Act & Assert
        var descriptor = DiagnosticDescriptors.CouldUseCoverAttribute;
        descriptor.Id.Should().Be("TDIAG08");
        descriptor.Title.ToString().Should().Contain("Cover");
        descriptor.Category.Should().Be("IoCTools.Testing");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Info); // downgraded from Warning in 1.7.2 — advisory, not blocking
        descriptor.IsEnabledByDefault.Should().BeTrue();
        descriptor.Description.ToString().Should().Contain("Cover");
    }

    [Fact]
    public void TDIAG08_DetectsManualServiceConstruction_WithoutCover()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

public interface IDep { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

public class ManualTests
{
    [Fact]
    public void Test()
    {
        var dep = new Moq.Mock<IDep>().Object;
        var sut = new MyService(dep);
    }
}";

        var result = CompileWithCover(source);
        var tdiag08 = result.Diagnostics.Where(d => d.Id == "TDIAG08").ToList();
        tdiag08.Should().NotBeEmpty("manual construction of IoCTools service should emit TDIAG08");
    }

    [Fact]
    public void TDIAG08_DetectsManualServiceConstruction_ForDependsOnOnlyService()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

public interface IDep { }

[DependsOn<IDep>]
public partial class MyService { }

public class ManualTests
{
    [Fact]
    public void Test()
    {
        var dep = new Moq.Mock<IDep>().Object;
        var sut = new MyService(dep);
    }
}";

        var result = CompileWithCover(source);
        var tdiag08 = result.Diagnostics.Where(d => d.Id == "TDIAG08").ToList();
        tdiag08.Should().NotBeEmpty("DependsOn-only IoCTools services can use Cover<T>");
    }

    [Fact]
    public void TDIAG08_DoesNotFire_OutsideTestProject()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

public interface IDep { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

public class ManualTests
{
    [Fact]
    public void Test()
    {
        var dep = new Moq.Mock<IDep>().Object;
        var sut = new MyService(dep);
    }
}";

        var result = CompileWithCover(source, isTestProject: false);
        var tdiag08 = result.Diagnostics.Where(d => d.Id == "TDIAG08").ToList();
        tdiag08.Should().BeEmpty("TDIAG diagnostics are test-project scoped");
    }


    [Fact]
    public void TDIAG08_DoesNotFire_WhenClassAlreadyHasCover()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public interface IDep { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

[Cover<MyService>]
public partial class MyServiceTests
{
    [Fact]
    public void Test()
    {
        var sut = new MyService(new Moq.Mock<IDep>().Object);
    }
}";

        var result = CompileWithCover(source);
        var tdiag08 = result.Diagnostics.Where(d => d.Id == "TDIAG08").ToList();
        tdiag08.Should().BeEmpty("class with [Cover<T>] should not emit TDIAG08");
    }

    [Fact]
    public void TDIAG08_RespectsIoCToolsTestingDiagnosticSeverity_WhenSetToWarning()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

public interface IDep { }

[Scoped]
[DependsOn<IDep>]
public partial class MyService { }

public class ManualTests
{
    [Fact]
    public void Test()
    {
        var dep = new Moq.Mock<IDep>().Object;
        var sut = new MyService(dep);
    }
}";

        var iocTestingAssembly = typeof(IoCTools.Testing.Annotations.CoverAttribute<>).Assembly;
        var result = SourceGeneratorTestHelper.CompileWithGenerator(
            source,
            analyzerBuildProperties: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.IsTestProject"] = "true",
                ["build_property.IoCToolsTestingDiagnosticSeverity"] = "Warning"
            },
            additionalMetadataReferences: new MetadataReference[]
            {
                MetadataReference.CreateFromFile(iocTestingAssembly.Location),
                MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location)
            });

        var tdiag08 = result.Diagnostics.Where(d => d.Id == "TDIAG08").ToList();
        tdiag08.Should().NotBeEmpty("manual construction should emit TDIAG08 when property is set");
        tdiag08.Should().AllSatisfy(d => d.Severity.Should().Be(DiagnosticSeverity.Warning),
            "IoCToolsTestingDiagnosticSeverity=Warning should escalate TDIAG08 from Info to Warning");
    }

    #endregion

    #region TDIAG09 Emission Tests — ForceMock against non-virtual concrete

    [Fact]
    public void TDIAG09_Fires_WhenForceMockTargetsConcreteWithNonVirtualMethods()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public partial class Collaborator
{
    public int Compute(int x) => x + 1;
}

[DependsOn<Collaborator>]
public partial class MyService { }

[Cover<MyService>(ConcreteHandling = ConcreteHandling.ForceMock)]
public partial class MyServiceTests { }";

        var result = CompileWithCover(source);
        var tdiag09 = result.Diagnostics.Where(d => d.Id == "TDIAG09").ToList();

        tdiag09.Should().ContainSingle("ForceMock on a concrete with only non-virtual methods cannot be intercepted by Moq");
        tdiag09[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        tdiag09[0].GetMessage().Should().Contain("Collaborator");
        tdiag09[0].GetMessage().Should().Contain("MyService");
    }

    [Fact]
    public void TDIAG09_DoesNotFire_WhenConcreteHasVirtualMethod()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public partial class Collaborator
{
    public virtual int Compute(int x) => x + 1;
}

[DependsOn<Collaborator>]
public partial class MyService { }

[Cover<MyService>(ConcreteHandling = ConcreteHandling.ForceMock)]
public partial class MyServiceTests { }";

        var result = CompileWithCover(source);
        result.Diagnostics.Where(d => d.Id == "TDIAG09").Should().BeEmpty(
            "a virtual method is interceptable by Moq, so ForceMock is safe");
    }

    [Fact]
    public void TDIAG09_DoesNotFire_WhenDependencyIsInterface()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public interface ICollaborator { int Compute(int x); }

[DependsOn<ICollaborator>]
public partial class MyService { }

[Cover<MyService>(ConcreteHandling = ConcreteHandling.ForceMock)]
public partial class MyServiceTests { }";

        var result = CompileWithCover(source);
        result.Diagnostics.Where(d => d.Id == "TDIAG09").Should().BeEmpty(
            "interface members are always interceptable by Moq");
    }

    [Fact]
    public void TDIAG09_DoesNotFire_WhenConcreteHandlingIsAuto()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public partial class Collaborator
{
    public int Compute(int x) => x + 1;
}

[DependsOn<Collaborator>]
public partial class MyService { }

[Cover<MyService>]
public partial class MyServiceTests { }";

        var result = CompileWithCover(source);
        result.Diagnostics.Where(d => d.Id == "TDIAG09").Should().BeEmpty(
            "default Auto mode constructs a real instance, not a Mock<T>, so the footgun does not apply");
    }

    [Fact]
    public void TDIAG09_Fires_ForPocoWithOnlyProperties()
    {
        // A POCO whose surface is properties only has no overridable *methods*; its property
        // accessors are not mockable behavior. Per the issue, ForceMock on a type with zero
        // overridable public instance methods is the footgun.
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;

public partial class Settings
{
    public string Name { get; set; } = string.Empty;
}

[DependsOn<Settings>]
public partial class MyService { }

[Cover<MyService>(ConcreteHandling = ConcreteHandling.ForceMock)]
public partial class MyServiceTests { }";

        var result = CompileWithCover(source);
        result.Diagnostics.Where(d => d.Id == "TDIAG09").Should().ContainSingle(
            "a property-only POCO has no overridable methods for Moq to intercept");
    }

    #endregion
}
