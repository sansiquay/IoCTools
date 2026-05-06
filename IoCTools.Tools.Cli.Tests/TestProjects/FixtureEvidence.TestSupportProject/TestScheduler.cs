namespace FixtureEvidence.TestSupportProject;

public interface ITestClock
{
}

public sealed class TestClock : ITestClock
{
}

public sealed class TestScheduler
{
    public TestScheduler(TestClock clock)
    {
    }
}
