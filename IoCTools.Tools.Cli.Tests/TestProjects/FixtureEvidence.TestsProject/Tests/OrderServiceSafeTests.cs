using FixtureEvidence.TestsProject.Services;
using IoCTools.Testing.Annotations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

/// <summary>
/// Safe migration candidate: all deps mocked with matching Cover<OrderService>.
/// </summary>
[Cover<OrderService>]
public partial class OrderServiceSafeTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<IPricingEngine> _pricingEngineMock = new();
    private readonly Mock<ILogger<OrderService>> _loggerMock = new();

    [Fact]
    public void ProcessOrder_ShouldSucceed()
    {
        Assert.NotNull(_orderRepositoryMock);
    }
}
