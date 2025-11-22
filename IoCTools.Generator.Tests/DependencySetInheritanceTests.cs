namespace IoCTools.Generator.Tests;

public class DependencySetInheritanceTests
{
    [Fact]
    public void DependencySet_Inheritance_DedupesDependencies()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

[DependsOn<ILogger<Service>>(memberNames: new[] { ""logger"" })]
public interface BaseInfra : IDependencySet {}

[DependsOn<BaseInfra>]
public interface DerivedInfra : IDependencySet {}

[Scoped]
[DependsOn<DerivedInfra>]
public partial class Service {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ctor = result.GetConstructorSourceText("Service");
        ctor.Should().Contain("ILogger<Service> logger");
        result.GetDiagnosticsByCode("IOC051").Should().BeEmpty();
    }

    [Fact]
    public void DependencySet_Inheritance_NameCollisionRaisesIOC051()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

[DependsOn<ILogger<Service>>(memberNames: new[] { ""loggerA"" })]
public interface BaseInfra : IDependencySet {}

[DependsOn<BaseInfra>]
[DependsOn<ILogger<Service>>(memberNames: new[] { ""loggerB"" })]
public interface DerivedInfra : IDependencySet {}

[DependsOn<DerivedInfra>]
public partial class Service {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC051").Should().ContainSingle();
    }
}
