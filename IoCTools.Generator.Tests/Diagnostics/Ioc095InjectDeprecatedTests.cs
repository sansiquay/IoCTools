namespace IoCTools.Generator.Tests;

using Xunit;

public sealed class Ioc095InjectDeprecatedTests
{
    [Fact]
    public void IOC095_fires_on_Inject_field()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace TestNs
{
    public interface IFoo { }
    [Scoped]
    public partial class Svc { [Inject] private readonly IFoo _foo; }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC095");
    }

    [Fact]
    public void IOC095_does_not_fire_on_InjectConfiguration_field()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;
namespace TestNs
{
    [Scoped]
    public partial class Svc { [InjectConfiguration(""Section"")] private readonly IConfiguration _cfg; }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Where(d => d.Id == "IOC095").Should().BeEmpty();
    }

    [Fact]
    public void IOC095_fires_with_field_name_and_type_in_message()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace TestNs
{
    public interface IFoo { }
    [Scoped]
    public partial class Svc { [Inject] private readonly IFoo _foo; }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "IOC095");
        diag.Should().NotBeNull();
        diag!.GetMessage().Should().Contain("_foo").And.Contain("IFoo");
    }

    [Fact]
    public void IoCToolsInjectDeprecationSeverity_Info_reduces_severity()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace TestNs
{
    public interface IFoo { }
    [Scoped]
    public partial class Svc { [Inject] private readonly IFoo _foo; }
}
";
        var options = new Dictionary<string, string>
        {
            ["build_property.IoCToolsInjectDeprecationSeverity"] = "Info"
        };
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, options);
        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "IOC095");
        diag.Should().NotBeNull();
        diag!.Severity.Should().Be(DiagnosticSeverity.Info);
    }
}
