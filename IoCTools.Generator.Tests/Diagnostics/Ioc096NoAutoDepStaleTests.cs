namespace IoCTools.Generator.Tests;

using System.Collections.Generic;
using System.Linq;

using Xunit;

public sealed class Ioc096NoAutoDepStaleTests
{
    private static Dictionary<string, string> OptIn() => new()
    {
        ["build_property.IoCToolsAutoDepsDisable"] = "false"
    };

    [Fact]
    public void IOC096_fires_on_stale_NoAutoDep()
    {
        // NoAutoDep<IUnrelated> but nothing ever adds IUnrelated to the auto-dep set.
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace TestNs
{
    public interface IUnrelated { }

    [Scoped]
    [NoAutoDep<IUnrelated>]
    public partial class Svc { }
}
";
        var options = new Dictionary<string, string>
        {
            ["build_property.IoCToolsAutoDepsDisable"] = "false",
            // Disable the built-in ILogger auto-dep to keep the pre-opt-out set empty.
            ["build_property.IoCToolsAutoDetectLogger"] = "false"
        };
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, options);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC096");
    }

    [Fact]
    public void IOC096_does_not_fire_when_NoAutoDep_actually_suppresses_an_auto_dep()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<TestNs.IFoo>]
namespace TestNs
{
    public interface IFoo { }
    [Scoped] public partial class FooImpl : IFoo { }

    [Scoped]
    [NoAutoDep<IFoo>]
    public partial class Svc { }
}
";
        var options = new Dictionary<string, string>
        {
            ["build_property.IoCToolsAutoDepsDisable"] = "false",
            ["build_property.IoCToolsAutoDetectLogger"] = "false"
        };
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, options);
        result.Diagnostics.Where(d => d.Id == "IOC096").Should().BeEmpty();
    }

    [Fact]
    public void IOC096_fires_on_stale_NoAutoDepOpen()
    {
        // NoAutoDepOpen(typeof(IRepo<>)) but no AutoDepOpen introduces IRepo<> entries.
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace TestNs
{
    public interface IRepo<T> { }

    [Scoped]
    [NoAutoDepOpen(typeof(IRepo<>))]
    public partial class Svc { }
}
";
        var options = new Dictionary<string, string>
        {
            ["build_property.IoCToolsAutoDepsDisable"] = "false",
            ["build_property.IoCToolsAutoDetectLogger"] = "false"
        };
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, options);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC096");
    }

    [Fact]
    public void IOC096_does_not_fire_when_NoAutoDepOpen_suppresses_builtin_ILogger()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
namespace TestNs
{
    [Scoped]
    [NoAutoDepOpen(typeof(ILogger<>))]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        result.Diagnostics.Where(d => d.Id == "IOC096").Should().BeEmpty();
    }
}
