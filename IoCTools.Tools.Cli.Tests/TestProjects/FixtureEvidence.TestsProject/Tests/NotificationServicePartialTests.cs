using FixtureEvidence.TestsProject.Services;
using IoCTools.Testing.Annotations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

/// <summary>
/// Partial migration candidate: some mocks present but manual construction exists.
/// </summary>
[Cover<NotificationService>]
public partial class NotificationServicePartialTests
{
    private readonly Mock<ISmsClient> _smsClientMock = new();

    [Fact]
    public void SendNotification_ShouldSend()
    {
        Assert.NotNull(_smsClientMock);
    }
}
