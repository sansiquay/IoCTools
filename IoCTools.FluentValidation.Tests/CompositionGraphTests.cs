namespace IoCTools.FluentValidation.Tests;

using System.Collections.Immutable;
using System.Linq;

using FluentAssertions;

using IoCTools.FluentValidation.Generator.CompositionGraph;
using IoCTools.FluentValidation.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Tests for composition graph edge creation and integration with diagnostic validators.
/// Verifies that CompositionEdge structures correctly represent validator composition patterns.
/// Note: Full integration with CompositionGraphBuilder will be available after Plan 04 merge.
/// </summary>
public sealed class CompositionGraphTests
{
    #region CompositionEdge Structure

    [Fact]
    public void SetValidator_NewInstance_CreatesDirectInstantiationEdge()
    {
        // Arrange
        var edge = new CompositionEdge(
            parentValidatorName: "global::TestApp.OrderValidator",
            childValidatorName: "global::TestApp.AddressValidator",
            childValidatorTypeName: "AddressValidator",
            compositionType: CompositionType.SetValidator,
            isDirectInstantiation: true,
            location: null);

        // Assert
        edge.IsDirectInstantiation.Should().BeTrue("new instance indicates direct instantiation");
        edge.CompositionType.Should().Be(CompositionType.SetValidator);
        edge.ParentValidatorName.Should().Contain("OrderValidator");
        edge.ChildValidatorName.Should().Contain("AddressValidator");
        edge.ChildValidatorTypeName.Should().Be("AddressValidator");
    }

    [Fact]
    public void SetValidator_InjectedField_CreatesInjectedEdge()
    {
        // Arrange
        var edge = new CompositionEdge(
            parentValidatorName: "global::TestApp.OrderValidator",
            childValidatorName: "global::TestApp.AddressValidator",
            childValidatorTypeName: "AddressValidator",
            compositionType: CompositionType.SetValidator,
            isDirectInstantiation: false,
            location: null);

        // Assert
        edge.IsDirectInstantiation.Should().BeFalse("injected field is not direct instantiation");
        edge.CompositionType.Should().Be(CompositionType.SetValidator);
    }

    [Fact]
    public void SetInheritanceValidator_WithAddCalls_CreatesEdges()
    {
        // Arrange - SetInheritanceValidator with Add<T> pattern
        var edge = new CompositionEdge(
            parentValidatorName: "global::TestApp.AnimalValidator",
            childValidatorName: "global::TestApp.DogValidator",
            childValidatorTypeName: "DogValidator",
            compositionType: CompositionType.SetInheritanceValidator,
            isDirectInstantiation: true,
            location: null);

        // Assert
        edge.CompositionType.Should().Be(CompositionType.SetInheritanceValidator);
        edge.IsDirectInstantiation.Should().BeTrue("v.Add<Dog>(new DogValidator()) is direct instantiation");
        edge.ChildValidatorTypeName.Should().Be("DogValidator");
    }

    #endregion

    #region Edge Equality

    [Fact]
    public void CompositionEdge_SameValues_AreEqual()
    {
        // Arrange
        var edge1 = new CompositionEdge("Parent", "Child", "Child", CompositionType.SetValidator, true, null);
        var edge2 = new CompositionEdge("Parent", "Child", "Child", CompositionType.SetValidator, true, null);

        // Assert
        edge1.Should().Be(edge2);
        (edge1 == edge2).Should().BeTrue();
    }

    [Fact]
    public void CompositionEdge_DifferentInstantiationType_AreNotEqual()
    {
        // Arrange
        var edge1 = new CompositionEdge("Parent", "Child", "Child", CompositionType.SetValidator, true, null);
        var edge2 = new CompositionEdge("Parent", "Child", "Child", CompositionType.SetValidator, false, null);

        // Assert
        edge1.Should().NotBe(edge2);
        (edge1 != edge2).Should().BeTrue();
    }

    #endregion

    #region ValidatorClassInfo with CompositionEdges

    [Fact]
    public void ValidatorClassInfo_DefaultCompositionEdges_IsEmpty()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class Order { }

[Scoped]
public partial class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator() { }
}
";

        // Act
        var compilation = CreateCompilation(source);
        var symbol = FindType(compilation, "OrderValidator");
        var decl = FindTypeDeclaration(compilation, "OrderValidator");
        var validatedType = GetAbstractValidatorTypeArg(symbol!);

        var info = new ValidatorClassInfo(
            symbol!, decl!, compilation.GetSemanticModel(compilation.SyntaxTrees.First()),
            validatedType!, "Scoped");

        // Assert
        info.CompositionEdges.Should().BeEmpty("default composition edges should be empty");
    }

    [Fact]
    public void ValidatorClassInfo_WithCompositionEdges_RetainsEdges()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class Order { }

[Scoped]
public partial class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator() { }
}
";

        var compilation = CreateCompilation(source);
        var symbol = FindType(compilation, "OrderValidator");
        var decl = FindTypeDeclaration(compilation, "OrderValidator");
        var validatedType = GetAbstractValidatorTypeArg(symbol!);

        var edge = new CompositionEdge("Parent", "Child", "ChildType", CompositionType.Include, true, null);

        // Act
        var info = new ValidatorClassInfo(
            symbol!, decl!, compilation.GetSemanticModel(compilation.SyntaxTrees.First()),
            validatedType!, "Scoped", ImmutableArray.Create(edge));

        // Assert
        info.CompositionEdges.Should().HaveCount(1);
        info.CompositionEdges[0].CompositionType.Should().Be(CompositionType.Include);
    }

    #endregion

    #region Test Infrastructure

    private static CSharpCompilation CreateCompilation(string source)
    {
        var iocToolsAssembly = typeof(IoCTools.Abstractions.Annotations.ScopedAttribute).Assembly;
        var refs = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(iocToolsAssembly.Location),
        };

        var allRefs = refs.ToList();
        try
        {
            var fvAssembly = typeof(global::FluentValidation.AbstractValidator<>).Assembly;
            allRefs.Add(MetadataReference.CreateFromFile(fvAssembly.Location));
        }
        catch { /* FluentValidation may not be available */ }

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        return CSharpCompilation.Create(
            "CompositionGraphTest",
            new[] { syntaxTree },
            allRefs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static INamedTypeSymbol? FindType(CSharpCompilation compilation, string simpleName)
    {
        return compilation.GetSymbolsWithName(simpleName, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault();
    }

    private static TypeDeclarationSyntax? FindTypeDeclaration(CSharpCompilation compilation, string simpleName)
    {
        return compilation.SyntaxTrees
            .SelectMany(t => t.GetRoot().DescendantNodes())
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(td => td.Identifier.Text == simpleName);
    }

    private static INamedTypeSymbol? GetAbstractValidatorTypeArg(INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.IsGenericType &&
                current.OriginalDefinition.ToDisplayString().Contains("AbstractValidator"))
            {
                return current.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
            }
            current = current.BaseType;
        }
        return null;
    }

    #endregion
}
