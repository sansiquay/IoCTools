namespace IoCTools.Generator.Tests;

public class FrameworkDependencySkipTests
{
    [Fact]
    public void ILoggerDependency_DoesNotProduceIOC001()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

[DependsOn<ILogger<Service>>]
public partial class Service {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC001").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC002").Should().BeEmpty();
    }
}

