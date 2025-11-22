namespace IoCTools.Generator.Tests;

using System.Reflection;

using Abstractions.Annotations;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public class MultiProjectExternalDependencyTests
{
    private static MetadataReference BuildLibraryReference(string source,
        string assemblyName,
        MetadataReference[]? additional = null)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ScopedAttribute).Assembly.Location)
        };

        // netstandard reference needed for Attribute base type when compiling standalone assemblies in-memory
        try
        {
            var netstandard = Assembly.Load("netstandard");
            references.Add(MetadataReference.CreateFromFile(netstandard.Location));
            var systemRuntime = Assembly.Load("System.Runtime");
            references.Add(MetadataReference.CreateFromFile(systemRuntime.Location));
        }
        catch
        {
            // ignore if not found
        }

        if (additional != null)
            references.AddRange(additional);

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

    [Fact]
    public void ExternalFlag_Unneeded_WhenImplementationInReferencedAssembly()
    {
        var libSource = @"
using IoCTools.Abstractions.Annotations;

public interface IBackend { }

[Scoped]
public class Backend : IBackend { }
";

        var libRef = BuildLibraryReference(libSource, "LibBackend");

        var consumerSource = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

[DependsOn<IBackend>(external: true)]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(consumerSource,
            additionalMetadataReferences: new[] { libRef });

        result.GetDiagnosticsByCode("IOC042").Should().ContainSingle();
    }

    [Fact]
    public void ExternalFlag_Unneeded_WhenImplementationTwoHopsAway()
    {
        var infraSource = @"
using IoCTools.Abstractions.Annotations;

public interface IData { }

[Scoped]
public class DataStore : IData { }
";
        var infraRef = BuildLibraryReference(infraSource, "InfraLib");

        var appSource = @"
public interface IData { }
"; // app references the interface but not implementation
        var appRef = BuildLibraryReference(appSource, "AppLib", new[] { infraRef });

        var consumerSource = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

[DependsOn<IData>(external: true)]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(consumerSource,
            additionalMetadataReferences: new[] { appRef, infraRef });

        result.GetDiagnosticsByCode("IOC042").Should().ContainSingle();
    }
}
