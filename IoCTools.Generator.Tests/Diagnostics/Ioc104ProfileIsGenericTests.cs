namespace IoCTools.Generator.Tests;

using Xunit;

public sealed class Ioc104ProfileIsGenericTests
{
    [Fact]
    public void IOC104_fires_on_generic_profile_in_AutoDepIn()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepIn<TestNs.GenericProfile<int>, TestNs.IFoo>]
namespace TestNs
{
    public class GenericProfile<T> : IAutoDepsProfile { }
    public interface IFoo { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC104");
    }

    [Fact]
    public void IOC104_fires_on_generic_profile_in_AutoDeps_on_class()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace TestNs
{
    public class GenericProfile<T> : IAutoDepsProfile { }

    [Scoped]
    [AutoDeps<GenericProfile<int>>]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC104");
    }

    [Fact]
    public void IOC104_fires_on_generic_profile_in_AutoDepsApply()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApply<TestNs.GenericProfile<int>, TestNs.IBase>]
namespace TestNs
{
    public class GenericProfile<T> : IAutoDepsProfile { }
    public interface IBase { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC104");
    }

    [Fact]
    public void IOC104_fires_on_generic_profile_in_AutoDepsApplyGlob()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApplyGlob<TestNs.GenericProfile<int>>(""Foo.*"")]
namespace TestNs
{
    public class GenericProfile<T> : IAutoDepsProfile { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC104");
    }

    [Fact]
    public void IOC104_does_not_fire_on_nongeneric_profile()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepIn<TestNs.NonGenericProfile, TestNs.IFoo>]
namespace TestNs
{
    public class NonGenericProfile : IAutoDepsProfile { }
    public interface IFoo { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Where(d => d.Id == "IOC104").Should().BeEmpty();
    }
}
