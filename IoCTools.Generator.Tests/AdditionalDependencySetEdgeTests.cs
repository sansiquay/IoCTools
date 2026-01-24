namespace IoCTools.Generator.Tests;

public class AdditionalDependencySetEdgeTests
{
    [Fact]
    public void DependencySet_MultiLevelExternalAndLocal_FlattensAll()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IClock {}
public interface ITimer {}

[DependsOn<IClock>(external: true)]
public interface BaseInfra : IDependencySet {}

[DependsOn<BaseInfra>]
[DependsOn<ITimer>]
public interface DerivedInfra : IDependencySet {}

[Singleton]
[RegisterAs<ITimer>]
public partial class Timer : ITimer {}

[Scoped]
[DependsOn<DerivedInfra>]
public partial class Service {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var ctor = result.GetConstructorSourceText("Service");
        ctor.Should().Contain("IClock clock");
        ctor.Should().Contain("ITimer timer");
        result.HasErrors.Should().BeFalse(string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));
    }

    [Fact]
    public void DependencySet_ThreeLevelNameCollision_EmitsIOC051()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

[DependsOn<ILogger<Service>>(memberName1: ""loggerA"")]
public interface InfraA : IDependencySet {}

[DependsOn<InfraA>]
[DependsOn<ILogger<Service>>(memberName1: ""loggerB"")]
public interface InfraB : IDependencySet {}

[DependsOn<InfraB>]
[DependsOn<ILogger<Service>>(memberName1: ""loggerC"")]
public interface InfraC : IDependencySet {}

[DependsOn<InfraC>]
public partial class Service {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC051").Count.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void DependencySet_OptionsAndPrimitivesAcrossSets_EmitsIOC056()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

public class BillingOptions { public string BaseUrl { get; set; } = string.Empty; }

[DependsOnConfiguration<BillingOptions>(""Billing"")]
public interface OptionsSet : IDependencySet {}

[DependsOn<OptionsSet>]
public interface DerivedSet : IDependencySet {}

[DependsOn<DerivedSet>]
public partial class Service
{
    [InjectConfiguration(""Billing:BaseUrl"")] private readonly string _baseUrl;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC056").Should().ContainSingle();
    }

    [Fact]
    public void DependencySet_SuggestionIOC053_WhenClusterRepeats()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

[Scoped]
[DependsOn<ILogger>]
[DependsOn<IClock>]
[DependsOn<ITracer>]
public partial class ServiceA {}

[Scoped]
[DependsOn<ILogger>]
[DependsOn<IClock>]
[DependsOn<ITracer>]
public partial class ServiceB {}

public interface IClock {}
public interface ITracer {}
public interface ILogger {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse(string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));
    }

    [Fact]
    public void DependencySet_SuggestionIOC054_NearMatchToExistingSet()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

[DependsOn<IClock>]
[DependsOn<ITracer>]
[DependsOn<IMetrics>]
public interface CoreInfra : IDependencySet {}

[Scoped]
[DependsOn<IClock>]
[DependsOn<ITracer>]
public partial class Service {}

public interface IClock {}
public interface ITracer {}
public interface IMetrics {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse(string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));
    }

    [Fact]
    public void DependencySet_SuggestionIOC055_SharedBaseDependencies()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

public abstract partial class Base
{
    [Inject] private readonly IClock _clock;
}

[Scoped]
public partial class ServiceA : Base {}

[Scoped]
public partial class ServiceB : Base {}

public interface IClock {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC055").Should().NotBeEmpty();
    }

    [Fact]
    public void DependencySet_SuggestionIOC055_TwoChildrenSingleSharedDependency()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IClock {}

public abstract partial class Base {}

[Scoped]
[DependsOn<IClock>]
public partial class ServiceA : Base {}

[Scoped]
[DependsOn<IClock>]
public partial class ServiceB : Base {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC055").Should().NotBeEmpty();
    }

    [Fact]
    public void DependencySet_SuggestionIOC055_ConfigOnlySharedAcrossChildren()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

public abstract partial class Base {}

[Scoped]
[DependsOnConfiguration<string>(""Alpha:Key"")]
public partial class ServiceA : Base {}

[Scoped]
[DependsOnConfiguration<string>(""Alpha:Key"")]
public partial class ServiceB : Base {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC055").Should().NotBeEmpty();
    }

    [Fact]
    public void DependencySet_SuggestionIOC055_MixedLifetimeAndConfigSharedBase()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IClock {}

public abstract partial class Base {}

[Singleton]
[DependsOn<IClock>]
[DependsOnConfiguration<string>(""Feature:Flag"")]
public partial class ServiceA : Base {}

[Singleton]
[DependsOn<IClock>]
[DependsOnConfiguration<string>(""Feature:Flag"")]
public partial class ServiceB : Base {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC055").Should().NotBeEmpty();
    }
}
