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

    private const string ProfileMarkerSource = @"
namespace Profiles {
    public sealed class WebProfile : IoCTools.Abstractions.Annotations.IAutoDepsProfile { }
}
";

    // --- 10. Profile attach via base class ---
    [Fact]
    public void Profile_attached_via_base_class_contributes_deps()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApply<Profiles.WebProfile, Consumer.ControllerBase>]
[assembly: AutoDepIn<Profiles.WebProfile, Consumer.IFoo>]
namespace Consumer
{
    public interface IFoo { }
    public abstract class ControllerBase { }
    public class Svc : ControllerBase { }
}
" + ProfileMarkerSource;
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.Should().HaveCount(1);
        output.Entries[0].Sources[0].Kind.Should().Be(AutoDepSourceKind.AutoProfile);
        output.Entries[0].Sources[0].SourceName.Should().Contain("WebProfile");
    }

    // --- 11. Profile attach via interface ---
    [Fact]
    public void Profile_attached_via_implemented_interface_contributes_deps()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApply<Profiles.WebProfile, Consumer.IHandler>]
[assembly: AutoDepIn<Profiles.WebProfile, Consumer.IFoo>]
namespace Consumer
{
    public interface IFoo { }
    public interface IHandler { }
    public class Svc : IHandler { }
}
" + ProfileMarkerSource;
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.Should().HaveCount(1);
        output.Entries[0].Sources[0].Kind.Should().Be(AutoDepSourceKind.AutoProfile);
    }

    // --- 12. Profile attach via glob pattern ---
    [Fact]
    public void Profile_attached_via_glob_pattern_contributes_deps()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApplyGlob<Profiles.WebProfile>(""*.Web.*"")]
[assembly: AutoDepIn<Profiles.WebProfile, MyApp.Web.Controllers.IFoo>]
namespace MyApp.Web.Controllers
{
    public interface IFoo { }
    public class Svc { }
}
" + ProfileMarkerSource;
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "MyApp.Web.Controllers.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.Should().HaveCount(1);
        output.Entries[0].Sources[0].Kind.Should().Be(AutoDepSourceKind.AutoProfile);
    }

    // --- 13. Explicit [AutoDeps<Profile>] on service ---
    [Fact]
    public void Explicit_AutoDeps_on_service_attaches_profile()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepIn<Profiles.WebProfile, Consumer.IFoo>]
namespace Consumer
{
    public interface IFoo { }
    [AutoDeps<Profiles.WebProfile>]
    public class Svc { }
}
" + ProfileMarkerSource;
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.Should().HaveCount(1);
        output.Entries[0].Sources[0].Kind.Should().Be(AutoDepSourceKind.AutoProfile);
        output.Entries[0].Sources[0].SourceName.Should().Contain("WebProfile");
    }

    // --- 14. [NoAutoDeps] kills everything ---
    [Fact]
    public void NoAutoDeps_attribute_wipes_all_entries()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>]
namespace Consumer
{
    public interface IFoo { }
    [NoAutoDeps]
    public class Svc { }
}
";
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.IsEmpty.Should().BeTrue();
    }

    // --- 15. [NoAutoDep<T>] removes a specific closed entry ---
    [Fact]
    public void NoAutoDep_T_removes_specific_closed_entry_and_keeps_others()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>]
[assembly: AutoDep<Consumer.IBar>]
namespace Consumer
{
    public interface IFoo { }
    public interface IBar { }
    [NoAutoDep<IFoo>]
    public class Svc { }
}
";
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.Should().HaveCount(1);
        output.Entries[0].DepType.MetadataName.Should().Contain("IBar");
    }

    // --- 16. [NoAutoDepOpen(typeof(ILogger<>))] removes built-in ILogger entry ---
    [Fact]
    public void NoAutoDepOpen_ILogger_removes_builtin_entry()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
