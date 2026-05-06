namespace IoCTools.Generator.Tests;

/// <summary>
///     Verifies that <c>[RegisterAs&lt;T&gt;]</c> / <c>[RegisterAsAll]</c> compose with classes
///     that implement <c>IHostedService</c>. Without these tests in place, the generator
///     silently dropped the companion-interface registrations and emitted only
///     <c>services.AddHostedService&lt;TImpl&gt;()</c>, which prevented the documented
///     "register concrete once, bridge to multiple interfaces via factory" pattern from
///     working alongside hosted services.
/// </summary>
public class RegisterAsHostedServiceTests
{
    [Fact]
    public void RegisterAs_WithIHostedService_AndExplicitLifetime_EmitsConcretePlusInterfaceAndHostedServiceBridges()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test
{
    public interface IMyManager
    {
        void DoWork();
    }

    [Singleton]
    [RegisterAs<IMyManager>(InstanceSharing.Shared)]
    public sealed partial class MyRegistry : IMyManager, IHostedService
    {
        public void DoWork() { }
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Concrete singleton - the single shared instance.
        registrationSource.Content.Should()
            .Contain("services.AddSingleton<global::Test.MyRegistry>();");

        // Companion interface bridges to the same instance via factory.
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IMyManager>(provider => provider.GetRequiredService<global::Test.MyRegistry>());");

        // IHostedService also bridges to the same instance so the host runs Start/Stop on it
        // without creating a second copy of the class.
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Microsoft.Extensions.Hosting.IHostedService>(provider => provider.GetRequiredService<global::Test.MyRegistry>());");

        // The legacy short-circuit must NOT fire - we explicitly opted out of it by adding
        // [RegisterAs<T>], so AddHostedService<TImpl>() would create an unwanted second instance.
        registrationSource.Content.Should()
            .NotContain("services.AddHostedService<global::Test.MyRegistry>();");
    }

    [Fact]
    public void RegisterAsAll_WithIHostedService_AndMultipleInterfaces_BridgesAllInterfacesIncludingHostedService()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test
{
    public interface IRegistryA { void A(); }
    public interface IRegistryB { void B(); }

    [Singleton]
    [RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
    public sealed partial class FullRegistry : IRegistryA, IRegistryB, IHostedService
    {
        public void A() { }
        public void B() { }
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Concrete singleton.
        registrationSource.Content.Should()
            .Contain("services.AddSingleton<global::Test.FullRegistry>();");

        // Both user-declared interfaces bridge.
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IRegistryA>(provider => provider.GetRequiredService<global::Test.FullRegistry>());");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IRegistryB>(provider => provider.GetRequiredService<global::Test.FullRegistry>());");

        // IHostedService bridges to the same instance.
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Microsoft.Extensions.Hosting.IHostedService>(provider => provider.GetRequiredService<global::Test.FullRegistry>());");

        // Must not fall back to AddHostedService<T>() which would create a second instance.
        registrationSource.Content.Should()
            .NotContain("services.AddHostedService<global::Test.FullRegistry>();");
    }

    [Fact]
    public void HostedService_WithoutRegisterAs_PreservesAddHostedServiceShape()
    {
        // Regression guard for the common case: a class that is purely a hosted service with
        // no companion-interface needs must keep emitting the implicit AddHostedService<T>()
        // call exactly as before. The fix targets only the [RegisterAs*] opt-in path.
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test
{
    [Singleton]
    public sealed partial class PlainHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Implicit shape preserved.
        registrationSource.Content.Should()
            .Contain("services.AddHostedService<global::Test.PlainHostedService>();");

        // Must NOT have synthesised a Singleton concrete row or an IHostedService factory row.
        registrationSource.Content.Should().NotContain("services.AddSingleton<global::Test.PlainHostedService>();");
        registrationSource.Content.Should()
            .NotContain(
                "services.AddSingleton<global::Microsoft.Extensions.Hosting.IHostedService>(provider => provider.GetRequiredService<global::Test.PlainHostedService>());");
    }

    [Fact]
    public void RegisterAs_WithIHostedService_AndScopedLifetime_BridgesAtScopedLifetime()
    {
        // [Scoped] + IHostedService is unusual (the host resolves IHostedService from the root
        // provider, so a Scoped registration will throw at startup), but the generator's job is
        // to honour the user's declared lifetime - not to second-guess it. Mirror what the
        // generator already does for [Scoped] + IHostedService without [RegisterAs] (it emits a
        // hosted-service registration without rewriting the lifetime), and emit Scoped bridges
        // here so the failure surface stays consistent. Diagnostics for "Scoped hosted service"
        // are owned by the analyzer layer, not the registration layer.
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test
{
    public interface IScopedManager { void Touch(); }

    [Scoped]
    [RegisterAs<IScopedManager>(InstanceSharing.Shared)]
    public sealed partial class ScopedRegistry : IScopedManager, IHostedService
    {
        public void Touch() { }
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Concrete row at the user's declared lifetime.
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.ScopedRegistry>();");

        // Companion-interface bridge at the same lifetime.
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IScopedManager>(provider => provider.GetRequiredService<global::Test.ScopedRegistry>());");

        // IHostedService bridge at the same lifetime - emission-consistent with the rest of the
        // class. (Runtime-level diagnostics for Scoped hosted services are out of scope here.)
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Microsoft.Extensions.Hosting.IHostedService>(provider => provider.GetRequiredService<global::Test.ScopedRegistry>());");

        // Must NOT fall back to AddHostedService<T>().
        registrationSource.Content.Should()
            .NotContain("services.AddHostedService<global::Test.ScopedRegistry>();");
    }
}
