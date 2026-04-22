namespace IoCTools.Generator.Shared.Tests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using IoCTools.Generator.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public sealed class ResolveForServiceTests
{
    private static readonly string AbstractionsDllPath =
        typeof(IoCTools.Abstractions.Annotations.AutoDepAttribute<>).Assembly.Location;

    private static readonly string MelLoggingAbstractionsDllPath =
        typeof(Microsoft.Extensions.Logging.ILogger<>).Assembly.Location;

    private static readonly IReadOnlyDictionary<string, string> EmptyProps =
        new Dictionary<string, string>();

    private static MetadataReference[] BaseReferences(bool includeAbstractions, bool includeLogging)
    {
        var trustedAssemblies = ((string?)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(System.IO.Path.PathSeparator, System.StringSplitOptions.RemoveEmptyEntries);
        var netstandardPath = trustedAssemblies.FirstOrDefault(p =>
            System.IO.Path.GetFileNameWithoutExtension(p).Equals("netstandard", System.StringComparison.OrdinalIgnoreCase));
        var runtimePath = trustedAssemblies.FirstOrDefault(p =>
            System.IO.Path.GetFileNameWithoutExtension(p).Equals("System.Runtime", System.StringComparison.OrdinalIgnoreCase));

        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
        };
        if (netstandardPath is { }) refs.Add(MetadataReference.CreateFromFile(netstandardPath));
        if (runtimePath is { }) refs.Add(MetadataReference.CreateFromFile(runtimePath));
        if (includeAbstractions) refs.Add(MetadataReference.CreateFromFile(AbstractionsDllPath));
        if (includeLogging) refs.Add(MetadataReference.CreateFromFile(MelLoggingAbstractionsDllPath));
        return refs.ToArray();
    }

    private static Compilation CreateCompilation(
        string assemblyName,
        string source,
        MetadataReference[]? extraReferences = null,
        bool includeAbstractions = true,
        bool includeLogging = false)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var refs = BaseReferences(includeAbstractions, includeLogging);
        if (extraReferences is { }) refs = refs.Concat(extraReferences).ToArray();
        return CSharpCompilation.Create(
            assemblyName,
            new[] { tree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static MetadataReference EmitToReference(Compilation compilation, string label)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            var diagnostics = string.Join("\n", result.Diagnostics.Select(d => d.ToString()));
            throw new System.Exception($"Emit failed for {label}: {diagnostics}");
        }
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static INamedTypeSymbol GetType(Compilation compilation, string fullyQualifiedName)
    {
        var symbol = compilation.GetTypeByMetadataName(fullyQualifiedName);
        symbol.Should().NotBeNull($"type {fullyQualifiedName} should exist");
        return symbol!;
    }

    private static void AssertNoCompileErrors(Compilation c)
    {
        c.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    private static IReadOnlyDictionary<string, string> Props(params (string, string)[] pairs)
    {
        var d = new Dictionary<string, string>();
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }

    // --- 1. Empty universe ---
    [Fact]
    public void Empty_universe_no_attributes_yields_empty()
    {
        var source = "namespace Consumer { public class Svc { } }";
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.IsEmpty.Should().BeTrue();
    }

    // --- 2. Single closed AutoDep<IFoo> ---
    [Fact]
    public void Single_closed_AutoDep_yields_universal_entry()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>]
namespace Consumer { public interface IFoo { } public class Svc { } }
";
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.Should().HaveCount(1);
        output.Entries[0].Sources.Should().HaveCount(1);
        output.Entries[0].Sources[0].Kind.Should().Be(AutoDepSourceKind.AutoUniversal);
        output.Entries[0].DepType.MetadataName.Should().Contain("IFoo");
    }

    // --- 3. Open-generic AutoDepOpen closes to service type ---
    [Fact]
    public void Open_generic_AutoDepOpen_closes_to_service_type()
    {
        // Suppress built-in ILogger so we isolate the AutoDepOpen path;
        // otherwise both built-in and declared paths produce the same closed ILogger<Svc>.
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
[assembly: AutoDepOpen(typeof(ILogger<>))]
namespace Consumer { public class Svc { } }
";
        var compilation = CreateCompilation("ConsumerAsm", source, includeLogging: true);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var props = Props(("build_property.IoCToolsAutoDetectLogger", "false"));
        var output = AutoDepsResolver.ResolveForService(compilation, svc, props);

        output.Entries.Should().HaveCount(1);
        output.Entries[0].Sources[0].Kind.Should().Be(AutoDepSourceKind.AutoOpenUniversal);
        output.Entries[0].DepType.MetadataName.Should().Contain("ILogger");
        output.Entries[0].DepType.MetadataName.Should().Contain("Svc");
    }

    // --- 4. Built-in ILogger auto-detection ---
    [Fact]
    public void Builtin_ILogger_auto_detected_when_MEL_referenced()
    {
        var source = "namespace Consumer { public class Svc { } }";
        var compilation = CreateCompilation("ConsumerAsm", source, includeLogging: true);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.Should().HaveCount(1);
        output.Entries[0].Sources[0].Kind.Should().Be(AutoDepSourceKind.AutoBuiltinILogger);
        output.Entries[0].DepType.MetadataName.Should().Contain("ILogger");
        output.Entries[0].DepType.MetadataName.Should().Contain("Svc");
    }

    // --- 5. Built-in suppressed via IoCToolsAutoDetectLogger=false ---
    [Fact]
    public void Builtin_ILogger_suppressed_when_AutoDetectLogger_false()
    {
        var source = "namespace Consumer { public class Svc { } }";
        var compilation = CreateCompilation("ConsumerAsm", source, includeLogging: true);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var props = Props(("build_property.IoCToolsAutoDetectLogger", "false"));
        var output = AutoDepsResolver.ResolveForService(compilation, svc, props);

        output.Entries.IsEmpty.Should().BeTrue();
    }

    // --- 6. Transitive from referenced assembly ---
    [Fact]
    public void Transitive_AutoDep_from_referenced_assembly_is_attributed_as_transitive()
    {
        var librarySource = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Library.IFoo>(Scope = AutoDepScope.Transitive)]
namespace Library { public interface IFoo { } }
";
        var libraryCompilation = CreateCompilation("LibraryAsm", librarySource);
        AssertNoCompileErrors(libraryCompilation);
        var libraryRef = EmitToReference(libraryCompilation, "LibraryAsm");

        var consumerSource = "namespace Consumer { public class Svc { } }";
        var consumerCompilation = CreateCompilation("ConsumerAsm", consumerSource, new[] { libraryRef });
        AssertNoCompileErrors(consumerCompilation);
        var svc = GetType(consumerCompilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(consumerCompilation, svc, EmptyProps);

        output.Entries.Should().HaveCount(1);
        output.Entries[0].Sources[0].Kind.Should().Be(AutoDepSourceKind.AutoTransitive);
        output.Entries[0].Sources[0].AssemblyName.Should().Be("LibraryAsm");
    }

    // --- 7. Kill switch ---
    [Fact]
    public void Kill_switch_disables_all_auto_deps()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>]
namespace Consumer { public interface IFoo { } public class Svc { } }
";
        var compilation = CreateCompilation("ConsumerAsm", source, includeLogging: true);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var props = Props(("build_property.IoCToolsAutoDepsDisable", "true"));
        var output = AutoDepsResolver.ResolveForService(compilation, svc, props);

        output.Entries.IsEmpty.Should().BeTrue();
    }

    // --- 8. Exclude glob matching namespace ---
    [Fact]
    public void Exclude_glob_matching_namespace_suppresses_all()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<MyApp.Generated.IFoo>]
namespace MyApp.Generated { public interface IFoo { } public class Svc { } }
";
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "MyApp.Generated.Svc");

        var props = Props(("build_property.IoCToolsAutoDepsExcludeGlob", "*.Generated*"));
        var output = AutoDepsResolver.ResolveForService(compilation, svc, props);

        output.Entries.IsEmpty.Should().BeTrue();
    }

    // --- 9. Manual constructor skips resolution ---
    [Fact]
    public void Manual_constructor_skips_resolution()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>]
namespace Consumer
{
    public interface IFoo { }
    public class Svc
    {
        public Svc(IFoo foo) { }
    }
}
";
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.IsEmpty.Should().BeTrue();
    }
}
