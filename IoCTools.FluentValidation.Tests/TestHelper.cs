namespace IoCTools.FluentValidation.Tests;

using System.Collections.Immutable;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Test helper for running the IoCTools.FluentValidation generator in unit tests.
/// Note: The FluentValidation generator works alongside the main generator.
/// This helper runs both generators together (two-generator test pattern).
/// </summary>
internal static class TestHelper
{
    public static GenerationResult Generate(string source)
    {
        // Add IoCTools references
        var iocToolsAssembly = typeof(Abstractions.Annotations.ScopedAttribute).Assembly;
        var metadataRefs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(iocToolsAssembly.Location),
            MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location)
        };

        // Add FluentValidation reference
        try
        {
            var fvAssembly = typeof(global::FluentValidation.AbstractValidator<>).Assembly;
            metadataRefs.Add(MetadataReference.CreateFromFile(fvAssembly.Location));
        }
        catch
        {
            // FluentValidation may not be available in some test contexts
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

        // Try to add Microsoft.Extensions.DependencyInjection
        try
        {
            var diAssembly = Assembly.Load("Microsoft.Extensions.DependencyInjection.Abstractions");
            if (diAssembly != null)
                metadataRefs.Add(MetadataReference.CreateFromFile(diAssembly.Location));
        }
        catch
        {
            // Not critical if not available
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

        // Run both generators together (two-generator test pattern)
        var compilation = CSharpCompilation.Create(
            "Test",
            new[] { syntaxTree },
            metadataRefs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable));

        var mainGenerator = new IoCTools.Generator.DependencyInjectionGenerator();
        var fvGenerator = new IoCTools.FluentValidation.FluentValidationGenerator();
        var driver = CSharpGeneratorDriver.Create(new[]
        {
            mainGenerator.AsSourceGenerator(),
            fvGenerator.AsSourceGenerator()
        },
            Array.Empty<AdditionalText>(),
            new CSharpParseOptions(LanguageVersion.Preview));

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
