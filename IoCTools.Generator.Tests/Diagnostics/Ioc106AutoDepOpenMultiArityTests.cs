namespace IoCTools.Generator.Tests;

using Xunit;

public sealed class Ioc106AutoDepOpenMultiArityTests
{
    [Fact]
    public void IOC106_fires_on_multi_arity_unbound_generic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;
[assembly: AutoDepOpen(typeof(IDictionary<,>))]
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC106");
    }

    [Fact]
    public void IOC106_does_not_fire_on_single_arity_unbound_generic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;
[assembly: AutoDepOpen(typeof(IEnumerable<>))]
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Where(d => d.Id == "IOC106").Should().BeEmpty();
    }

    [Fact]
    public void IOC106_message_contains_type_and_arity()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;
[assembly: AutoDepOpen(typeof(IDictionary<,>))]
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "IOC106");
        diag.Should().NotBeNull();
        diag!.GetMessage().Should().Contain("IDictionary").And.Contain("2");
    }
}
