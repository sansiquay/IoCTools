namespace IoCTools.Generator.Tests;

using Xunit;

public sealed class Ioc097ProfileMissingMarkerTests
{
    [Fact]
    public void IOC097_does_not_fire_when_profile_implements_marker()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepIn<TestNs.GoodProfile, TestNs.IFoo>]
namespace TestNs
{
    public class GoodProfile : IAutoDepsProfile { }
    public interface IFoo { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Where(d => d.Id == "IOC097").Should().BeEmpty();
    }

    [Fact]
    public void IOC097_does_not_fire_on_AutoDeps_with_valid_profile()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace TestNs
{
    public class GoodProfile : IAutoDepsProfile { }

    [Scoped]
    [AutoDeps<GoodProfile>]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Where(d => d.Id == "IOC097").Should().BeEmpty();
    }

    /// <summary>
    /// C# enforces the <c>where TProfile : IAutoDepsProfile</c> constraint at compile time, so a positive
    /// IOC097 case cannot be constructed from source — the compiler rejects the attempt first (CS0311).
    /// This test documents that behavior; IOC097 remains as a defensive safety-net for edge cases where the
    /// symbol binds but the marker implementation is observed as missing (e.g., metadata/source mismatches).
    /// </summary>
    [Fact]
    public void Csharp_compile_error_prevents_unmarked_profile_from_binding()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepIn<TestNs.UnmarkedProfile, TestNs.IFoo>]
namespace TestNs
{
    public class UnmarkedProfile { } // intentionally missing : IAutoDepsProfile
    public interface IFoo { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "CS0311" || d.Id == "CS0315");
    }
}
