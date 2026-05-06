namespace IoCTools.Tools.Cli;

using System.Text.Json;
using CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Runs the `test scaffold` subcommand. Generates a partial test class with [Cover&lt;T&gt;]
/// and a smoke test for a production service type.
/// </summary>
internal static class TestScaffoldRunner
{
    public static async Task<int> RunAsync(TestScaffoldCommandOptions options, CancellationToken token)
    {
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        output.Verbose($"Service type: {options.ServiceType}");

        // Load the production project to resolve the service type
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");

        // Find the service type
        var serviceSymbol = FindServiceType(context.Compilation, options.ServiceType);
        if (serviceSymbol == null)
        {
            // Try to provide fuzzy suggestions
            var allTypes = GetAllServiceTypes(context.Compilation);
            var fuzzy = FuzzySuggestionUtility.GetSuggestions(options.ServiceType, allTypes.Select(t => t.ToDisplayString()));
            var msg = $"Service type '{options.ServiceType}' not found in project '{options.Common.ProjectPath}'.";
            if (fuzzy.Count > 0)
                msg += $" Did you mean: {string.Join(", ", fuzzy)}?";
            return ExitWithError(options, msg);
        }

        if (serviceSymbol.DeclaredAccessibility != Accessibility.Public &&
            serviceSymbol.DeclaredAccessibility != Accessibility.Internal)
        {
            return ExitWithError(options,
                $"Service type '{options.ServiceType}' is not public or internal and cannot be used in a test class.");
        }

        // Build test class name
        var serviceName = serviceSymbol.Name;
        var testClassName = $"{serviceName}Tests";

        // Infer test namespace from test-project or service namespace
        var serviceNamespace = serviceSymbol.ContainingNamespace?.ToString() ?? "Tests";
        var testNamespace = options.TestProjectPath != null
            ? InferTestNamespaceFromProject(options.TestProjectPath, serviceNamespace)
            : InferTestNamespace(serviceNamespace);

        // Discover constructor parameters
        var constructor = serviceSymbol.Constructors
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault(c => !c.IsStatic);

        var dependencies = new List<ScaffoldDependency>();
        if (constructor != null)
        {
            foreach (var param in constructor.Parameters)
            {
                var fieldName = GetMockFieldName(param.Type);
                dependencies.Add(new ScaffoldDependency(
                    param.Type.ToDisplayString(),
                    param.Name,
                    fieldName));
            }
        }

        // Check for ambiguity
        if (options.ServiceType.Contains('.') == false)
        {
            var matches = GetAllServiceTypes(context.Compilation)
                .Where(t => t.Name == serviceSymbol.Name)
                .ToList();
            if (matches.Count > 1)
            {
                return ExitWithError(options,
                    $"Ambiguous type name '{options.ServiceType}'. Found {matches.Count} matches. Use fully-qualified name (e.g. {matches[0].ToDisplayString()}).");
            }
        }

        // Generate test class source
        var source = GenerateTestClassSource(
            testClassName,
            testNamespace,
            serviceSymbol.ToDisplayString(),
            dependencies,
            options.TestFramework,
            options.Mocking,
            options.Assertions);

        // Determine output path
        string outputPath;
        if (options.OutputPath != null)
        {
            // If output is a directory (trailing separator, exists as dir, or looks like dir),
            // append the test class file name.
            var normalizedOutput = options.OutputPath;
            if (normalizedOutput.EndsWith(Path.DirectorySeparatorChar) ||
                normalizedOutput.EndsWith(Path.AltDirectorySeparatorChar) ||
                Directory.Exists(normalizedOutput) ||
                string.IsNullOrEmpty(Path.GetExtension(normalizedOutput)))
            {
                outputPath = Path.Combine(normalizedOutput, $"{testClassName}.cs");
            }
            else
            {
                outputPath = normalizedOutput;
            }
        }
        else
        {
            // Default: infer from test project conventions or current directory
            outputPath = InferOutputPath(options.TestProjectPath, testClassName);
        }

        // Build result
        var result = new ScaffoldResult(
            options.ServiceType,
            testClassName,
            outputPath,
            dependencies,
            source);

        // Dry-run: print the generated source
        if (options.DryRun)
        {
            if (options.Common.Json)
            {
                output.WriteJson(result);
            }
            else
            {
                Console.WriteLine($"// IoCTools Test Scaffold - Dry Run");
                Console.WriteLine($"// Service: {options.ServiceType}");
                Console.WriteLine($"// Output: {outputPath}");
                Console.WriteLine($"// Dependencies: {dependencies.Count}");
                Console.WriteLine(new string('-', 60));
                Console.WriteLine(source);
            }
            return 0;
        }

        // Check if file exists
        if (File.Exists(outputPath) && !options.Force)
        {
            return ExitWithError(options,
                $"Output file '{outputPath}' already exists. Use --force to overwrite.");
        }

        // Ensure directory exists
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Write file
        await File.WriteAllTextAsync(outputPath, source, token);
        if (!options.Common.Json)
            Console.WriteLine($"Generated test scaffold: {outputPath}");

        if (options.Common.Json)
        {
            output.WriteJson(result);
        }

        output.ReportTiming("test scaffold completed");
        return 0;
    }

