namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

public class DependencySetTests
{
    [Fact]
    public void DependencySet_FlattensDependenciesAndConfiguration()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Test;

[DependsOn<ILogger<BillingService>>]
[DependsOnConfiguration<string>(""Billing:BaseUrl"")]
public interface BillingInfra : IDependencySet {}

[Scoped]
[DependsOn<BillingInfra>]
public partial class BillingService : IService
{
}

public interface IService {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse(string.Join(" | ", result.CompilationDiagnostics));

        var ctor = result.GetConstructorSourceText("BillingService");
        ctor.Should().Contain("ILogger<BillingService> logger");
        ctor.Should().Contain("private readonly string _billingBaseUrl;");
        ctor.Should().Contain("_billingBaseUrl = configuration.GetValue<string>(\"Billing:BaseUrl\")");
    }

    [Fact]
    public void DependencySet_Cycle_EmitsIOC050()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

[DependsOn<InfraB>]
public interface InfraA : IDependencySet {}

[DependsOn<InfraA>]
public interface InfraB : IDependencySet {}

[Scoped]
[DependsOn<InfraA>]
public partial class Consumer {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC050").Should().NotBeEmpty();
    }

    [Fact]
    public void DependencySet_WithMembers_EmitsIOC049()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface Infra : IDependencySet
{
    string Name { get; }
}

[DependsOn<Infra>]
public partial class Consumer {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC049").Should().ContainSingle();
    }

    [Fact]
    public void DependencySet_WithLifetimeAttribute_EmitsIOC052()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

[Scoped]
public interface Infra : IDependencySet {}

[DependsOn<Infra>]
public partial class Consumer {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC052").Should().ContainSingle();
    }

    [Fact]
    public void DependencySet_NameCollisionAcrossSets_EmitsIOC051()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

[DependsOn<ILogger<Service>>(memberNames: new[] { ""firstLogger"" })]
public class InfraA : IDependencySet {}

[DependsOn<ILogger<Service>>(memberNames: new[] { ""secondLogger"" })]
public class InfraB : IDependencySet {}

[DependsOn<InfraA>]
[DependsOn<InfraB>]
public partial class Service {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var infraA = result.Compilation.GetTypeByMetadataName("Test.InfraA");
        var attr = infraA!.GetAttributes().First(a => a.AttributeClass?.Name.StartsWith("DependsOn") == true);

        string FormatConst(TypedConstant c)
        {
            return c.Kind == TypedConstantKind.Array
                ? string.Join("|", c.Values.Select(v => v.Value?.ToString() ?? "<null>"))
                : c.Value?.ToString() ?? "<null>";
        }

        var ctorDescription = string.Join(";", attr.ConstructorArguments.Select(a => $"{a.Kind}:{FormatConst(a)}"));
        var ctorMemberNames = attr.ConstructorArguments.Last().Values.Select(v => v.Value?.ToString() ?? "<null>")
            .ToArray();
        ctorMemberNames.Should().Contain("firstLogger",
            $"CtorArgs:{ctorDescription}; named:{string.Join(",", attr.NamedArguments.Select(n => n.Key + ":" + n.Value))}");

        var ctor = result.GetConstructorSourceText("Service");
        ctor.Should().Contain("firstLogger");

        var allDiags = string.Join(" | ", result.Diagnostics.Select(d => $"{d.Id}:{d.GetMessage()}"));
        result.GetDiagnosticsByCode("IOC051").Should().ContainSingle($"Diagnostics: {allDiags}");
    }

    [Fact]
    public void DependencySet_LifetimeValidation_FlowsThroughExpansion()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

[Scoped]
public partial class ScopedDep {}

[DependsOn<ScopedDep>]
public interface Infra : IDependencySet {}

[Singleton]
[DependsOn<Infra>]
public partial class Consumer {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC012").Should().ContainSingle();
    }

    [Fact]
    public void DependencySet_NestedSets_FlattensRecursively()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IClock { }

[DependsOn<ILogger<Service>>]
public interface CoreInfra : IDependencySet {}

[DependsOn<CoreInfra>]
[DependsOn<IClock>]
public interface FeatureInfra : IDependencySet {}

[Scoped]
[DependsOn<FeatureInfra>]
public partial class Service {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse(string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));

        var ctor = result.GetConstructorSourceText("Service");
        ctor.Should().Contain("ILogger<Service> logger");
        ctor.Should().Contain("IClock clock");
    }

    [Fact]
    public void DependencySet_ExternalDependencies_SkipMissingImplementationDiagnostics()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IClock { }

[DependsOn<IClock>(external: true)]
public interface Infra : IDependencySet {}

[Scoped]
[DependsOn<Infra>]
public partial class Service {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC001").Should().BeEmpty();

        var ctor = result.GetConstructorSourceText("Service");
        ctor.Should().Contain("IClock clock");
    }

    [Fact]
    public void DependencySet_NestedConfiguration_GeneratesFields()
    {
        var source = @"
using IoCTools.Abstractions;
using IoCTools.Abstractions.Annotations;

namespace Test;

[DependsOnConfiguration<string>(""Alpha:Key"")]
public interface BaseConfig : IDependencySet {}

[DependsOn<BaseConfig>]
public interface FeatureConfig : IDependencySet {}

[Scoped]
[DependsOn<FeatureConfig>]
public partial class Service {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse(string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));

        var ctor = result.GetConstructorSourceText("Service");
        ctor.Should().Contain("string _alphaKey");
        ctor.Should().Contain("_alphaKey = configuration.GetValue<string>(\"Alpha:Key\")");
    }
}