namespace Consumer
{
    [NoAutoDepOpen(typeof(ILogger<>))]
    public class Svc { }
}
";
        var compilation = CreateCompilation("ConsumerAsm", source, includeLogging: true);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.IsEmpty.Should().BeTrue();
    }

    // --- 17. [NoAutoDepOpen] removes declared open-generic entry ---
    [Fact]
    public void NoAutoDepOpen_removes_declared_open_generic_entry()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
[assembly: AutoDepOpen(typeof(ILogger<>))]
namespace Consumer
{
    [NoAutoDepOpen(typeof(ILogger<>))]
    public class Svc { }
}
";
        var compilation = CreateCompilation("ConsumerAsm", source, includeLogging: true);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var props = Props(("build_property.IoCToolsAutoDetectLogger", "false"));
        var output = AutoDepsResolver.ResolveForService(compilation, svc, props);

        output.Entries.IsEmpty.Should().BeTrue();
    }

    // --- 18. Bare DependsOn wins over auto-dep ---
    [Fact]
    public void Bare_DependsOn_wins_over_AutoDep_for_same_type()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>]
namespace Consumer
{
    public interface IFoo { }
    [DependsOn<IFoo>]
    public class Svc { }
}
";
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.IsEmpty.Should().BeTrue();
    }

    // --- 19. DependsOn with memberName1 customization wins ---
    [Fact]
    public void Customized_DependsOn_memberName_wins_over_AutoDep()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>]
namespace Consumer
{
    public interface IFoo { }
    [DependsOn<IFoo>(memberName1: ""_custom"")]
    public class Svc { }
}
";
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.IsEmpty.Should().BeTrue();
    }

    // --- 20. DependsOn with external:true via constructor-param named syntax ---
    [Fact]
    public void Customized_DependsOn_external_true_via_ctor_named_syntax_wins()
    {
        // This test specifically verifies the ConstructorArguments positional-read path
        // because `external` is a CONSTRUCTOR PARAMETER (index 3), not just a property.
        // `[DependsOn<IFoo>(external: true)]` puts `true` in ConstructorArguments[3], NOT
        // NamedArguments, since C# named-argument syntax binds to a constructor parameter.
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>]
namespace Consumer
{
    public interface IFoo { }
    [DependsOn<IFoo>(external: true)]
    public class Svc { }
}
";
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        output.Entries.IsEmpty.Should().BeTrue();
    }

    // --- 21. Multi-partial attribute union: DependsOn + NoAutoDep across partials ---
    [Fact]
    public void Multi_partial_service_unions_DependsOn_and_NoAutoDep_across_partials()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>]
[assembly: AutoDep<Consumer.IBar>]
[assembly: AutoDep<Consumer.IBaz>]
namespace Consumer
{
    public interface IFoo { }
    public interface IBar { }
    public interface IBaz { }

    [DependsOn<IFoo>]
    public partial class Svc { }

    [NoAutoDep<IBar>]
    public partial class Svc { }
}
";
        var compilation = CreateCompilation("ConsumerAsm", source);
        AssertNoCompileErrors(compilation);
        var svc = GetType(compilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(compilation, svc, EmptyProps);

        // IFoo dropped by DependsOn; IBar dropped by NoAutoDep; IBaz survives.
        output.Entries.Should().HaveCount(1);
        output.Entries[0].DepType.MetadataName.Should().Contain("IBaz");
    }

    // --- 22. Transitive AutoDep + consumer [NoAutoDep<T>] wins ---
    [Fact]
    public void Consumer_NoAutoDep_overrides_transitive_AutoDep()
    {
        var librarySource = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Library.IFoo>(Scope = AutoDepScope.Transitive)]
namespace Library { public interface IFoo { } }
";
        var libraryCompilation = CreateCompilation("LibraryAsm", librarySource);
        AssertNoCompileErrors(libraryCompilation);
        var libraryRef = EmitToReference(libraryCompilation, "LibraryAsm");

        var consumerSource = @"
