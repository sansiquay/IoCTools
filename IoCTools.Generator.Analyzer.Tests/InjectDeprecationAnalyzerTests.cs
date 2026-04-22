namespace IoCTools.Generator.Analyzer.Tests;

using System.Threading.Tasks;
using FluentAssertions;
using IoCTools.Generator.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

/// <summary>
///     Verifies that <see cref="InjectDeprecationAnalyzer" /> emits IOC095 for every
///     <c>[Inject]</c> field it encounters. The analyzer is the quick-fix anchor —
///     without it the code-fix provider has no diagnostic to bind to.
/// </summary>
public sealed class InjectDeprecationAnalyzerTests
{
    private const string InjectStub = @"
namespace IoCTools.Abstractions.Annotations
{
    public sealed class InjectAttribute : System.Attribute { }
    public sealed class ExternalServiceAttribute : System.Attribute { }
}
";

    private static AnalyzerTest<XUnitVerifier> BuildAnalyzerTest(string testSource)
    {
        var test = new CSharpAnalyzerTest<InjectDeprecationAnalyzer, XUnitVerifier>
        {
            TestState =
            {
                Sources = { InjectStub, testSource },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test;
    }

    [Fact]
    public async Task Emits_IOC095_on_inject_field()
    {
        var source = @"
public interface IFoo {}

public partial class Svc
{
    [IoCTools.Abstractions.Annotations.Inject]
    private readonly IFoo {|IOC095:_foo|} = null!;
}
";
        var test = BuildAnalyzerTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task Does_not_emit_on_untouched_field()
    {
        var source = @"
public interface IFoo {}

public partial class Svc
{
    private readonly IFoo _foo = null!;
}
";
        var test = BuildAnalyzerTest(source);
        await test.RunAsync();
    }

    [Fact]
    public async Task Emits_once_per_inject_field()
    {
        var source = @"
public interface IFoo {}
public interface IBar {}

public partial class Svc
{
    [IoCTools.Abstractions.Annotations.Inject]
    private readonly IFoo {|IOC095:_foo|} = null!;

    [IoCTools.Abstractions.Annotations.Inject]
    private readonly IBar {|IOC095:_bar|} = null!;
}
";
        var test = BuildAnalyzerTest(source);
        await test.RunAsync();
    }
}
