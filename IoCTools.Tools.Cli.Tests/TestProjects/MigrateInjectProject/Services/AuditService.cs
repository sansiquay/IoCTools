using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace MigrateInjectProject.Services;

public interface IAuditSink
{
    void Write(string entry);
}

[Scoped]
public partial class AuditSink : IAuditSink
{
    public void Write(string entry)
    {
    }
}

// AuditService has an [Inject] ILogger<AuditService> — with auto-deps enabled this is
// covered by the builtin-ILogger auto-dep so the migrator should DELETE the field entirely
// (not emit a [DependsOn<ILogger<...>>] attribute).
//
// The IAuditSink [Inject][ExternalService] field should convert to [DependsOn<IAuditSink>(external: true)].
[Scoped]
public partial class AuditService
{
    [Inject] private readonly ILogger<AuditService> _logger = null!;

    [Inject]
    [ExternalService]
    private readonly IAuditSink _sink = null!;

    public void Record(string entry)
    {
        _logger.LogInformation("Recording {Entry}", entry);
        _sink.Write(entry);
    }
}
