using IoCTools.Abstractions.Annotations;

namespace MigrateInjectProject.Services;

public interface IEmailSender
{
    void Send(string to, string body);
}

public interface ISmsSender
{
    void Send(string to, string body);
}

[Scoped]
public partial class EmailSender : IEmailSender
{
    public void Send(string to, string body)
    {
    }
}

[Scoped]
public partial class SmsSender : ISmsSender
{
    public void Send(string to, string body)
    {
    }
}

// Target service with two [Inject] fields. The migrator should remove both and add
// a class-level [DependsOn<IEmailSender, ISmsSender>].
[Scoped]
public partial class NotificationService
{
    [Inject] private readonly IEmailSender _emailSender = null!;
    [Inject] private readonly ISmsSender _smsSender = null!;

    public void Notify(string to, string body)
    {
        _emailSender.Send(to, body);
        _smsSender.Send(to, body);
    }
}
