namespace IoCTools.Testing.Tests;

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Test helper for running the IoCTools.Testing generator in unit tests.
/// Note: The test fixture generation depends on the main generator first creating
/// constructors for services. This helper runs both generators together.
/// </summary>
internal static class TestHelper
{
    public static GenerationResult Generate(string source)
    {
        // Add IoCTools references
        var iocToolsAssembly = typeof(Abstractions.Annotations.ScopedAttribute).Assembly;
        var iocTestingAssembly = typeof(IoCTools.Testing.Annotations.CoverAttribute<>).Assembly;
        var metadataRefs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(iocToolsAssembly.Location),
            MetadataReference.CreateFromFile(iocTestingAssembly.Location),
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

        // Try to add Microsoft.Extensions.Configuration
        try
        {
            var configAssembly = Assembly.Load("Microsoft.Extensions.Configuration, Version=6.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60")
                ?? Assembly.Load("Microsoft.Extensions.Configuration");
            if (configAssembly != null)
                metadataRefs.Add(MetadataReference.CreateFromFile(configAssembly.Location));
        }
        catch
        {
            // Not critical if not available
        }

        // Try to add Microsoft.Extensions.Options
        try
        {
            var optionsAssembly = Assembly.Load("Microsoft.Extensions.Options, Version=6.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60")
                ?? Assembly.Load("Microsoft.Extensions.Options");
            if (optionsAssembly != null)
                metadataRefs.Add(MetadataReference.CreateFromFile(optionsAssembly.Location));
        }
        catch
        {
            // Not critical if not available
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

        // Run both generators together
        var compilation = CSharpCompilation.Create(
            "Test",
            new[] { syntaxTree },
            metadataRefs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable));

        var mainGenerator = new IoCTools.Generator.DependencyInjectionGenerator();
        var testingGenerator = new IoCTools.Testing.IoCToolsTestingGenerator();
        var driver = CSharpGeneratorDriver.Create(new[]
        {
            mainGenerator.AsSourceGenerator(),
            testingGenerator.AsSourceGenerator()
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
