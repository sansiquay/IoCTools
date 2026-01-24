namespace IoCTools.Generator.Tests;

public class ManualRegistrationSuggestionTests
{
    [Fact]
    public void ManualAddScopedWithoutAttributes_ProducesIOC086()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IService { }
public class Service : IService { }

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped<IService, Service>();
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var diagnostics = result.GetDiagnosticsByCode("IOC086");
        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage().Should().Contain("Service");
    }
}