using IoCTools.Abstractions.Annotations;
namespace Consumer
{
    [NoAutoDep<Library.IFoo>]
    public class Svc { }
}
";
        var consumerCompilation = CreateCompilation("ConsumerAsm", consumerSource, new[] { libraryRef });
        AssertNoCompileErrors(consumerCompilation);
        var svc = GetType(consumerCompilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(consumerCompilation, svc, EmptyProps);

        output.Entries.IsEmpty.Should().BeTrue();
    }

    // --- 23. Multi-library transitive union: two libraries same dep -> one entry, two sources ---
    [Fact]
    public void Multi_library_transitive_union_yields_one_entry_with_both_sources()
    {
        // Both libraries declare a transitive AutoDep for the same shared interface defined
        // in a shared library. Consumer references both; the resolver must dedupe on
        // SymbolIdentity but accumulate attributions.
        var sharedSource = @"
namespace Shared { public interface IFoo { } }
";
        var sharedCompilation = CreateCompilation("SharedAsm", sharedSource);
        AssertNoCompileErrors(sharedCompilation);
        var sharedRef = EmitToReference(sharedCompilation, "SharedAsm");

        var libAsource = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Shared.IFoo>(Scope = AutoDepScope.Transitive)]
";
        var libA = CreateCompilation("LibAAsm", libAsource, new[] { sharedRef });
        AssertNoCompileErrors(libA);
        var libAref = EmitToReference(libA, "LibAAsm");

        var libBsource = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Shared.IFoo>(Scope = AutoDepScope.Transitive)]
";
        var libB = CreateCompilation("LibBAsm", libBsource, new[] { sharedRef });
        AssertNoCompileErrors(libB);
        var libBref = EmitToReference(libB, "LibBAsm");

        var consumerSource = "namespace Consumer { public class Svc { } }";
        var consumer = CreateCompilation("ConsumerAsm", consumerSource, new[] { sharedRef, libAref, libBref });
        AssertNoCompileErrors(consumer);
        var svc = GetType(consumer, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(consumer, svc, EmptyProps);

        output.Entries.Should().HaveCount(1);
        output.Entries[0].Sources.Should().HaveCount(2);
        output.Entries[0].Sources.Select(s => s.AssemblyName).Should()
            .BeEquivalentTo(new[] { "LibAAsm", "LibBAsm" });
        output.Entries[0].Sources.Should().OnlyContain(s => s.Kind == AutoDepSourceKind.AutoTransitive);
    }

    // --- 24. Transitive AutoDepsApply evaluated in consumer ---
    [Fact]
    public void Transitive_AutoDepsApply_evaluates_match_in_consumer_compilation()
    {
        // Library ships both the profile attach rule (transitive) and the profile contribution
        // (also transitive). Consumer has a service whose base class matches the rule's TBase.
        var librarySource = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepsApply<Profiles.WebProfile, Library.ControllerBase>(Scope = AutoDepScope.Transitive)]
[assembly: AutoDepIn<Profiles.WebProfile, Library.IFoo>(Scope = AutoDepScope.Transitive)]
namespace Library
{
    public abstract class ControllerBase { }
    public interface IFoo { }
}
namespace Profiles
{
    public sealed class WebProfile : IoCTools.Abstractions.Annotations.IAutoDepsProfile { }
}
";
        var libraryCompilation = CreateCompilation("LibraryAsm", librarySource);
        AssertNoCompileErrors(libraryCompilation);
        var libraryRef = EmitToReference(libraryCompilation, "LibraryAsm");

        var consumerSource = @"
namespace Consumer
{
    public class Svc : Library.ControllerBase { }
}
";
        var consumerCompilation = CreateCompilation("ConsumerAsm", consumerSource, new[] { libraryRef });
        AssertNoCompileErrors(consumerCompilation);
        var svc = GetType(consumerCompilation, "Consumer.Svc");

        var output = AutoDepsResolver.ResolveForService(consumerCompilation, svc, EmptyProps);

        output.Entries.Should().HaveCount(1);
        output.Entries[0].Sources[0].Kind.Should().Be(AutoDepSourceKind.AutoTransitive);
        output.Entries[0].Sources[0].SourceName.Should().Contain("WebProfile");
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
