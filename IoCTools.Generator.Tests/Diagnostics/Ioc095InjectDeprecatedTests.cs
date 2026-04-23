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

    [Theory]
    [InlineData("Error", DiagnosticSeverity.Error)]
    [InlineData("Warning", DiagnosticSeverity.Warning)]
    [InlineData("Info", DiagnosticSeverity.Info)]
    [InlineData("Hidden", DiagnosticSeverity.Hidden)]
    public void IoCToolsInjectDeprecationSeverity_maps_string_to_diagnostic_severity(string setting, DiagnosticSeverity expected)
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
            ["build_property.IoCToolsInjectDeprecationSeverity"] = setting
        };
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, options);
        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "IOC095");
        diag.Should().NotBeNull();
        diag!.Severity.Should().Be(expected);
    }

    [Fact]
    public void IOC095_dual_descriptor_open_generic_fallback_still_fires_with_distinct_message()
    {
        // IOC095 in 1.6 ships two descriptors under the same ID: the primary [Inject]
        // deprecation (above) and the legacy open-generic shared-instance fallback from
        // 1.5.x. This regression anchor pins that requesting InstanceSharing.Shared on
        // open-generic interface aliases still emits IOC095 with the legacy message
        // (not the [Inject] deprecation message), proving the two descriptors coexist.
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNs;

public sealed class User { }
public interface IRepository<T> { }
public interface IValidator<T> { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class GenericDataService<T> : IRepository<T>, IValidator<T>
{
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "IOC095");
        diag.Should().NotBeNull();
        // The legacy descriptor's message mentions open-generic / InstanceSharing /
        // fallback — distinct vocabulary from the primary descriptor which references
        // "[Inject]" and "[DependsOn<T>]". Either "open generic" or "Shared" being
        // present confirms the right descriptor fired.
        var message = diag!.GetMessage();
        (message.Contains("open generic", System.StringComparison.OrdinalIgnoreCase) ||
         message.Contains("Shared", System.StringComparison.Ordinal) ||
         message.Contains("fallback", System.StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue($"legacy IOC095 fallback message expected, got: {message}");
        message.Should().NotContain("[Inject]");
    }
}
