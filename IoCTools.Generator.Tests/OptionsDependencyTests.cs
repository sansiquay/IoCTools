namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

public class OptionsDependencyTests
{
    [Fact]
    public void OptionsDependency_DependsOn_ProducesIOC043()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Options;

namespace Test;

public class ServiceOptions { }

[DependsOn<IOptions<ServiceOptions>>]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC043");
        diags.Should().ContainSingle();
        diags[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void OptionsDependency_InjectField_ProducesIOC043()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Options;

namespace Test;

public class ServiceOptions { }

public partial class Consumer
{
    [Inject] private readonly IOptions<ServiceOptions> _opts;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC043").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void FrameworkService_NoExternalNeeded_NoIOC042()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

[DependsOn<ILogger<Consumer>>]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC042").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC042").Should().BeEmpty();
        result.CompilationDiagnostics.Where(d => d.Severity != DiagnosticSeverity.Hidden).Should().BeEmpty();
    }

    [Fact]
    public void FrameworkService_ExternalTrue_WarnsIOC042()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;

[DependsOn<IConfiguration>(external: true)]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC042").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }
}
