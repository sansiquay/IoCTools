using IoCTools.Generator.Diagnostics;

using Xunit;

namespace IoCTools.Generator.Tests;

public sealed class NewDiagnosticDescriptorTests
{
    [Theory]
    [InlineData("IOC095", nameof(DiagnosticDescriptors.InjectDeprecated))]
    [InlineData("IOC096", nameof(DiagnosticDescriptors.NoAutoDepStale))]
    [InlineData("IOC097", nameof(DiagnosticDescriptors.ProfileMissingMarker))]
    [InlineData("IOC098", nameof(DiagnosticDescriptors.DependsOnAutoDepOverlap))]
    [InlineData("IOC099", nameof(DiagnosticDescriptors.AutoDepsApplyStale))]
    [InlineData("IOC106", nameof(DiagnosticDescriptors.AutoDepOpenMultiArity))]
    [InlineData("IOC107", nameof(DiagnosticDescriptors.AutoDepOpenNonGeneric))]
    [InlineData("IOC108", nameof(DiagnosticDescriptors.AutoDepOpenConstraintViolation))]
    [InlineData("IOC103", nameof(DiagnosticDescriptors.AutoDepsApplyGlobInvalid))]
    [InlineData("IOC104", nameof(DiagnosticDescriptors.ProfileIsGeneric))]
    [InlineData("IOC105", nameof(DiagnosticDescriptors.RedundantProfileAttachment))]
    public void Descriptor_has_expected_id(string expectedId, string descriptorName)
    {
        var field = typeof(DiagnosticDescriptors).GetField(descriptorName);
        field.Should().NotBeNull($"descriptor '{descriptorName}' should exist as a public static field");
        var descriptor = (DiagnosticDescriptor)field!.GetValue(null)!;
        descriptor.Id.Should().Be(expectedId);
        descriptor.HelpLinkUri.Should().StartWith("https://github.com/sansiquay/IoCTools/blob/main/docs/");
    }

    [Fact]
    public void IOC095_uses_migration_help_link()
    {
        DiagnosticDescriptors.InjectDeprecated.HelpLinkUri
            .Should().Contain("migration.md#migrating-from-15x-to-16x");
    }

    [Theory]
    [InlineData(nameof(DiagnosticDescriptors.NoAutoDepStale), "ioc096")]
    [InlineData(nameof(DiagnosticDescriptors.ProfileMissingMarker), "ioc097")]
    [InlineData(nameof(DiagnosticDescriptors.DependsOnAutoDepOverlap), "ioc098")]
    [InlineData(nameof(DiagnosticDescriptors.AutoDepsApplyStale), "ioc099")]
    [InlineData(nameof(DiagnosticDescriptors.AutoDepOpenMultiArity), "ioc106")]
    [InlineData(nameof(DiagnosticDescriptors.AutoDepOpenNonGeneric), "ioc107")]
    [InlineData(nameof(DiagnosticDescriptors.AutoDepOpenConstraintViolation), "ioc108")]
    [InlineData(nameof(DiagnosticDescriptors.AutoDepsApplyGlobInvalid), "ioc103")]
    [InlineData(nameof(DiagnosticDescriptors.ProfileIsGeneric), "ioc104")]
    [InlineData(nameof(DiagnosticDescriptors.RedundantProfileAttachment), "ioc105")]
    public void AutoDeps_descriptor_help_link_uses_auto_deps_anchor(string descriptorName, string expectedAnchor)
    {
        var field = typeof(DiagnosticDescriptors).GetField(descriptorName);
        var descriptor = (DiagnosticDescriptor)field!.GetValue(null)!;
        descriptor.HelpLinkUri
            .Should().Be($"https://github.com/sansiquay/IoCTools/blob/main/docs/auto-deps.md#{expectedAnchor}");
    }
}
