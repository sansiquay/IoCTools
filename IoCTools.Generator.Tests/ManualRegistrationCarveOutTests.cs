namespace IoCTools.Generator.Tests;

using System.Reflection;

using Abstractions.Annotations;

using Microsoft.CodeAnalysis.CSharp;

/// <summary>
///     Coverage for the IOC081 / IOC082 / IOC086 manual-registration diagnostics under the
///     framework-level analysis-scope gate plus the small set of orthogonal call-shape carve-outs
///     that survive regardless of project type.
///     <para>
///         IOC081/082/086 are <see cref="IoCTools.Generator.Diagnostics.AnalysisScope.Production" />-
///         scoped: they fire in production projects (where consistent attribute-based registration
///         is the norm) and suppress in test projects (where re-registering with fakes/stubs and
///         deliberately exercising lifetime-mismatch paths are routine). Test detection follows
///         the standard Roslyn pattern via <c>build_property.IsTestProject</c>; no naming
///         heuristics, no test-framework reference detection.
///     </para>
///     <para>
///         Two call-shape carve-outs are orthogonal to the scope gate (intentional regardless of
///         project type) and are exercised here too: <c>services.Replace(ServiceDescriptor.X&lt;T&gt;(...))</c>
///         is the canonical override pattern, and
///         <c>services.AddSingleton&lt;IHostedService&gt;(sp =&gt; sp.GetRequiredService&lt;TImpl&gt;())</c>
///         is the legacy companion-interface bridge.
///     </para>
/// </summary>
public class ManualRegistrationCarveOutTests
{
    private static Dictionary<string, string> TestProjectProperties() => new(StringComparer.Ordinal)
    {
        ["build_property.IsTestProject"] = "true"
    };

    #region Test-project carve-out (IOC081 / IOC082 / IOC086)

    /// <summary>
    ///     Regression-prevention: a duplicate manual registration in a non-test project still
    ///     emits IOC081. Without this the test-project gating could over-suppress.
    /// </summary>
    [Fact]
    public void IOC081_FiresOutsideTestProject()
    {
        const string source = @"
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
        result.GetDiagnosticsByCode("IOC081").Should().ContainSingle();
    }

    [Fact]
    public void IOC081_SuppressedInTestProject()
    {
        const string source = @"
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

        var result = SourceGeneratorTestHelper.CompileWithGenerator(
            source,
            analyzerBuildProperties: TestProjectProperties());
        result.GetDiagnosticsByCode("IOC081").Should().BeEmpty();
    }

    [Fact]
    public void IOC082_FiresOutsideTestProject()
    {
        const string source = @"
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
        result.GetDiagnosticsByCode("IOC082").Should().ContainSingle();
    }

    [Fact]
    public void IOC082_SuppressedInTestProject()
    {
        const string source = @"
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

        var result = SourceGeneratorTestHelper.CompileWithGenerator(
            source,
            analyzerBuildProperties: TestProjectProperties());
        result.GetDiagnosticsByCode("IOC082").Should().BeEmpty();
    }

    [Fact]
    public void IOC086_FiresOutsideTestProject()
    {
        const string source = @"
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public class HelperService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped<HelperService>();
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC086").Should().ContainSingle();
    }

    [Fact]
    public void IOC086_SuppressedInTestProject()
    {
        const string source = @"
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public class HelperService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped<HelperService>();
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(
            source,
            analyzerBuildProperties: TestProjectProperties());
        result.GetDiagnosticsByCode("IOC086").Should().BeEmpty();
    }

    #endregion

    #region services.Replace(...) carve-out (IOC081 / IOC082)

    /// <summary>
    ///     <c>services.Replace(ServiceDescriptor.X&lt;T&gt;(...))</c> is the canonical "swap the
    ///     IoCTools-registered impl for a different one" call shape (fakes in tests, alternate
    ///     impls in composition root). It is intentional and never a duplicate.
    /// </summary>
    [Fact]
    public void IOC081_SuppressedInsideServicesReplace()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Test;

[Scoped]
public partial class FooService {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.Replace(ServiceDescriptor.Scoped<FooService, FooService>());
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC081").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC082").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC086").Should().BeEmpty();
    }

    #endregion

    #region IHostedService companion-bridge carve-out (IOC081 / IOC082)

    /// <summary>
    ///     <c>services.AddSingleton&lt;IHostedService&gt;(sp =&gt; sp.GetRequiredService&lt;TImpl&gt;())</c>
    ///     is the legacy way to bridge a hosted service to its companion interfaces. Today the
    ///     emitter handles this automatically when the class has <c>[RegisterAs&lt;T&gt;(Shared)]</c>,
    ///     but legacy code still uses the manual bridge. The validator must not flag it.
    /// </summary>
    [Fact]
    public void IOC081_SuppressedForHostedServiceCompanionBridge()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

