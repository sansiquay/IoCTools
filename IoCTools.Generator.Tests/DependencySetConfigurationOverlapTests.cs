namespace IoCTools.Generator.Tests;

public class DependencySetConfigurationOverlapTests
{
    [Fact]
    public void DependsOnConfiguration_FromSet_And_PrimitiveOnConsumer_EmitsIOC056()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

[DependsOnConfiguration<BillingOptions>(""Billing"")]
public interface BillingInfra : IDependencySet {}

public class BillingOptions { public string BaseUrl { get; set; } = string.Empty; }

[DependsOn<BillingInfra>]
public partial class Service
{
    [InjectConfiguration(""Billing:BaseUrl"")] private readonly string _baseUrl;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC056").Should().ContainSingle();
    }
}
