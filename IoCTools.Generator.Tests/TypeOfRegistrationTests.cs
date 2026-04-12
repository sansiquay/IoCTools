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
    public void OpenGeneric_TypeOf_WithRegisterAsAll_SameLifetime_EmitsIOC091()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IRepository<T> where T : class { }

[Scoped]
[RegisterAsAll]
public partial class Repository<T> : IRepository<T> where T : class { }

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
        var duplicates = result.GetDiagnosticsByCode("IOC091");
        var openGenericInfo = result.GetDiagnosticsByCode("IOC094");

        duplicates.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
        openGenericInfo.Should().BeEmpty();
    }

    [Fact]
    public void OpenGeneric_TypeOf_WithRegisterAsAll_LifetimeMismatch_EmitsIOC092()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IRepository<T> where T : class { }

[Scoped]
[RegisterAsAll]
public partial class Repository<T> : IRepository<T> where T : class { }

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IRepository<>), typeof(Repository<>));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var mismatch = result.GetDiagnosticsByCode("IOC092");
        var openGenericInfo = result.GetDiagnosticsByCode("IOC094");

        mismatch.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
        openGenericInfo.Should().BeEmpty();
    }

    [Fact]
    public void OpenGeneric_TypeOf_WithoutIoCToolsIntent_EmitsIOC094()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IRepository<T> where T : class { }
public class Repository<T> : IRepository<T> where T : class { }

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
        var info = result.GetDiagnosticsByCode("IOC094");
        var warning = result.GetDiagnosticsByCode("IOC090");

        info.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Info);
        warning.Should().BeEmpty();
    }

    [Fact]
    public void OpenGeneric_TypeOf_WithRegisterAsAllDirectOnly_EmitsIOC094()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IRepository<T> where T : class { }

[Scoped]
[RegisterAsAll(RegistrationMode.DirectOnly)]
public partial class Repository<T> : IRepository<T> where T : class { }

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
        var info = result.GetDiagnosticsByCode("IOC094");
        var duplicate = result.GetDiagnosticsByCode("IOC091");
        var mismatch = result.GetDiagnosticsByCode("IOC092");

        info.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Info);
        duplicate.Should().BeEmpty();
        mismatch.Should().BeEmpty();
    }

    [Fact]
    public void OpenGeneric_TypeOf_WithQualifiedGlobalSyntax_EmitsIOC094()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IRepository<T> where T : class { }
public class Repository<T> : IRepository<T> where T : class { }

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(global::Test.IRepository<>), typeof(global::Test.Repository<>));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var info = result.GetDiagnosticsByCode("IOC094");
        var warning = result.GetDiagnosticsByCode("IOC090");

        info.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Info);
        warning.Should().BeEmpty();
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
