using FixtureEvidence.TestsProject.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

/// <summary>
/// Semantic harness: class name contains "Harness", uses lifecycle setup, constructor injection patterns.
/// </summary>
public class PricingHarnessTests
{
    private readonly Mock<IPricingEngine> _pricingMock = new();

    public PricingHarnessTests()
    {
        _pricingMock.Setup(p => p.Calculate(It.IsAny<decimal>())).Returns(100m);
    }

    [Fact]
    public void Price_ShouldCalculate()
    {
        Assert.NotNull(_pricingMock);
    }
}
