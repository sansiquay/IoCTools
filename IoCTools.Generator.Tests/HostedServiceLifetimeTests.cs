namespace IoCTools.Generator.Tests;


public class HostedServiceLifetimeTests
{
    [Fact]
    public void BackgroundService_NoLifetime_NoDiagnostic()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;

namespace Test;

public class Worker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC072").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC070").Should().BeEmpty();
    }

    [Fact]
    public void HostedService_WithLifetime_WarnsIOC072()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;

namespace Test;

[Singleton]
public class Worker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC072").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void HostedService_WithAdditionalInterface_RequiresLifetime()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IWorkerTelemetry {}

public class Worker : BackgroundService, IWorkerTelemetry
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC070").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
        result.GetDiagnosticsByCode("IOC072").Should().BeEmpty();
    }

    /// <summary>
    ///     Open-generic IHostedService implementer with no IoCTools intent attribute must not be
    ///     auto-registered. Emitting <c>services.AddHostedService&lt;Foo&lt;TContext&gt;&gt;()</c>
    ///     would reference a type parameter that does not exist at the call site (CS0246), since
    ///     <c>AddHostedService&lt;T&gt;()</c> in Microsoft.Extensions.DependencyInjection requires
    ///     a closed type.
    /// </summary>
    [Fact]
    public void OpenGeneric_BackgroundService_NoIoCToolsIntent_NotAutoRegistered()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public partial class OpenGenericRelayService<TContext> : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Either of these compiler errors would mean the broken codegen has returned: an unbound
        // type parameter at the registration site, or a method-arg-inference failure on it.
        result.GetDiagnosticsByCode("CS0246").Should().BeEmpty();
        result.GetDiagnosticsByCode("CS0411").Should().BeEmpty();
        result.HasErrors.Should().BeFalse();

        // Registration source may not be emitted when there are no registerable services in the
        // compilation. If it is, it must not contain an AddHostedService call for the open-generic
        // class in any unbound shape.
        if (result.TryGetServiceRegistrationSource(out var registration))
        {
            registration.Content.Should().NotContain("AddHostedService<global::Test.OpenGenericRelayService");
            registration.Content.Should().NotContain("OpenGenericRelayService<TContext>");
            registration.Content.Should().NotContain("OpenGenericRelayService<>");
        }
    }

    /// <summary>
    ///     Open-generic IHostedService implementer with an explicit lifetime attribute is also
    ///     skipped: the explicit attribute does not change the fundamental constraint that
    ///     <c>AddHostedService&lt;T&gt;</c> requires a closed type. IOC073 (Info) surfaces so the
    ///     omission is observable. IOC072 will also fire (the existing "lifetime is implicit for
    ///     hosted services" warning) — that behavior is unchanged here.
    /// </summary>
    [Fact]
    public void OpenGeneric_BackgroundService_WithExplicitLifetime_StillSkippedAndDiagnoses()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

[Singleton]
public partial class OpenGenericExplicitLifetime<TContext> : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("CS0246").Should().BeEmpty();
        result.GetDiagnosticsByCode("CS0411").Should().BeEmpty();
        result.HasErrors.Should().BeFalse();

        var ioc073 = result.GetDiagnosticsByCode("IOC073");
        ioc073.Should().ContainSingle();
        ioc073[0].GetMessage().Should().Contain("OpenGenericExplicitLifetime");
        ioc073[0].Severity.Should().Be(DiagnosticSeverity.Info);

        if (result.TryGetServiceRegistrationSource(out var registration))
        {
            registration.Content.Should().NotContain("AddHostedService<global::Test.OpenGenericExplicitLifetime");
            registration.Content.Should().NotContain("OpenGenericExplicitLifetime<TContext>");
            registration.Content.Should().NotContain("OpenGenericExplicitLifetime<>");
        }
    }

    /// <summary>
    ///     Regression: a closed-generic specialization (subclass that closes the type parameter via
    ///     inheritance) must still auto-register. The open-generic guard targets only the unbound
    ///     base class; closed subclasses are valid <c>AddHostedService&lt;T&gt;</c> arguments.
    /// </summary>
    [Fact]
    public void ClosedGeneric_BackgroundService_StillAutoRegisters()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public partial class GenericRelayBase<TContext> : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class StringContext { }

public partial class StringRelayService : GenericRelayBase<StringContext>
{
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("CS0246").Should().BeEmpty();
        result.GetDiagnosticsByCode("CS0411").Should().BeEmpty();
        result.HasErrors.Should().BeFalse();

        var registration = result.GetRequiredServiceRegistrationSource();
        registration.Content.Should().Contain("services.AddHostedService<global::Test.StringRelayService>");

        // The open-generic base must still be skipped (no AddHostedService for it).
        registration.Content.Should().NotContain("AddHostedService<global::Test.GenericRelayBase");
    }

    /// <summary>
    ///     Regression: ordinary non-generic IHostedService implementers continue to auto-register.
    ///     Guards against an over-broad change that would suppress all <c>AddHostedService</c>
    ///     calls.
    /// </summary>
    [Fact]
    public void NonGeneric_BackgroundService_AutoRegistersAsHostedService()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public partial class PlainBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.HasErrors.Should().BeFalse();
        result.GetDiagnosticsByCode("IOC073").Should().BeEmpty();

        var registration = result.GetRequiredServiceRegistrationSource();
        registration.Content.Should().Contain("services.AddHostedService<global::Test.PlainBackgroundService>");
    }
}

