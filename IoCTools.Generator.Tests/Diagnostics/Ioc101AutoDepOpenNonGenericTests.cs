namespace IoCTools.Generator.Tests;

using Xunit;

public sealed class Ioc101AutoDepOpenNonGenericTests
{
    [Fact]
    public void IOC101_fires_when_type_is_non_generic()
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
        result.Diagnostics.Should().Contain(d => d.Id == "IOC101");
    }

    [Fact]
    public void IOC101_does_not_fire_on_unbound_generic()
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
        result.Diagnostics.Where(d => d.Id == "IOC101").Should().BeEmpty();
    }

    [Fact]
    public void IOC101_message_contains_type_name()
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
        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "IOC101");
        diag.Should().NotBeNull();
        diag!.GetMessage().Should().Contain("IFoo");
    }
}
