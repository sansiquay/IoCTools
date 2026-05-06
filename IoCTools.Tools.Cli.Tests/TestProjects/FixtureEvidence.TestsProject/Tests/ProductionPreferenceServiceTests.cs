using FixtureEvidence.ProductionProject.Services;
using IoCTools.Abstractions.Annotations;
using Moq;
using Xunit;

namespace FixtureEvidence.TestsProject.Tests;

[DependsOn<IProdRepository, IProdGateway>]
public partial class ProductionPreferenceHelperTests
{
    public ProductionPreferenceHelperTests(
        IProdRepository repository,
        IProdGateway gateway)
    {
    }
}

public class ProductionPreferenceServiceTests
{
    private readonly Mock<IProdRepository> _repository = new();
    private readonly Mock<IProdGateway> _gateway = new();

    [Fact]
    public void Test()
    {
    }
}
