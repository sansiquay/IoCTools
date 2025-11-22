using IoCTools.Generator.Tests;

public class ManualRegistrationOverlapTests
{
    [Fact]
    public void DuplicateManualRegistration_SameLifetime_TriggersIOC050()
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
        var diags = result.GetDiagnosticsByCode("IOC050");
        diags.Should().ContainSingle();
    }

    [Fact]
    public void DuplicateManualRegistration_InterfacePair_SameLifetime_TriggersIOC050()
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
        var diags = result.GetDiagnosticsByCode("IOC050");
        diags.Should().ContainSingle();
    }

    [Fact]
    public void ManualRegistration_LifetimeMismatch_TriggersIOC051()
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
        var diags = result.GetDiagnosticsByCode("IOC051");
        diags.Should().ContainSingle();
    }

    [Fact]
    public void ManualRegistration_InterfaceMismatch_TriggersIOC051()
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
        var diags = result.GetDiagnosticsByCode("IOC051");
        diags.Should().ContainSingle();
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
        result.GetDiagnosticsByCode("IOC050").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC051").Should().BeEmpty();
    }
}
