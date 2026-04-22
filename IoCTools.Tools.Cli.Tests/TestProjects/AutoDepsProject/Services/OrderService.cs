using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace AutoDepsProject.Services;

public interface IPaymentService
{
    void Charge();
}

[Scoped]
public partial class PaymentService : IPaymentService
{
    public void Charge()
    {
    }
}

// OrderService has ILogger<T> auto-injected via built-in detection, and
// declares IPaymentService explicitly via [DependsOn].
[Scoped]
[DependsOn<IPaymentService>]
public partial class OrderService
{
    public void Place()
    {
        _logger.LogInformation("Order placed");
    }
}
