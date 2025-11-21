#nullable enable
namespace FieldsProject.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

public partial class TelemetryReporter
{
    private readonly ILogger<TelemetryReporter> _logger;
    private readonly IMetricsClient _metricsClient;
    private readonly string _endpoint;
    private readonly int _observabilityTimeoutSeconds;

    public TelemetryReporter(ILogger<TelemetryReporter> logger, IMetricsClient metricsClient, IConfiguration configuration)
    {
        _logger = logger;
        _metricsClient = metricsClient;
        _endpoint = configuration.GetValue<string>("Observability:Endpoint")!;
        _observabilityTimeoutSeconds = configuration.GetValue<int>("Observability:TimeoutSeconds");
    }
}
