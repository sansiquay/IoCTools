namespace IoCTools.Generator.Tests;

public class MissedOpportunityTests
{
    [Fact]
    public void ManualConstructor_WithInterfaces_SuggestsDependsOnAndLifetime()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }
public interface ILogger { }

public partial class ManualService
{
    private readonly IRepo _repo;
    private readonly ILogger _logger;

    public ManualService(IRepo repo, ILogger logger)
    {
        _repo = repo;
        _logger = logger;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
        suggestions[0].GetMessage().Should().Contain("ManualService");
        suggestions[0].GetMessage().Should().Contain("IRepo");
        suggestions[0].GetMessage().Should().Contain("ILogger");
    }

    [Fact]
    public void ManualConstructor_WithValueTypes_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public partial class ValueTypeCtor
{
    public ValueTypeCtor(int count) { }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }
}
