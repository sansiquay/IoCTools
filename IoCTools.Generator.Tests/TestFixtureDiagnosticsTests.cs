namespace IoCTools.Generator.Tests;

using Diagnostics;

/// <summary>
/// Tests for test fixture analyzer diagnostics (TDIAG-01 through TDIAG-05)
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
        DiagnosticDescriptors.ManualMockField.Id.Should().Be("TDIAG-01");
        DiagnosticDescriptors.ManualSutConstruction.Id.Should().Be("TDIAG-02");
        DiagnosticDescriptors.CouldUseFixture.Id.Should().Be("TDIAG-03");
        DiagnosticDescriptors.ServiceMissingConstructor.Id.Should().Be("TDIAG-04");
        DiagnosticDescriptors.TestClassNotPartial.Id.Should().Be("TDIAG-05");
    }

    [Fact]
    public void TDIAG01_ManualMockField_Has_Correct_Properties()
    {
        // Act & Assert
        var descriptor = DiagnosticDescriptors.ManualMockField;
        descriptor.Id.Should().Be("TDIAG-01");
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
        descriptor.Id.Should().Be("TDIAG-02");
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
        descriptor.Id.Should().Be("TDIAG-03");
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
        descriptor.Id.Should().Be("TDIAG-04");
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
        descriptor.Id.Should().Be("TDIAG-05");
        descriptor.Title.ToString().Should().Contain("partial");
        descriptor.Category.Should().Be("IoCTools.Testing");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.Description.ToString().Should().Contain("partial");
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
            DiagnosticDescriptors.TestClassNotPartial
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
            DiagnosticDescriptors.TestClassNotPartial
        };

        foreach (var descriptor in descriptors)
        {
            descriptor.Category.Should().Be("IoCTools.Testing");
        }
    }
}
