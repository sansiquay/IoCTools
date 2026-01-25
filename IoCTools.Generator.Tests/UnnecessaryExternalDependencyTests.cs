namespace IoCTools.Generator.Tests;


public class UnnecessaryExternalDependencyTests
{
    [Fact]
    public void ExternalFlag_WithLocalImplementation_ProducesIOC042()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[Scoped]
public class Service : IService { }

[DependsOn<IService>(external: true)]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC042");
        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("IService");
    }

    [Fact]
    public void ExternalFlag_OnFrameworkService_ProducesIOC042()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

[DependsOn<ILogger<Consumer>>(external: true)]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC042");
        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("Microsoft.Extensions.Logging.ILogger");
    }

    [Fact]
    public void ExternalFlag_WhenNoImplementation_Passes()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IMissing { }

[DependsOn<IMissing>(external: true)]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC042").Should().BeEmpty();
    }

    [Fact]
    public void ExternalFlag_WithExternalServiceImplementation_DoesNotWarn()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

[ExternalService]
public class ExternalImpl : IDep { }

public interface IDep { }

[DependsOn<IDep>(external: true)]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC042").Should().BeEmpty();
    }
}
