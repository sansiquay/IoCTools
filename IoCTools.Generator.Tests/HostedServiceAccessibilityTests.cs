namespace IoCTools.Generator.Tests;

/// <summary>
///     Coverage for the IOC066 / accessibility-skip path.
///
///     <para>
///         The generated registration extension lives in a separate type under a
///         <c>*.Extensions.Generated</c> namespace inside the same assembly. The extension can
///         therefore reference any <c>internal</c>-or-better type, but anything stricter on any
///         link of the containing-type chain (private nested, protected nested, public-nested-in-
///         private-outer) is unreachable from the emission site and would produce CS0122 if the
///         generator emitted <c>services.AddHostedService&lt;TImpl&gt;()</c> for it.
///     </para>
///     <para>
///         The selector skips emission and surfaces IOC066 (Info by default; escalated to
///         Warning when the user opted in via an explicit lifetime attribute). A render-time
///         backstop in <see cref="ServiceRegistrationGenerator" /> additionally suppresses the
///         emission if any future code path bypasses the selector guard.
///     </para>
/// </summary>
public class HostedServiceAccessibilityTests
{
    /// <summary>
    ///     Baseline: a private nested BackgroundService with no IoCTools intent attribute is
    ///     skipped silently for registration purposes (no AddHostedService emitted) and the
    ///     compiler does not see CS0122. IOC066 surfaces at Info severity.
    /// </summary>
    [Fact]
    public void PrivateNested_BackgroundService_NoIntent_NotAutoRegistered_NoCS0122()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public class Outer
{
    private class HiddenWorker : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("CS0122").Should().BeEmpty();
        result.HasErrors.Should().BeFalse();

        if (result.TryGetServiceRegistrationSource(out var registration))
            registration.Content.Should().NotContain("HiddenWorker");

        var ioc066 = result.GetDiagnosticsByCode("IOC066");
        ioc066.Should().ContainSingle();
        ioc066[0].Severity.Should().Be(DiagnosticSeverity.Info);
        ioc066[0].GetMessage().Should().Contain("HiddenWorker");
    }

    /// <summary>
    ///     Regression-prevention: a top-level <c>internal</c> BackgroundService still
    ///     auto-registers via <c>services.AddHostedService&lt;T&gt;()</c>.
    /// </summary>
    [Fact]
    public void InternalTopLevel_BackgroundService_AutoRegistered()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

internal class InternalWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.HasErrors.Should().BeFalse();
        result.GetDiagnosticsByCode("IOC066").Should().BeEmpty();

        var registration = result.GetRequiredServiceRegistrationSource();
        registration.Content.Should().Contain("services.AddHostedService<global::Test.InternalWorker>");
    }

    /// <summary>
    ///     Regression-prevention: a top-level <c>public</c> BackgroundService still
    ///     auto-registers via <c>services.AddHostedService&lt;T&gt;()</c>.
    /// </summary>
    [Fact]
    public void PublicTopLevel_BackgroundService_AutoRegistered()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public class PublicWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.HasErrors.Should().BeFalse();
        result.GetDiagnosticsByCode("IOC066").Should().BeEmpty();

        var registration = result.GetRequiredServiceRegistrationSource();
        registration.Content.Should().Contain("services.AddHostedService<global::Test.PublicWorker>");
    }

    /// <summary>
    ///     A private nested BackgroundService with an explicit <c>[Singleton]</c> attribute
    ///     escalates IOC066 to Warning severity. The user opted in to registration that the
    ///     generator cannot deliver because the type is unreachable from the registration
    ///     extension; warning severity reflects the unmet expectation while still avoiding the
    ///     CS0122 that direct emission would produce.
    /// </summary>
    [Fact]
    public void PrivateNested_BackgroundService_WithSingletonAttribute_SkippedWithDiagnostic()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public class Outer
{
    [Singleton]
    private class OptedInWorker : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("CS0122").Should().BeEmpty();
        result.HasErrors.Should().BeFalse();

        if (result.TryGetServiceRegistrationSource(out var registration))
            registration.Content.Should().NotContain("OptedInWorker");

        var ioc066 = result.GetDiagnosticsByCode("IOC066");
        ioc066.Should().ContainSingle();
        ioc066[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        ioc066[0].GetMessage().Should().Contain("OptedInWorker");
    }

    /// <summary>
    ///     Chain-walk coverage: a nominally <c>public</c> BackgroundService nested inside a
    ///     <c>private</c> outer is effectively private from the extension's vantage point. The
    ///     accessibility check must walk the entire containing-type chain and pick the strictest
    ///     link.
    /// </summary>
    [Fact]
    public void PublicNested_InsidePrivateOuter_NotAutoRegistered()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public class GrandParent
{
    private class Outer
    {
        public class Worker : BackgroundService
        {
            protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
        }
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("CS0122").Should().BeEmpty();
        result.HasErrors.Should().BeFalse();

        if (result.TryGetServiceRegistrationSource(out var registration))
            registration.Content.Should().NotContain("AddHostedService<global::Test.GrandParent");

        var ioc066 = result.GetDiagnosticsByCode("IOC066");
        ioc066.Should().NotBeEmpty();
        ioc066[0].Severity.Should().Be(DiagnosticSeverity.Info);
    }

    /// <summary>
    ///     A protected nested BackgroundService is effectively private from a non-derived
    ///     extension type; the registration extension is not a derived class, so it cannot
    ///     reference protected nested types.
    /// </summary>
    [Fact]
    public void ProtectedNested_BackgroundService_NotAutoRegistered()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public class Outer
{
    protected class ProtectedWorker : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("CS0122").Should().BeEmpty();
        result.HasErrors.Should().BeFalse();

        if (result.TryGetServiceRegistrationSource(out var registration))
            registration.Content.Should().NotContain("ProtectedWorker");

        var ioc066 = result.GetDiagnosticsByCode("IOC066");
        ioc066.Should().ContainSingle();
        ioc066[0].Severity.Should().Be(DiagnosticSeverity.Info);
    }

    /// <summary>
    ///     Positive case: an <c>internal</c> subclass of a <c>public</c> BackgroundService is
    ///     itself reachable from the registration extension (both links of the chain are
    ///     internal-or-better). This exercises the chain-walk's positive path and confirms the
    ///     common test-fixture pattern (internal subclass inside the test assembly) still
    ///     auto-registers.
    /// </summary>
    [Fact]
    public void Internal_TestSubclass_OfPublic_BackgroundService_AutoRegistered()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public class PublicBaseWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

internal class TestSubclassWorker : PublicBaseWorker
{
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.HasErrors.Should().BeFalse();
        result.GetDiagnosticsByCode("IOC066").Should().BeEmpty();

        var registration = result.GetRequiredServiceRegistrationSource();
        registration.Content.Should().Contain("services.AddHostedService<global::Test.TestSubclassWorker>");
    }
}