    private static int ExitWithError(TestScaffoldCommandOptions options, string message)
    {
        if (options.Common.Json)
        {
            Console.Error.WriteLine(message);
            return 1;
        }

        return UsagePrinter.ExitWithError(message);
    }

    private static INamedTypeSymbol? FindServiceType(CSharpCompilation compilation, string typeName)
    {
        // Try exact match first
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var typeDecls = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();
            foreach (var typeDecl in typeDecls)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (symbol == null) continue;

                var fullName = symbol.ToDisplayString();
                if (string.Equals(fullName, typeName, StringComparison.Ordinal))
                    return symbol;

                // Match on name only if no namespace qualifier
                if (!typeName.Contains('.') &&
                    string.Equals(symbol.Name, typeName, StringComparison.Ordinal))
                    return symbol;
            }
        }

        return null;
    }

    private static List<INamedTypeSymbol> GetAllServiceTypes(CSharpCompilation compilation)
    {
        var types = new List<INamedTypeSymbol>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var typeDecls = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();
            foreach (var typeDecl in typeDecls)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (symbol != null)
                    types.Add(symbol);
            }
        }
        return types;
    }

    private static string InferTestNamespace(string serviceNamespace)
    {
        // If the service is in a project namespace like "MyApp.Services",
        // the test namespace would be "MyApp.Services.Tests"
        if (serviceNamespace.EndsWith(".Tests", StringComparison.Ordinal))
            return serviceNamespace;

        // If the namespace already contains "Tests", keep it
        if (serviceNamespace.Contains(".Tests"))
            return serviceNamespace;

        return $"{serviceNamespace}.Tests";
    }

    /// <summary>
    /// Infers the test namespace from a test project path.
    /// Reads the root namespace from the test project's csproj (RootNamespace property)
    /// or falls back to the assembly name.
    /// </summary>
    private static string InferTestNamespaceFromProject(string? testProjectPath, string serviceNamespace)
    {
        if (string.IsNullOrEmpty(testProjectPath) || !File.Exists(testProjectPath))
            return InferTestNamespace(serviceNamespace);

        try
        {
            var content = File.ReadAllText(testProjectPath);
            // Try to extract RootNamespace from csproj
            var rootNsMatch = System.Text.RegularExpressions.Regex.Match(
                content, @"<RootNamespace>([^<]+)</RootNamespace>");
            if (rootNsMatch.Success)
                return rootNsMatch.Groups[1].Value;

            // Fall back to assembly name (file name without extension)
            var assemblyName = Path.GetFileNameWithoutExtension(testProjectPath);
            if (!string.IsNullOrEmpty(assemblyName))
                return assemblyName;
        }
        catch
        {
            // Fall through to default inference
        }

        return InferTestNamespace(serviceNamespace);
    }

    /// <summary>
    /// Infers a default output path based on the test project conventions.
    /// </summary>
    private static string InferOutputPath(string? testProjectPath, string testClassName)
    {
        if (!string.IsNullOrEmpty(testProjectPath))
        {
            var testProjectDir = Path.GetDirectoryName(testProjectPath);
            if (!string.IsNullOrEmpty(testProjectDir))
                return Path.Combine(testProjectDir, $"{testClassName}.cs");
        }

        return Path.Combine(Directory.GetCurrentDirectory(), $"{testClassName}.cs");
    }

    internal static string GenerateTestClassSource(
        string testClassName,
        string testNamespace,
        string serviceTypeFull,
        List<ScaffoldDependency> dependencies,
        string testFramework,
        string mocking,
        string assertions)
    {
        var sb = new System.Text.StringBuilder();

        // Usings
        sb.AppendLine("using IoCTools.Testing.Annotations;");

        if (testFramework == "xunit")
            sb.AppendLine("using Xunit;");
        else if (testFramework == "nunit")
            sb.AppendLine("using NUnit.Framework;");
        else if (testFramework == "mstest")
        {
            sb.AppendLine("using Microsoft.VisualStudio.TestTools.UnitTesting;");
        }

        if (assertions == "fluentassertions")
            sb.AppendLine("using FluentAssertions;");
        else if (assertions == "shouldly")
            sb.AppendLine("using Shouldly;");

        sb.AppendLine();

        // Namespace
        if (!string.IsNullOrEmpty(testNamespace))
        {
            sb.AppendLine($"namespace {testNamespace};");
            sb.AppendLine();
        }

        // Class declaration
        sb.AppendLine($"// <auto-generated />");
        sb.AppendLine($"[Cover<{serviceTypeFull}>(Logger = FixtureLoggerProfile.NullLogger)]");
        sb.AppendLine($"public partial class {testClassName}");
        sb.AppendLine("{");

        // Smoke test attribute
        var testAttribute = testFramework switch
        {
            "nunit" => "[Test]",
            "mstest" => "[TestMethod]",
            _ => "[Fact]"
        };

        // Smoke test assertion
        var assertLine = assertions switch
        {
            "fluentassertions" => "Sut.Should().NotBeNull();",
            "shouldly" => "Sut.ShouldNotBeNull();",
            _ when testFramework == "mstest" => "Assert.IsNotNull(Sut);",
            _ => "Assert.NotNull(Sut);"
        };

        // Method visibility
        var visibility = testFramework == "mstest" ? "public" : "public";

        sb.AppendLine($"    {testAttribute}");
        sb.AppendLine($"    {visibility} void Sut_ShouldConstruct()");
        sb.AppendLine("    {");
        sb.AppendLine($"        {assertLine}");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Gets the mock field name for a type, mirroring TypeNameUtilities.GetSimpleTypeName logic.
    /// </summary>
    private static string GetMockFieldName(ITypeSymbol type)
    {
        var baseName = GetSimpleTypeName(type);
        return $"_mock{baseName}";
    }

    private static string GetSimpleTypeName(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var genericArgs = string.Join(null, namedType.TypeArguments.Select(GetSimpleTypeName));
            var baseName = StripInterfacePrefix(namedType.Name);
            return $"{baseName}{genericArgs}";
        }
        return StripInterfacePrefix(type.Name);
    }

    private static string StripInterfacePrefix(string name)
    {
        if (name.StartsWith("II", StringComparison.Ordinal) && name.Length > 2 && char.IsUpper(name[2]))
            return name.Substring(1);
        if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
            return name.Substring(1);
        return name;
    }
}

/// <summary>
/// Describes a service constructor dependency for scaffold output.
/// </summary>
internal sealed record ScaffoldDependency(
    string TypeName,
    string ParameterName,
    string FieldName);

/// <summary>
/// Result model for the test scaffold command.
/// </summary>
internal sealed record ScaffoldResult(
    string ServiceType,
    string TestClassName,
    string OutputPath,
    IReadOnlyList<ScaffoldDependency> Dependencies,
    string Source);
