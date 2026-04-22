namespace IoCTools.Generator.Tests;

using System.Collections.Generic;
using System.Linq;

using Xunit;

public sealed class Ioc098DependsOnAutoDepOverlapTests
{
    private static Dictionary<string, string> OptIn() => new()
    {
        ["build_property.IoCToolsAutoDepsDisable"] = "false"
    };

    [Fact]
    public void IOC098_fires_on_bare_DependsOn_overlapping_universal_AutoDep()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<TestNs.IFoo>]
namespace TestNs
{
    public interface IFoo { }
    [Scoped] public partial class FooImpl : IFoo { }

    [Scoped]
    [DependsOn<IFoo>]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "IOC098");
        diag.Should().NotBeNull();
        diag!.GetMessage().Should().Contain("auto-universal");
    }

    [Fact]
    public void IOC098_does_not_fire_on_customized_DependsOn_with_memberName()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<TestNs.IFoo>]
namespace TestNs
{
    public interface IFoo { }
    [Scoped] public partial class FooImpl : IFoo { }

    [Scoped]
    [DependsOn<IFoo>(memberName1: ""_customFoo"")]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        result.Diagnostics.Where(d => d.Id == "IOC098").Should().BeEmpty();
    }

    [Fact]
    public void IOC098_does_not_fire_on_customized_DependsOn_with_external()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<TestNs.IFoo>]
namespace TestNs
{
    public interface IFoo { }
    [Scoped] public partial class FooImpl : IFoo { }

    [Scoped]
    [DependsOn<IFoo>(external: true)]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        result.Diagnostics.Where(d => d.Id == "IOC098").Should().BeEmpty();
    }

    [Fact]
    public void IOC098_fires_with_auto_builtin_source_for_ILogger_overlap()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
namespace TestNs
{
    [Scoped]
    [DependsOn<ILogger<Svc>>]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "IOC098");
        diag.Should().NotBeNull();
        diag!.GetMessage().Should().Contain("auto-builtin:ILogger");
    }

    [Fact]
    public void IOC098_does_not_fire_when_auto_dep_source_is_disabled()
    {
        // With AutoDetectLogger=false, no auto-dep is added for ILogger -> nothing to overlap.
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
namespace TestNs
{
    [Scoped]
    [DependsOn<ILogger<Svc>>]
    public partial class Svc { }
}
";
        var options = new Dictionary<string, string>
        {
            ["build_property.IoCToolsAutoDepsDisable"] = "false",
            ["build_property.IoCToolsAutoDetectLogger"] = "false"
        };
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, options);
        result.Diagnostics.Where(d => d.Id == "IOC098").Should().BeEmpty();
    }
}
