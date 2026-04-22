namespace IoCTools.Generator.Tests;

using System.Collections.Generic;
using System.Linq;

using Xunit;

public sealed class Ioc105RedundantProfileAttachmentTests
{
    private static Dictionary<string, string> OptIn() => new()
    {
        ["build_property.IoCToolsAutoDepsDisable"] = "false"
    };

    [Fact]
    public void IOC105_fires_when_service_attached_via_AutoDeps_and_AutoDepsApplyGlob()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApplyGlob<TestNs.P>(""TestNs*"")]
namespace TestNs
{
    public class P : IAutoDepsProfile { }

    [Scoped]
    [AutoDeps<P>]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        result.Diagnostics.Should().Contain(d => d.Id == "IOC105");
    }

    [Fact]
    public void IOC105_fires_when_service_attached_via_AutoDepsApply_and_AutoDeps()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApply<TestNs.P, TestNs.IBase>]
namespace TestNs
{
    public class P : IAutoDepsProfile { }
    public interface IBase { }

    [Scoped]
    [AutoDeps<P>]
    public partial class Svc : IBase { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        result.Diagnostics.Should().Contain(d => d.Id == "IOC105");
    }

    [Fact]
    public void IOC105_does_not_fire_when_service_attached_via_single_path()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace TestNs
{
    public class P : IAutoDepsProfile { }

    [Scoped]
    [AutoDeps<P>]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        result.Diagnostics.Where(d => d.Id == "IOC105").Should().BeEmpty();
    }

    [Fact]
    public void IOC105_does_not_fire_when_different_profiles_attached()
    {
        // Two attachments, two different profiles — not a redundancy.
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApplyGlob<TestNs.P2>(""TestNs.*"")]
namespace TestNs
{
    public class P1 : IAutoDepsProfile { }
    public class P2 : IAutoDepsProfile { }

    [Scoped]
    [AutoDeps<P1>]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        result.Diagnostics.Where(d => d.Id == "IOC105").Should().BeEmpty();
    }
}
