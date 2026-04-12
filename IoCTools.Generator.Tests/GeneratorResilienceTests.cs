namespace IoCTools.Generator.Tests;

using System.Reflection;

using IoCTools.Generator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using FluentAssertions;

public sealed class GeneratorResilienceTests
{
    [Fact]
    public void GetInterfacesForRegistration_WhenInterfaceTraversalThrows_ReportsIOC093AndReturnsEmpty()
    {
        var compilation = CreateCompilation("""
                                          using IoCTools.Abstractions.Annotations;

                                          namespace Test;

                                          public interface IService { }

                                          [Scoped]
                                          public partial class MyService : IService { }
                                          """);

        var classSymbol = compilation.GetTypeByMetadataName("Test.MyService");
        classSymbol.Should().NotBeNull();

        var diagnostics = new List<Diagnostic>();
        var result = InvokeGetInterfacesForRegistration(classSymbol!, diagnostics,
            static _ => throw new InvalidOperationException("boom"));

        result.Should().BeEmpty();
        diagnostics.Should().ContainSingle(d => d.Id == "IOC093");
    }

    [Fact]
    public void ResolveDeclaredClassSymbol_WithMismatchedSemanticModel_ReportsIOC093()
    {
        var firstTree = CSharpSyntaxTree.ParseText("""
                                                   namespace Test;

                                                   public partial class MyService { }
                                                   """);
        var secondTree = CSharpSyntaxTree.ParseText("""
                                                    namespace Other;

                                                    public class Placeholder { }
                                                    """);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { firstTree, secondTree },
            SourceGeneratorTestHelper.GetStandardReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var mismatchedModel = compilation.GetSemanticModel(secondTree);
        var classDeclaration = firstTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().Single();
        var diagnostics = new List<Diagnostic>();

        var symbol = InvokeResolveDeclaredClassSymbol(classDeclaration, mismatchedModel, diagnostics);

        symbol.Should().BeNull();
        diagnostics.Should().ContainSingle(d => d.Id == "IOC093");
    }

    private static Compilation CreateCompilation(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            SourceGeneratorTestHelper.GetStandardReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static IReadOnlyList<INamedTypeSymbol> InvokeGetInterfacesForRegistration(
        INamedTypeSymbol classSymbol,
        ICollection<Diagnostic> diagnostics,
        Func<INamedTypeSymbol, IEnumerable<INamedTypeSymbol>> interfaceProvider)
    {
        var method = GetGeneratorAssembly().GetType("IoCTools.Generator.Generator.RegistrationSelector")
            ?.GetMethod(
            "GetInterfacesForRegistration",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("the resilience wrapper should exist");

        var result = method!.Invoke(
            null,
            new object?[]
            {
                classSymbol,
                new Action<Diagnostic>(diagnostics.Add),
                interfaceProvider
            });

        return ((IEnumerable<INamedTypeSymbol>)result!).ToList();
    }

    private static INamedTypeSymbol? InvokeResolveDeclaredClassSymbol(
        TypeDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        ICollection<Diagnostic> diagnostics)
    {
        var method = GetGeneratorAssembly().GetType("IoCTools.Generator.CodeGeneration.ConstructorGenerator")
            ?.GetMethod("ResolveDeclaredClassSymbol", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("the semantic model recovery helper should exist");

        var result = method!.Invoke(
            null,
            new object?[]
            {
                classDeclaration,
                semanticModel,
                new Action<Diagnostic>(diagnostics.Add)
            });

        return (INamedTypeSymbol?)result;
    }

    private static Assembly GetGeneratorAssembly() => typeof(DependencyInjectionGenerator).Assembly;
}
