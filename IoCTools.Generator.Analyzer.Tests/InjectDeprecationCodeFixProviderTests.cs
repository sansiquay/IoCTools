namespace IoCTools.Generator.Analyzer.Tests;

using System.Threading.Tasks;
using IoCTools.Generator.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

/// <summary>
///     End-to-end verification of the quick-fix path: analyzer emits IOC095,
///     the code-fix provider rewrites the class through <c>InjectMigrationRewriter</c>,
///     and the resulting document matches the expected source.
/// </summary>
/// <remarks>
///     Without a resolved auto-dep set the rewriter's Branch A (delete) never fires;
///     every field is routed through Branch B/C (convert to <c>[DependsOn&lt;T&gt;]</c>).
///     The IDE code-fix has no way to know which types are in the auto-dep set without
///     running the full generator pipeline — we accept conservative convert-everything
///     behavior in the IDE path. The <c>migrate-inject</c> CLI command exercises the
///     full pipeline and produces the delete branch for covered types.
/// </remarks>
public sealed class InjectDeprecationCodeFixProviderTests
{
    private const string InjectStub = @"
namespace IoCTools.Abstractions.Annotations
{
    public sealed class InjectAttribute : System.Attribute { }
    public sealed class ExternalServiceAttribute : System.Attribute { }
    public sealed class DependsOnAttribute<T1> : System.Attribute
    {
        public DependsOnAttribute(string? memberName1 = null, bool external = false) { }
    }
    public sealed class DependsOnAttribute<T1, T2> : System.Attribute
    {
        public DependsOnAttribute(string? memberName1 = null, string? memberName2 = null, bool external = false) { }
    }
}
";

    private static CSharpCodeFixTest<InjectDeprecationAnalyzer, InjectDeprecationCodeFixProvider, XUnitVerifier>
        BuildFixTest(string testSource, string fixedSource)
    {
        return new CSharpCodeFixTest<InjectDeprecationAnalyzer, InjectDeprecationCodeFixProvider, XUnitVerifier>
        {
            TestState =
            {
                Sources = { InjectStub, testSource },
            },
            FixedState =
            {
                Sources = { InjectStub, fixedSource },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
    }

    [Fact]
    public async Task Convert_bare_field_to_dependson()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

public interface IFoo {}

public partial class Svc
{
    [Inject]
    private readonly IFoo {|IOC095:_foo|} = null!;
}
";
        var fixedSource = @"
using IoCTools.Abstractions.Annotations;

public interface IFoo {}

[DependsOn<IFoo>]
public partial class Svc
{
}
";
        var test = BuildFixTest(source, fixedSource);
        test.NumberOfIncrementalIterations = 1;
        test.NumberOfFixAllIterations = 1;
        await test.RunAsync();
    }

    [Fact]
    public async Task Convert_custom_named_field_emits_memberName()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

public interface IFoo {}

public partial class Svc
{
    [Inject]
    private readonly IFoo {|IOC095:_customName|} = null!;
}
";
        var fixedSource = @"
using IoCTools.Abstractions.Annotations;

public interface IFoo {}

[DependsOn<IFoo>(memberName1: ""_customName"")]
public partial class Svc
{
}
";
        var test = BuildFixTest(source, fixedSource);
        test.NumberOfIncrementalIterations = 1;
        test.NumberOfFixAllIterations = 1;
        await test.RunAsync();
    }

    [Fact]
    public async Task Preserve_external_flag_on_dependson()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

public interface IFoo {}

public partial class Svc
{
    [Inject]
    [ExternalService]
    private readonly IFoo {|IOC095:_foo|} = null!;
}
";
        var fixedSource = @"
using IoCTools.Abstractions.Annotations;

public interface IFoo {}

[DependsOn<IFoo>(external: true)]
public partial class Svc
{
}
";
        var test = BuildFixTest(source, fixedSource);
        test.NumberOfIncrementalIterations = 1;
        test.NumberOfFixAllIterations = 1;
        await test.RunAsync();
    }

    [Fact]
    public async Task Coalesce_two_fields_into_single_dependson()
    {
        // Fix-all on both diagnostics at once -- both fields coalesce into a single
        // [DependsOn<IFoo, IBar>] attribute.
        var source = @"
using IoCTools.Abstractions.Annotations;

public interface IFoo {}
public interface IBar {}

public partial class Svc
{
    [Inject]
    private readonly IFoo {|IOC095:_foo|} = null!;

    [Inject]
    private readonly IBar {|IOC095:_bar|} = null!;
}
";
        var fixedSource = @"
using IoCTools.Abstractions.Annotations;

public interface IFoo {}
public interface IBar {}

[DependsOn<IFoo, IBar>]
public partial class Svc
{
}
";
        var test = BuildFixTest(source, fixedSource);
        // A single fix invocation migrates ALL [Inject] fields on the class (the provider
        // calls InjectMigrationRewriter over the full field set, not just the clicked one).
        // So after one iteration both IOC095 diagnostics are gone; iteration count == 1.
        test.NumberOfIncrementalIterations = 1;
        test.NumberOfFixAllIterations = 1;
        await test.RunAsync();
    }
}
