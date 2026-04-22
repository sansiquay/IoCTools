namespace IoCTools.Generator.Tests;

using System.Collections.Generic;
using System.Linq;

using Xunit;

public sealed class Ioc099AutoDepsApplyStaleTests
{
    [Fact]
    public void IOC099_fires_when_AutoDepsApply_matches_zero_services()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApply<TestNs.P, TestNs.IUnused>]
namespace TestNs
{
    public class P : IAutoDepsProfile { }
    public interface IUnused { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC099");
    }

    [Fact]
    public void IOC099_fires_when_glob_matches_zero_services()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApplyGlob<TestNs.P>(""NoMatch*"")]
namespace TestNs
{
    public class P : IAutoDepsProfile { }
    [Scoped]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC099");
    }

    [Fact]
    public void IOC099_does_not_fire_when_AutoDepsApply_matches()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApply<TestNs.P, TestNs.IBase>]
namespace TestNs
{
    public class P : IAutoDepsProfile { }
    public interface IBase { }
    [Scoped]
    public partial class Svc : IBase { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Where(d => d.Id == "IOC099").Should().BeEmpty();
    }

    [Fact]
    public void IOC099_does_not_fire_when_glob_matches()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApplyGlob<TestNs.P>(""TestNs*"")]
namespace TestNs
{
    public class P : IAutoDepsProfile { }
    [Scoped]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Where(d => d.Id == "IOC099").Should().BeEmpty();
    }

    [Fact]
    public void IOC099_does_not_fire_for_invalid_pattern_IOC103_owns_that()
    {
        // Invalid pattern should surface as IOC103, not IOC099.
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApplyGlob<TestNs.P>(""[unterminated"")]
namespace TestNs
{
    public class P : IAutoDepsProfile { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Where(d => d.Id == "IOC099").Should().BeEmpty();
        result.Diagnostics.Where(d => d.Id == "IOC103").Should().NotBeEmpty();
    }
}
