namespace IoCTools.FluentValidation.Tests;

using FluentAssertions;

using IoCTools.FluentValidation.Diagnostics;

public sealed class DescriptorTests
{
    [Fact]
    public void ValidatorDirectInstantiation_MessageFormat_EndsWithPeriod_ForMultiSentenceDescriptor()
    {
        var message = FluentValidationDiagnosticDescriptors.ValidatorDirectInstantiation.MessageFormat.ToString();

        message.Should().NotContain("\n");
        message.Should().NotContain("\r");
        message.Should().NotEndWith(" ");
        message.Should().EndWith(".");
    }
}
