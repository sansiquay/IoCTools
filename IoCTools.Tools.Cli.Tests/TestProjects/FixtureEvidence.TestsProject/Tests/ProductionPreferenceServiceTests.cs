using FixtureEvidence.ProductionProject.Services;
using Moq;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

public class ProductionPreferenceServiceTests
{
    private readonly Mock<IProdRepository> _repository = new();
    private readonly Mock<IProdGateway> _gateway = new();

    [Fact]
    public void Test()
    {
    }
}
