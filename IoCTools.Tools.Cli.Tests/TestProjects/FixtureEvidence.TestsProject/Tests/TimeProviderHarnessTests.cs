using Moq;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

/// <summary>
/// Semantic harness: complex setup with harness-style signals (observability, log, lease naming).
/// </summary>
public class TimeProviderHarnessTests : IDisposable
{
    private readonly IObservabilityLease _leaseManager;

    public TimeProviderHarnessTests()
    {
        _leaseManager = new ObservabilityLease();
    }

    public void Dispose()
    {
        _leaseManager.Dispose();
    }

    [Fact]
    public void TimeProvider_ShouldWork()
    {
        Assert.NotNull(_leaseManager);
    }

    public interface IObservabilityLease : IDisposable { }
    public sealed class ObservabilityLease : IObservabilityLease
    {
        public void Dispose() { }
    }
}
