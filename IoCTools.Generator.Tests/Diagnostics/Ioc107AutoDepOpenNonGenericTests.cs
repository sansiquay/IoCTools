namespace IoCTools.Generator.Tests;

using Xunit;

public sealed class Ioc107AutoDepOpenNonGenericTests
{
    [Fact]
    public void IOC107_fires_when_type_is_non_generic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepOpen(typeof(TestNs.IFoo))]
namespace TestNs
{
    public interface IFoo { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC107");
    }

    [Fact]
    public void IOC107_does_not_fire_on_unbound_generic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepOpen(typeof(TestNs.IFoo<>))]
namespace TestNs
{
    public interface IFoo<T> { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Where(d => d.Id == "IOC107").Should().BeEmpty();
    }

    [Fact]
    public void IOC107_message_contains_type_name()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepOpen(typeof(TestNs.IFoo))]
namespace TestNs
{
    public interface IFoo { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "IOC107");
        diag.Should().NotBeNull();
        diag!.GetMessage().Should().Contain("IFoo");
    }
}
