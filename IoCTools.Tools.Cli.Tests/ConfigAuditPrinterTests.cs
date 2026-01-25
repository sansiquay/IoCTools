using FluentAssertions;
using Xunit;

namespace IoCTools.Tools.Cli.Tests;

public class ConfigAuditPrinterTests
{
    [Fact]
    public void InferSectionKeyFromTypeName_RemovesSettingsSuffix()
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName("CancellationQueueSettings");
        result.Should().Be("CancellationQueue");
    }

    [Fact]
    public void InferSectionKeyFromTypeName_RemovesConfigurationSuffix()
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName("DatabaseConfiguration");
        result.Should().Be("Database");
    }

    [Fact]
    public void InferSectionKeyFromTypeName_RemovesConfigSuffix()
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName("CacheConfig");
        result.Should().Be("Cache");
    }

    [Fact]
    public void InferSectionKeyFromTypeName_RemovesOptionsSuffix()
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName("RetryOptions");
        result.Should().Be("Retry");
    }

    [Fact]
    public void InferSectionKeyFromTypeName_RemovesObjectSuffix()
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName("TransferObject");
        result.Should().Be("Transfer");
    }

    [Fact]
    public void InferSectionKeyFromTypeName_StripsNamespace()
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName("MyApp.Config.RetryOptions");
        result.Should().Be("Retry");
    }

    [Fact]
    public void InferSectionKeyFromTypeName_HandlesIOptionsGeneric()
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName("IOptions<CancellationQueueOptions>");
        result.Should().Be("CancellationQueue");
    }

    [Fact]
    public void InferSectionKeyFromTypeName_HandlesIOptionsSnapshotGeneric()
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName("IOptionsSnapshot<RetryOptions>");
        result.Should().Be("Retry");
    }

    [Fact]
    public void InferSectionKeyFromTypeName_HandlesFullyQualifiedIOptions()
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName("Microsoft.Extensions.Options.IOptions<DatabaseSettings>");
        result.Should().Be("Database");
    }

    [Fact]
    public void InferSectionKeyFromTypeName_HandlesGenericWithBacktick()
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName("IOptions`1<RetryOptions>");
        result.Should().Be("Retry");
    }

    [Fact]
    public void InferSectionKeyFromTypeName_KeepsPlainTypeName()
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName("ConnectionStrings");
        result.Should().Be("ConnectionStrings");
    }

    [Fact]
    public void InferSectionKeyFromTypeName_EmptyStringReturnsEmpty()
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName("");
        result.Should().Be("");
    }

    [Theory]
    [InlineData("CancellationQueueOptions", "CancellationQueue")]
    [InlineData("AutomationsSettings", "Automations")]
    [InlineData("FeatureFlagsConfig", "FeatureFlags")]
    [InlineData("CacheConfiguration", "Cache")]
    [InlineData("TransferObject", "Transfer")]
    public void InferSectionKeyFromTypeName_VariousTypeNames(string input, string expected)
    {
        var result = ConfigAuditPrinter.InferSectionKeyFromTypeName(input);
        result.Should().Be(expected);
    }
}
