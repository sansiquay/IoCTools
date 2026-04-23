namespace IoCTools.Generator.Tests;

using System.Collections.Generic;
using System.Linq;

using Xunit;

public sealed class Ioc108AutoDepOpenConstraintViolationTests
{
    private static Dictionary<string, string> OptIn() => new()
    {
        ["build_property.IoCToolsAutoDepsDisable"] = "false"
    };

    [Fact]
    public void IOC108_fires_when_service_violates_struct_constraint()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepOpen(typeof(TestNs.IValue<>))]
namespace TestNs
{
    public interface IValue<T> where T : struct { }
    [Scoped]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "IOC108");
        diag.Should().NotBeNull();
        diag!.GetMessage().Should().Contain("struct");
    }

    [Fact]
    public void IOC108_does_not_fire_when_class_constraint_is_satisfied()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepOpen(typeof(TestNs.IRepo<>))]
namespace TestNs
{
    public interface IRepo<T> where T : class { }
    [Scoped]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        result.Diagnostics.Where(d => d.Id == "IOC108").Should().BeEmpty();
    }

    [Fact]
    public void IOC108_fires_when_interface_constraint_is_not_satisfied()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;
[assembly: AutoDepOpen(typeof(TestNs.IHandler<>))]
namespace TestNs
{
    public interface IHandler<T> where T : IDisposable { }
    [Scoped]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "IOC108");
        diag.Should().NotBeNull();
        diag!.GetMessage().Should().Contain("IDisposable");
    }

    [Fact]
    public void IOC108_does_not_fire_when_interface_constraint_is_satisfied()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;
[assembly: AutoDepOpen(typeof(TestNs.IHandler<>))]
namespace TestNs
{
    public interface IHandler<T> where T : IDisposable { }
    [Scoped]
    public partial class Svc : System.IDisposable
    {
        public void Dispose() { }
    }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        result.Diagnostics.Where(d => d.Id == "IOC108").Should().BeEmpty();
    }
}
