using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace FixtureEvidence.TestsProject.Services;

public interface IOrderRepository
{
    Task SaveAsync();
}

public interface IEmailService
{
    Task SendAsync(string to, string subject);
}

public interface IPricingEngine
{
    decimal Calculate(decimal basePrice);
}

[Scoped]
public partial class PricingEngine : IPricingEngine
{
    public decimal Calculate(decimal basePrice) => basePrice * 1.1m;
}

[Scoped]
[DependsOn<IOrderRepository, IEmailService, IPricingEngine>]
public partial class OrderService
{
}
