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

        // Try to add Moq reference from NuGet cache
        try
        {
            var moqAssembly = Assembly.Load("Moq, Version=4.20.72.0, Culture=neutral, PublicKeyToken=69f491c39445e920");
            if (moqAssembly != null)
                metadataRefs.Add(MetadataReference.CreateFromFile(moqAssembly.Location));
        }
        catch
        {
            try
            {
                var moqAssembly = Assembly.Load("Moq");
                if (moqAssembly != null)
                    metadataRefs.Add(MetadataReference.CreateFromFile(moqAssembly.Location));
            }
            catch
            {
                // Fall back to NuGet cache path
                var nugetCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget", "packages", "moq", "4.20.72", "lib", "netstandard2.0", "Moq.dll");
                if (File.Exists(nugetCache))
                    metadataRefs.Add(MetadataReference.CreateFromFile(nugetCache));
            }
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

    /// <summary>
    /// Verifies that the generated fixture code from a generation result
    /// compiles without errors when combined with the original source and
    /// all required references. Returns a new combined result.
    /// </summary>
    public static GenerationResult VerifyCompiles(string source, GenerationResult result, MetadataReference[]? additionalReferences = null)
    {
        // Collect all original source trees plus generated trees
        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
        };
        syntaxTrees.AddRange(result.GeneratedTrees);

        // Use trusted platform assemblies like the fixture generation tests do
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => MetadataReference.CreateFromFile(p))
            .Cast<MetadataReference>()
            .ToList();

        var iocToolsAssembly = typeof(Abstractions.Annotations.ScopedAttribute).Assembly;
        var iocTestingAssembly = typeof(IoCTools.Testing.Annotations.CoverAttribute<>).Assembly;

        var metadataRefs = new List<MetadataReference>(trustedAssemblies)
        {
            MetadataReference.CreateFromFile(iocToolsAssembly.Location),
            MetadataReference.CreateFromFile(iocTestingAssembly.Location),
        };

        // Add Moq reference - use both simple name and NuGet package cache path
        var moqLoaded = false;
        try
        {
            var moqAssembly = Assembly.Load("Moq, Version=4.20.72.0, Culture=neutral, PublicKeyToken=69f491c39445e920");
            if (moqAssembly != null)
            {
                metadataRefs.Add(MetadataReference.CreateFromFile(moqAssembly.Location));
                moqLoaded = true;
            }
        }
        catch { }

        if (!moqLoaded)
        {
            try
            {
                var moqAssembly = Assembly.Load("Moq");
                if (moqAssembly != null)
                {
                    metadataRefs.Add(MetadataReference.CreateFromFile(moqAssembly.Location));
                    moqLoaded = true;
                }
            }
            catch { }
        }

        if (!moqLoaded)
        {
            // Fall back to NuGet cache path
            var nugetCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages", "moq", "4.20.72", "lib", "netstandard2.0", "Moq.dll");
            if (File.Exists(nugetCache))
            {
                metadataRefs.Add(MetadataReference.CreateFromFile(nugetCache));
                moqLoaded = true;
            }
        }

        // Add System.Collections.Immutable
        try
        {
            var immutableAssembly = Assembly.Load("System.Collections.Immutable");
            if (immutableAssembly != null)
                metadataRefs.Add(MetadataReference.CreateFromFile(immutableAssembly.Location));
        }
        catch { }

        // Add Microsoft.Extensions.Configuration
        try
        {
            var configAssembly = Assembly.Load("Microsoft.Extensions.Configuration");
            if (configAssembly != null)
                metadataRefs.Add(MetadataReference.CreateFromFile(configAssembly.Location));
        }
        catch { }

        // Add Microsoft.Extensions.Configuration.Binder (for IConfiguration.GetValue<T>())
        var binderLoaded = false;
        try
        {
            var binderAssembly = Assembly.Load("Microsoft.Extensions.Configuration.Binder, Version=6.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60");
            if (binderAssembly != null)
            {
                metadataRefs.Add(MetadataReference.CreateFromFile(binderAssembly.Location));
                binderLoaded = true;
            }
        }
        catch { }

        if (!binderLoaded)
        {
            try
            {
                var binderAssembly = Assembly.Load("Microsoft.Extensions.Configuration.Binder");
                if (binderAssembly != null)
                {
                    metadataRefs.Add(MetadataReference.CreateFromFile(binderAssembly.Location));
                    binderLoaded = true;
                }
            }
            catch { }
        }

        if (!binderLoaded)
        {
            var binderCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages", "microsoft.extensions.configuration.binder", "6.0.0", "lib", "netstandard2.0", "Microsoft.Extensions.Configuration.Binder.dll");
            if (File.Exists(binderCache))
            {
                metadataRefs.Add(MetadataReference.CreateFromFile(binderCache));
                binderLoaded = true;
            }
        }

        // Add Microsoft.Extensions.Options
        try
        {
            var optionsAssembly = Assembly.Load("Microsoft.Extensions.Options");
            if (optionsAssembly != null)
                metadataRefs.Add(MetadataReference.CreateFromFile(optionsAssembly.Location));
        }
        catch { }

        // Add Microsoft.Extensions.Logging.Abstractions
        try
        {
            var loggingAssembly = Assembly.Load("Microsoft.Extensions.Logging.Abstractions");
            if (loggingAssembly != null)
                metadataRefs.Add(MetadataReference.CreateFromFile(loggingAssembly.Location));
        }
        catch { }

        if (additionalReferences != null && additionalReferences.Length > 0)
        {
            metadataRefs.AddRange(additionalReferences);
        }

        var compilation = CSharpCompilation.Create(
            "CompileCheck",
            syntaxTrees,
            metadataRefs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable));

        var allDiagnostics = compilation.GetDiagnostics();
        var errors = allDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();

        return new GenerationResult(result.GeneratedTrees, errors);
    }

    public record GenerationResult(
        ImmutableArray<SyntaxTree> GeneratedTrees,
        ImmutableArray<Diagnostic> Diagnostics);
}
