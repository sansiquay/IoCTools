using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace FixtureEvidence.TestsProject.Services;

public interface ISmsClient
{
    Task SendAsync(string to, string message);
}

[Scoped]
public partial class SmsClient : ISmsClient
{
    public Task SendAsync(string to, string message) => Task.CompletedTask;
}

[Scoped]
[DependsOn<ISmsClient>]
public partial class NotificationService
{
}
