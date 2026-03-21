namespace IoCTools.Generator.Tests;

/// <summary>
///     Integration tests for typeof() diagnostics (IOC090-094).
///     These tests verify that typeof()-based registrations are properly detected
///     and trigger the appropriate diagnostics.
/// </summary>
public sealed class TypeOfRegistrationTests
{
    #region IOC090 - typeof() could use IoCTools attributes

    [Fact]
    public void AddScoped_TypeOf_NoAttributes_EmitsIOC090()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IMyService {}
public class MyServiceImpl : IMyService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(IMyService), typeof(MyServiceImpl));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC090");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void AddSingleton_TypeOf_NoAttributes_EmitsIOC090()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IMyService {}
public class MyServiceImpl : IMyService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IMyService), typeof(MyServiceImpl));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC090");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void AddTransient_TypeOf_NoAttributes_EmitsIOC090()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IMyService {}
public class MyServiceImpl : IMyService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(IMyService), typeof(MyServiceImpl));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC090");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    #endregion

    #region IOC091 - typeof() duplicates IoCTools

    [Fact]
    public void AddScoped_TypeOf_SameLifetime_EmitsIOC091()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IMyService {}

[Scoped]
public partial class MyServiceImpl : IMyService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(IMyService), typeof(MyServiceImpl));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC091");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void AddSingleton_TypeOf_SameLifetime_EmitsIOC091()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IMyService {}

[Singleton]
public partial class MyServiceImpl : IMyService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IMyService), typeof(MyServiceImpl));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC091");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    #endregion

    #region IOC092 - typeof() lifetime mismatch

    [Fact]
    public void AddTransient_TypeOf_ScopedService_EmitsIOC092()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IMyService {}

[Scoped]
public partial class MyServiceImpl : IMyService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(IMyService), typeof(MyServiceImpl));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC092");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void AddSingleton_TypeOf_ScopedService_EmitsIOC092()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IMyService {}

[Scoped]
public partial class MyServiceImpl : IMyService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IMyService), typeof(MyServiceImpl));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC092");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    #endregion

    #region IOC094 - Open generic typeof()

    [Fact]
    public void OpenGeneric_TypeOf_EmitsIOC094()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IRepository {}
public class Repository : IRepository {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC094");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Info);
    }

    #endregion

    #region ServiceDescriptor factory methods

    [Fact]
    public void ServiceDescriptor_Scoped_TypeOf_SameLifetime_EmitsIOC091()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IMyService {}

[Scoped]
public partial class MyServiceImpl : IMyService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.Add(ServiceDescriptor.Scoped(typeof(IMyService), typeof(MyServiceImpl)));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC091");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ServiceDescriptor_Transient_TypeOf_ScopedService_EmitsIOC092()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IMyService {}

[Scoped]
public partial class MyServiceImpl : IMyService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.Add(ServiceDescriptor.Transient(typeof(IMyService), typeof(MyServiceImpl)));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC092");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    #endregion

    #region No false positives

    [Fact]
    public void GenericTypeArgs_StillEmitIOC081()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IMyService {}

[Scoped]
public partial class MyServiceImpl : IMyService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMyService, MyServiceImpl>();
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC081");
        diags.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void TypeOf_SingleArg_NoInterface_NoFalsePositive()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public class MyService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(MyService));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        // Single-arg typeof() should be handled gracefully without crash
        // May or may not emit a diagnostic depending on implementation
        result.CompilationDiagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error && !d.Id.StartsWith("IOC"));
    }

    #endregion
}
