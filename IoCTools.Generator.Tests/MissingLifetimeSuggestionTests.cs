namespace IoCTools.Generator.Tests;

public class MissingLifetimeSuggestionTests
{
    [Fact]
    public void RegisterAs_WithoutLifetime_ProducesIOC069()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

[RegisterAs<IService>]
public partial class Service : IService { }

public interface IService { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var diagnostics = result.GetDiagnosticsByCode("IOC069");
        diagnostics.Should().ContainSingle();
    }

    [Fact]
    public void DependsOn_WithoutLifetime_ProducesIOC070()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

[DependsOn<IService>]
public partial class Service
{
}

public interface IService { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC070").Should().ContainSingle();
    }

    [Fact]
    public void ConditionalService_WithoutLifetime_ProducesIOC071()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

[ConditionalService(""FeatureX"")]
public partial class Service
{
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC071").Should().ContainSingle();
    }
}
