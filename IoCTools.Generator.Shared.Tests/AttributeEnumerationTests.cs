namespace IoCTools.Generator.Shared.Tests;

using System.IO;
using System.Linq;
using FluentAssertions;
using IoCTools.Generator.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public sealed class AttributeEnumerationTests
{
    private static readonly string AbstractionsDllPath =
        typeof(IoCTools.Abstractions.Annotations.AutoDepAttribute<>).Assembly.Location;

    private static MetadataReference[] BaseReferences(bool includeAbstractions)
    {
        // Use the runtime reference assembly set via System.Runtime trust so we get netstandard too.
        var trustedAssemblies = ((string?)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(System.IO.Path.PathSeparator, System.StringSplitOptions.RemoveEmptyEntries);
        var netstandardPath = trustedAssemblies.FirstOrDefault(p =>
            System.IO.Path.GetFileNameWithoutExtension(p).Equals("netstandard", System.StringComparison.OrdinalIgnoreCase));
        var runtimePath = trustedAssemblies.FirstOrDefault(p =>
            System.IO.Path.GetFileNameWithoutExtension(p).Equals("System.Runtime", System.StringComparison.OrdinalIgnoreCase));

        var refs = new System.Collections.Generic.List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
        };
        if (netstandardPath is { }) refs.Add(MetadataReference.CreateFromFile(netstandardPath));
        if (runtimePath is { }) refs.Add(MetadataReference.CreateFromFile(runtimePath));
        if (includeAbstractions) refs.Add(MetadataReference.CreateFromFile(AbstractionsDllPath));
        return refs.ToArray();
    }

    private static Compilation CreateCompilation(string assemblyName, string source,
        MetadataReference[]? extraReferences = null, bool includeAbstractions = true)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var refs = BaseReferences(includeAbstractions);
        if (extraReferences is { }) refs = refs.Concat(extraReferences).ToArray();
        return CSharpCompilation.Create(
            assemblyName,
            new[] { tree },
            refs,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
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

    private const string FooInterfaceSource = @"
namespace Consumer { public interface IFoo { } }
";

    // --- (a) Local AutoDep<IFoo> is enumerated as local, not transitive ---
    [Fact]
    public void Local_AutoDep_is_enumerated_as_local_not_transitive()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>]
" + FooInterfaceSource;
        var compilation = CreateCompilation("ConsumerAsm", source);
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var results = AutoDepsResolver.EnumerateAutoDepAttributes(compilation, includeTransitive: true).ToList();

        results.Should().HaveCount(1);
        var item = results[0];
        item.IsTransitive.Should().BeFalse();
        item.DeclaringAssembly.Should().BeSameAs(compilation.Assembly);
        item.Attribute.AttributeClass!.Name.Should().Be("AutoDepAttribute");
    }

    // --- (b) Local AutoDep with Scope=Transitive is still local ---
    [Fact]
    public void Local_AutoDep_with_Transitive_scope_is_still_local()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>(Scope = AutoDepScope.Transitive)]
" + FooInterfaceSource;
        var compilation = CreateCompilation("ConsumerAsm", source);
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var results = AutoDepsResolver.EnumerateAutoDepAttributes(compilation, includeTransitive: true).ToList();

        results.Should().HaveCount(1);
        results[0].IsTransitive.Should().BeFalse();
        results[0].DeclaringAssembly.Should().BeSameAs(compilation.Assembly);
    }

    // --- (c) Transitive attribute from referenced assembly is emitted with IsTransitive=true ---
    [Fact]
    public void Transitive_attribute_from_referenced_assembly_is_emitted()
    {
        var librarySource = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Library.IBar>(Scope = AutoDepScope.Transitive)]
namespace Library { public interface IBar { } }
";
        var libraryCompilation = CreateCompilation("LibraryAsm", librarySource);
        libraryCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var libraryRef = EmitToReference(libraryCompilation, "LibraryAsm");

        var consumerSource = "namespace Consumer { public class C { } }";
        var consumerCompilation = CreateCompilation(
            "ConsumerAsm", consumerSource, new[] { libraryRef });
        consumerCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var results = AutoDepsResolver.EnumerateAutoDepAttributes(consumerCompilation, includeTransitive: true).ToList();

        results.Should().HaveCount(1);
        var item = results[0];
        item.IsTransitive.Should().BeTrue();
        item.DeclaringAssembly.Identity.Name.Should().Be("LibraryAsm");
        item.Attribute.AttributeClass!.Name.Should().Be("AutoDepAttribute");
    }

    // --- (d) Non-transitive attribute from referenced assembly is NOT emitted ---
    [Fact]
    public void NonTransitive_attribute_from_referenced_assembly_is_not_emitted()
    {
        var librarySource = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Library.IBar>]
