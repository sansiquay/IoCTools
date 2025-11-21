using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace FieldsProject.Services;

public interface IMetricsClient
{
    void Increment();
}

[Singleton]
public partial class MetricsClient : IMetricsClient
{
    public void Increment()
    {
    }
}

[DependsOn<ILogger<TelemetryReporter>, IMetricsClient>]
[DependsOnConfiguration<string>("Observability:Endpoint", MemberNames = new[] { "_endpoint" })]
[DependsOnConfiguration<int>("Observability:TimeoutSeconds", RequiredFlags = new[] { false })]
public partial class TelemetryReporter
{
    public void Report()
    {
        _logger.LogInformation("Reporting with endpoint {Endpoint}", _endpoint);
    }
}
