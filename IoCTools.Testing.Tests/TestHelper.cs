namespace IoCTools.Testing.Tests;

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Test helper for running the IoCTools.Testing generator in unit tests.
/// </summary>
internal static class TestHelper
{
    public static GenerationResult Generate(string source)
    {
        var references = new[]
        {
            typeof(object).Assembly,
            typeof(CSharpCompilation).Assembly,
            Assembly.Load("Microsoft.CodeAnalysis, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35") ?? Assembly.Load("Microsoft.CodeAnalysis"),
            Assembly.Load("Microsoft.CodeAnalysis.CSharp, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35") ?? Assembly.Load("Microsoft.CodeAnalysis.CSharp"),
        };

        // Add IoCTools.Abstractions references
        var iocToolsAssembly = typeof(Abstractions.Annotations.ScopedAttribute).Assembly;
        var metadataRefs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(iocToolsAssembly.Location),
            MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location)
        };

        // Try to add Moq reference
        try
        {
            var moqAssembly = Assembly.Load("Moq, Version=4.0.0.0, Culture=neutral, PublicKeyToken=69f491c39445e920") ?? Assembly.Load("Moq");
            if (moqAssembly != null)
                metadataRefs.Add(MetadataReference.CreateFromFile(moqAssembly.Location));
        }
        catch
        {
            // Moq may not be available - tests will still work for checking generation
        }

        // Try to add System.Collections.Immutable
        try
        {
            var immutableAssembly = Assembly.Load("System.Collections.Immutable");
            if (immutableAssembly != null)
                metadataRefs.Add(MetadataReference.CreateFromFile(immutableAssembly.Location));
        }
        catch
        {
            // Not critical if not available
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var compilation = CSharpCompilation.Create(
            "Test",
            new[] { syntaxTree },
            metadataRefs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new IoCTools.Testing.IoCToolsTestingGenerator();
        var driver = CSharpGeneratorDriver.Create(new[] { generator.AsSourceGenerator() });
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees
            .Skip(1) // Skip the original source tree
            .ToImmutableArray();

        return new GenerationResult(generatedTrees, diagnostics.ToImmutableArray());
    }

    public record GenerationResult(
        ImmutableArray<SyntaxTree> GeneratedTrees,
        ImmutableArray<Diagnostic> Diagnostics);
}
