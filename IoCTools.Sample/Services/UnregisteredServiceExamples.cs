namespace IoCTools.Sample.Services;

using Abstractions.Annotations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Comprehensive examples demonstrating ManualService attribute usage patterns.
/// These services generate constructors but are not automatically registered in DI container.
/// </summary>

#region Basic Unregistered Service Examples

/// <summary>
///     Simple unregistered service - generates constructor but no registration
///     Must be manually registered if needed
/// </summary>
[DependsOn<ILogger<ManualRegistrationService>>]public partial class ManualRegistrationService : IManualRegistrationService
{

    public async Task ProcessAsync(string message)
    {
        _logger.LogInformation("Manual service processed message: {Message}", message);
        await Task.Delay(10);
    }
}

/// <summary>
///     Legacy service that's been replaced but still needs constructor generation
///     Not registered to prevent accidental use
/// </summary>
[DependsOn<ILogger<UnregisteredLegacyPaymentProcessor>>]public partial class UnregisteredLegacyPaymentProcessor : ILegacyPaymentProcessor
{

    public async Task<PaymentResult> ProcessLegacyPaymentAsync(Payment payment)
    {
        _logger.LogWarning("Legacy payment processor used - should migrate to new processor");

        // Legacy logic here
        await Task.Delay(50);
        return new PaymentResult(true, "Legacy processing completed");
    }
}

/// <summary>
///     Test helper service - generates constructor but not registered
///     Used for testing without affecting production DI container
/// </summary>
[DependsOn<ILogger<TestHelperService>>]public partial class TestHelperService : ITestHelperService
{

    public async Task<bool> ValidateAndCreateUserAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Invalid name for test user creation: {Name}", name);
            return false;
        }

        _logger.LogInformation("Test user created: {Name}", name);
        await Task.Delay(10);
        return true;
    }
}

#endregion

#region Inheritance with Unregistered Services

/// <summary>
///     Base service that provides common functionality but shouldn't be registered
///     Derived classes decide their registration strategy
/// </summary>
[DependsOn<ILogger<BasePaymentProcessor>>(memberName1:"Logger")]public partial class BasePaymentProcessor
{

    protected async Task LogPaymentAttemptAsync(string paymentMethod,
        decimal amount)
    {
        Logger.LogInformation("Payment attempt: {Method} for ${Amount}", paymentMethod, amount);
        await Task.Delay(5);
    }
}

/// <summary>
///     New payment processor - inherits from unregistered base but is registered
///     This demonstrates that registered classes can inherit from  bases
/// </summary>
[DependsOn<ILogger<UnregisteredNewPaymentProcessor>>(memberName1:"_specificLogger")]public partial class UnregisteredNewPaymentProcessor : BasePaymentProcessor, INewPaymentProcessor
{

    public async Task<PaymentResult> ProcessNewPaymentAsync(Payment payment)
    {
        await LogPaymentAttemptAsync("new-processor", payment.Amount);

        _specificLogger.LogInformation("Processing payment via new processor");
        await Task.Delay(20); // Simulate processing

        return new PaymentResult(true, "Payment processed successfully");
    }
}

/// <summary>
///     Another unregistered service in the inheritance chain
///     Demonstrates complex inheritance scenarios
/// </summary>
[DependsOn<ILogger<AdvancedPaymentBase>>(memberName1:"_advancedLogger")]public partial class AdvancedPaymentBase : BasePaymentProcessor
{

    protected Task<bool> ValidatePaymentAsync(decimal amount)
    {
        if (amount <= 0)
        {
            _advancedLogger.LogWarning("Invalid payment amount: {Amount}", amount);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}

/// <summary>
///     Final registered service in complex inheritance chain
/// </summary>
[DependsOn<ILogger<EnterprisePaymentProcessor>>(memberName1:"_enterpriseLogger")]public partial class EnterprisePaymentProcessor : AdvancedPaymentBase, IEnterprisePaymentProcessor
{

    public async Task<PaymentResult> ProcessEnterprisePaymentAsync(Payment payment,
        int orderId)
    {
        // Validate payment from AdvancedPaymentBase
        if (!await ValidatePaymentAsync(payment.Amount)) return new PaymentResult(false, "Invalid payment amount");

        // Log attempt from BasePaymentProcessor  
        await LogPaymentAttemptAsync("enterprise", payment.Amount);

        _enterpriseLogger.LogInformation("Processing enterprise payment for order {OrderId}", orderId);
        await Task.Delay(30);

        return new PaymentResult(true, $"Enterprise payment processed for order: {orderId}");
    }
}

#endregion

#region Factory Pattern with Unregistered Services

/// <summary>
///     Factory that creates unregistered services manually
///     Demonstrates controlled instantiation of unregistered services
/// </summary>
[Singleton]
[DependsOn<ILogger<ManualServiceFactory>,IServiceProvider>]public partial class ManualServiceFactory : IManualServiceFactory
{

    public ManualRegistrationService CreateManualRegistrationService()
    {
        _logger.LogDebug("Creating manual registration service via factory");

        // Manual constructor call with resolved dependencies
        var logger = _serviceProvider.GetRequiredService<ILogger<ManualRegistrationService>>();

        return new ManualRegistrationService(logger);
    }

    public TestHelperService CreateTestHelperService()
    {
        _logger.LogDebug("Creating test helper service via factory");

        var logger = _serviceProvider.GetRequiredService<ILogger<TestHelperService>>();

        return new TestHelperService(logger);
    }

    public UnregisteredLegacyPaymentProcessor CreateLegacyProcessor()
    {
        _logger.LogDebug("Creating legacy payment processor via factory");

        var logger = _serviceProvider.GetRequiredService<ILogger<UnregisteredLegacyPaymentProcessor>>();

        return new UnregisteredLegacyPaymentProcessor(logger);
    }
}

#endregion

#region Interface Definitions for Unregistered Services

public interface IManualRegistrationService
{
    Task ProcessAsync(string message);
}

public interface ILegacyPaymentProcessor
{
    Task<PaymentResult> ProcessLegacyPaymentAsync(Payment payment);
}

public interface INewPaymentProcessor
{
    Task<PaymentResult> ProcessNewPaymentAsync(Payment payment);
}

public interface IEnterprisePaymentProcessor
{
    Task<PaymentResult> ProcessEnterprisePaymentAsync(Payment payment,
        int orderId);
}

public interface ITestHelperService
{
    Task<bool> ValidateAndCreateUserAsync(string name);
}

public interface IManualServiceFactory
{
    ManualRegistrationService CreateManualRegistrationService();
    TestHelperService CreateTestHelperService();
    UnregisteredLegacyPaymentProcessor CreateLegacyProcessor();
}

#endregion