namespace Library { public interface IBar { } }
";
        var libraryCompilation = CreateCompilation("LibraryAsm", librarySource);
        libraryCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var libraryRef = EmitToReference(libraryCompilation, "LibraryAsm");

        var consumerSource = "namespace Consumer { public class C { } }";
        var consumerCompilation = CreateCompilation(
            "ConsumerAsm", consumerSource, new[] { libraryRef });

        var results = AutoDepsResolver.EnumerateAutoDepAttributes(consumerCompilation, includeTransitive: true).ToList();

        results.Should().BeEmpty();
    }

    // --- (e) Referenced assembly without IoCTools.Abstractions reference is skipped ---
    [Fact]
    public void Referenced_assembly_without_abstractions_reference_is_skipped()
    {
        // Library that does NOT reference IoCTools.Abstractions — should be skipped entirely.
        var librarySource = "namespace Library { public class Empty { } }";
        var libraryCompilation = CreateCompilation(
            "NonAbstractionsLibraryAsm", librarySource, extraReferences: null, includeAbstractions: false);
        libraryCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var libraryRef = EmitToReference(libraryCompilation, "NonAbstractionsLibraryAsm");

        // Consumer with a local AutoDep and a reference to the non-abstractions library.
        var consumerSource = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>]
" + FooInterfaceSource;
        var consumerCompilation = CreateCompilation(
            "ConsumerAsm", consumerSource, new[] { libraryRef });

        var results = AutoDepsResolver.EnumerateAutoDepAttributes(consumerCompilation, includeTransitive: true).ToList();

        // Only the local entry; the non-abstractions library was skipped.
        results.Should().HaveCount(1);
        results[0].IsTransitive.Should().BeFalse();
        results[0].DeclaringAssembly.Should().BeSameAs(consumerCompilation.Assembly);
    }

    // --- (f) includeTransitive: false suppresses referenced-assembly enumeration ---
    [Fact]
    public void IncludeTransitive_false_suppresses_referenced_assembly_enumeration()
    {
        var librarySource = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Library.IBar>(Scope = AutoDepScope.Transitive)]
namespace Library { public interface IBar { } }
";
        var libraryCompilation = CreateCompilation("LibraryAsm", librarySource);
        var libraryRef = EmitToReference(libraryCompilation, "LibraryAsm");

        var consumerSource = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDep<Consumer.IFoo>]
" + FooInterfaceSource;
        var consumerCompilation = CreateCompilation(
            "ConsumerAsm", consumerSource, new[] { libraryRef });

        var results = AutoDepsResolver.EnumerateAutoDepAttributes(consumerCompilation, includeTransitive: false).ToList();

        // Only the local attribute — the transitive one from LibraryAsm is suppressed.
        results.Should().HaveCount(1);
        results[0].IsTransitive.Should().BeFalse();
        results[0].DeclaringAssembly.Identity.Name.Should().Be("ConsumerAsm");
    }

    // --- (g) Cross-version tolerance: referenced assembly with abstractions but no auto-dep attributes yields nothing ---
    [Fact]
    public void Referenced_assembly_with_abstractions_but_no_autodep_attributes_yields_nothing()
    {
        var librarySource = "namespace Library { public class Empty { } }";
        var libraryCompilation = CreateCompilation("EmptyLibraryAsm", librarySource);
        var libraryRef = EmitToReference(libraryCompilation, "EmptyLibraryAsm");

        var consumerSource = "namespace Consumer { public class C { } }";
        var consumerCompilation = CreateCompilation(
            "ConsumerAsm", consumerSource, new[] { libraryRef });

        var results = AutoDepsResolver.EnumerateAutoDepAttributes(consumerCompilation, includeTransitive: true).ToList();

        // No local attrs, no transitive attrs — empty.
        results.Should().BeEmpty();
    }
}
