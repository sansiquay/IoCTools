using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

/// <summary>
/// Unknown review: has Mock<T> fields but no matching service found (plain test without Cover).
/// </summary>
public class ReporterUnknownTests
{
    private readonly Mock<ILogger<ReporterUnknownTests>> _loggerMock = new();

    [Fact]
    public void Log_ShouldWork()
    {
        _loggerMock.Object.LogInformation("Test");
        Assert.True(true);
    }
}