[Singleton]
public partial class HostedWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<HostedWorker>());
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC081").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC082").Should().BeEmpty();
    }

    #endregion

    #region Factory-registration carve-out (IOC086)

    /// <summary>
    ///     Explicit-factory registration shapes — <c>services.AddSingleton&lt;T&gt;(sp =&gt; ...)</c>
    ///     and the <c>TryAdd*</c> variants — express composition logic that IoCTools attributes
    ///     cannot capture. IOC086 must not fire on these shapes; the consumer is intentionally
    ///     wiring a delegate, not declaring an attribute-driven registration.
    /// </summary>
    [Fact]
    public void IOC086_DoesNotFire_OnFactoryRegistration()
    {
        const string source = @"
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IFoo { }
public class FooImpl : IFoo { }

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFoo>(sp => new FooImpl());
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC086").Should().BeEmpty();
    }

    [Fact]
    public void IOC086_DoesNotFire_OnTryAddSingletonFactory()
    {
        const string source = @"
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Test;

public interface IFoo { }
public class FooImpl : IFoo { }

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.TryAddSingleton<IFoo>(sp => new FooImpl());
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC086").Should().BeEmpty();
    }

    #endregion

    #region TryAddEnumerable carve-out (IOC086)

    /// <summary>
    ///     <c>services.TryAddEnumerable(ServiceDescriptor.X&lt;T, TImpl&gt;(...))</c> is the canonical
    ///     additive registration shape — one of N contributors to an enumerable resolution. The
    ///     inner <c>ServiceDescriptor</c> factory call is intentionally not a sole registration,
    ///     so the "use IoCTools attributes instead" suggestion is wrong: removing the manual call
    ///     would change semantics.
    /// </summary>
    [Fact]
    public void IOC086_DoesNotFire_OnTryAddEnumerable()
    {
        const string source = @"
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Test;

public interface IFoo { }
public class FooImpl : IFoo { }

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFoo, FooImpl>());
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC086").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC081").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC082").Should().BeEmpty();
    }

    #endregion

    #region External-assembly carve-out (IOC086)

    /// <summary>
    ///     Builds a metadata reference for a synthetic assembly that does NOT reference
    ///     <c>IoCTools.Abstractions</c>. Types defined in such assemblies cannot be IoCTools-
    ///     attributed by the consumer, so IOC086's "use IoCTools attributes instead" suggestion
    ///     is not actionable.
    /// </summary>
    private static MetadataReference BuildIoCToolsUnawareLibrary(string source, string assemblyName)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
        };

        try
        {
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location));
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location));
        }
        catch
        {
            // ignore if not present in this runtime
        }

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        using var ms = new MemoryStream();
        var emit = compilation.Emit(ms);
        emit.Success.Should().BeTrue(string.Join("\n", emit.Diagnostics.Select(d => d.ToString())));
        ms.Position = 0;
        return MetadataReference.CreateFromImage(ms.ToArray());
    }

    /// <summary>
    ///     IHttpContextAccessor / IPostConfigureOptions&lt;T&gt; / third-party framework types live
    ///     in assemblies the consumer does not own. IOC086 must not fire on registrations whose
    ///     implementation type is sourced from an assembly with no IoCTools.Abstractions
    ///     reference — the consumer cannot add IoCTools attributes to source they don't own.
    /// </summary>
    [Fact]
    public void IOC086_DoesNotFire_OnExternalAssemblyServiceType()
    {
        const string libSource = @"
namespace ThirdParty.Lib;

public interface IThirdPartyService { }
public class ThirdPartyServiceImpl : IThirdPartyService { }
";
        var libRef = BuildIoCToolsUnawareLibrary(libSource, "ThirdParty.Lib");

        const string consumerSource = @"
using Microsoft.Extensions.DependencyInjection;
using ThirdParty.Lib;

namespace Test;

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IThirdPartyService, ThirdPartyServiceImpl>();
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(consumerSource,
            additionalMetadataReferences: new[] { libRef });
        result.GetDiagnosticsByCode("IOC086").Should().BeEmpty();
    }

    /// <summary>
    ///     Regression: the original IOC086 detection still fires on simple manual registrations
    ///     in the consumer's own assembly where the implementation type has no IoCTools attributes.
    ///     Without this, the carve-outs above could over-suppress and silence the rule entirely.
    /// </summary>
    [Fact]
    public void IOC086_StillFires_OnSimpleManualRegistration()
    {
        const string source = @"
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IFoo { }
public class FooImpl : IFoo { }

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFoo, FooImpl>();
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC086").Should().ContainSingle();
    }

    #endregion
}
