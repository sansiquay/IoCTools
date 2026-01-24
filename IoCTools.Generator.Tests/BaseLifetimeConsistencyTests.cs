namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

public class BaseLifetimeConsistencyTests
{
    [Fact]
    public void MixedChildLifetimes_ReportsIOC075OnBase()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public abstract class BaseService {}

[Scoped]
public partial class ScopedChild : BaseService {}

[Singleton]
public partial class SingletonChild : BaseService {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC075");
        diags.Select(d => d.Id).Should().Contain("IOC075",
            "available diagnostics: {0}", string.Join(",", result.GeneratorDiagnostics.Select(d => d.Id)));
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
        diags[0].Location.GetLineSpan().StartLinePosition.Line.Should().Be(5);
    }

    [Fact]
    public void ConsistentChildLifetimes_NoDiagnostic()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public abstract class BaseService {}

[Scoped]
public partial class ChildOne : BaseService {}

[Scoped]
public partial class ChildTwo : BaseService {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC075").Should().BeEmpty();
    }
}
