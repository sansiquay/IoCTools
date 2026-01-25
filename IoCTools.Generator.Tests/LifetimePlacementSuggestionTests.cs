namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

public class LifetimePlacementSuggestionTests
{
    [Fact]
    public void DerivedServicesSharingBase_EmitSharedBaseLifetimeSuggestion()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepositoryA { }
public interface IRepositoryB { }

public abstract class RepositoryBase<T> { }

public sealed class RepoA : RepositoryBase<int>, IRepositoryA { }
public sealed class RepoB : RepositoryBase<int>, IRepositoryB { }

[Singleton]
[
    DependsOn<IRepositoryA>
]
public sealed partial class ConsumerA
{
}

[Singleton]
[
    DependsOn<IRepositoryB>
]
public sealed partial class ConsumerB
{
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var sharedBaseDiagnostics = result.GetDiagnosticsByCode("IOC058");
        sharedBaseDiagnostics.Should().ContainSingle();
        sharedBaseDiagnostics[0].Severity.Should().Be(DiagnosticSeverity.Info);
        sharedBaseDiagnostics[0].GetMessage().Should().Contain("RepositoryBase");
        sharedBaseDiagnostics[0].GetMessage().Should().Contain("Scoped");

        // Until the base class is annotated, consumers still see missing registration warnings
        result.GetDiagnosticsByCode("IOC002").Should().NotBeEmpty();
    }
}
