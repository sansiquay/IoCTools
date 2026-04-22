namespace IoCTools.Generator.Tests;

using Xunit;

public sealed class Ioc103AutoDepsApplyGlobInvalidTests
{
    [Fact]
    public void IOC103_fires_on_unterminated_character_class()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApplyGlob<TestNs.P>(""[unterminated"")]
namespace TestNs
{
    public class P : IAutoDepsProfile { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC103");
    }

    [Fact]
    public void IOC103_fires_on_empty_pattern()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApplyGlob<TestNs.P>("""")]
namespace TestNs
{
    public class P : IAutoDepsProfile { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC103");
    }

    [Fact]
    public void IOC103_does_not_fire_on_valid_pattern()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApplyGlob<TestNs.P>(""MyApp.Services.*"")]
namespace TestNs
{
    public class P : IAutoDepsProfile { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Where(d => d.Id == "IOC103").Should().BeEmpty();
    }

    [Fact]
    public void IOC103_does_not_fire_on_valid_pattern_with_character_class()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApplyGlob<TestNs.P>(""MyApp.[AB]*"")]
namespace TestNs
{
    public class P : IAutoDepsProfile { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Where(d => d.Id == "IOC103").Should().BeEmpty();
    }
}
