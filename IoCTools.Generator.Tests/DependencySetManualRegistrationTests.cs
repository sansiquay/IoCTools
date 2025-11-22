namespace IoCTools.Generator.Tests;

public class DependencySetManualRegistrationTests
{
    [Fact]
    public void DependencySet_WithRegisterAsAll_EmitsIOC052()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

[RegisterAsAll]
public interface Infra : IDependencySet {}

[DependsOn<Infra>]
public partial class Service {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC052").Should().ContainSingle();
    }
}
