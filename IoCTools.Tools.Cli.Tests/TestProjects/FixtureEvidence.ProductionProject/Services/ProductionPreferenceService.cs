using IoCTools.Abstractions.Annotations;

namespace FixtureEvidence.ProductionProject.Services;

public interface IProdRepository
{
}

public interface IProdGateway
{
}

[DependsOn<IProdRepository, IProdGateway>]
public partial class ProductionPreferenceService
{
    public ProductionPreferenceService(
        IProdRepository repository,
        IProdGateway gateway)
    {
    }
}
