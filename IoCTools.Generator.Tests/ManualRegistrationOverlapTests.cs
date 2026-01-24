using IoCTools.Generator.Tests;
using Microsoft.CodeAnalysis;

public class ManualRegistrationOverlapTests
{
    [Fact]
    public void DuplicateManualRegistration_SameLifetime_TriggersIOC081()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

[Scoped]
public partial class FooService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped<FooService>();
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC081");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void DuplicateManualRegistration_InterfacePair_SameLifetime_TriggersIOC081()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IFoo {}

[Scoped]
public partial class FooService : IFoo {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped<IFoo, FooService>();
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC081");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ManualRegistration_LifetimeMismatch_TriggersIOC082()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

[Scoped]
public partial class FooService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddSingleton<FooService>();
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC082");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void ManualRegistration_InterfaceMismatch_TriggersIOC082()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IFoo {}

[Scoped]
public partial class FooService : IFoo {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFoo, FooService>();
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC082");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void ManualRegistration_InterfaceFactory_SameLifetime_TriggersIOC081()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IFoo {}

[Scoped]
public partial class FooService : IFoo {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped<IFoo>(_ => new FooService());
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC081");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ManualRegistration_InterfaceFactory_MismatchedLifetime_TriggersIOC082()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IFoo {}

[Scoped]
public partial class FooService : IFoo {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFoo>(_ => new FooService());
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC082");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void ManualRegistration_NoIoCToolsIntent_NoDiagnostic()
    {
        var source = @"
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public class FooService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped<FooService>();
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC081").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC082").Should().BeEmpty();
    }
}
