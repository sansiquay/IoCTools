using System.Threading;
using System.Threading.Tasks;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RegistrationProject.Services;

public interface IAnalyticsProcessor
{
    void Process();
}

public interface INotificationDispatcher
{
    void Dispatch();
}

[Singleton]
[RegisterAs<IAnalyticsProcessor>]
[DependsOn<ILogger<AnalyticsProcessor>>]
public partial class AnalyticsProcessor : IAnalyticsProcessor
{
    [Inject] private readonly ILogger<AnalyticsProcessor> _logger;

    public void Process() => _logger.LogInformation("Processing");
}

[Scoped]
[RegisterAs<INotificationDispatcher>(InstanceSharing.Shared)]
public partial class NotificationDispatcher : INotificationDispatcher
{
    [InjectConfiguration("Notifications", SupportsReloading = true)]
    private readonly IOptionsSnapshot<NotificationOptions> _options;

    public void Dispatch() => _options.Value.ToString();
}

public class NotificationOptions
{
    public string Channel { get; set; } = "email";
}

[Singleton]
[ConditionalService(ConfigValue = "Features:EnableBackground", Equals = "true")]
[DependsOn<ILogger<BackgroundMetricsService>>]
public partial class BackgroundMetricsService : BackgroundService
{
    [Inject] private readonly ILogger<BackgroundMetricsService> _logger;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Running background metrics");
        return Task.CompletedTask;
    }
}
