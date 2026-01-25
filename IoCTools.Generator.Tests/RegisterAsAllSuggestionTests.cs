namespace IoCTools.Generator.Tests;


public class RegisterAsAllSuggestionTests
{
    [Fact]
    public void MultiInterfaceWithLifetime_SuggestsRegisterAsAll()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IOne { }
public interface ITwo { }
public interface IThree { }

[Scoped]
public partial class Multi : IOne, ITwo, IThree { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var suggestions = result.GetDiagnosticsByCode("IOC074");
        suggestions.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Info);
    }

    [Fact]
    public void MultiInterfaceWithRegisterAsAll_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IOne { }
public interface ITwo { }

[Scoped]
[RegisterAsAll]
public partial class Multi : IOne, ITwo { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC074").Should().BeEmpty();
    }
}
