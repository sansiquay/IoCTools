namespace IoCTools.Testing.Generator.Pipeline;

using System.Collections.Immutable;
using System.Linq;
using IoCTools.Testing.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class TestFixturePipeline
{
    internal static IncrementalValuesProvider<TestClassInfo> Build(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) =>
                {
                    var typeDecl = (TypeDeclarationSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl);
                    if (symbol == null || symbol.IsStatic || symbol.TypeKind != TypeKind.Class)
                        return (TestClassInfo?)null;

                    // Check for partial keyword
                    if (!typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                        return (TestClassInfo?)null;

                    // Check for [Cover<T>] attribute
                    var coverAttr = symbol.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.Name == "CoverAttribute" &&
                                            a.AttributeClass?.ToDisplayString().Contains("IoCTools.Testing") == true);

                    if (coverAttr == null)
                        return (TestClassInfo?)null;

                    // Extract TService generic type argument
                    if (coverAttr.AttributeClass is INamedTypeSymbol namedAttr &&
                        namedAttr.TypeArguments.Length > 0 &&
                        namedAttr.TypeArguments[0] is INamedTypeSymbol serviceSymbol)
                    {
                        return new TestClassInfo(symbol, typeDecl, ctx.SemanticModel, serviceSymbol);
                    }

                    return (TestClassInfo?)null;
                })
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x!.Value)
            .Collect()
            .SelectMany(static (tests, _) =>
                tests
                    .GroupBy(t => t.TestClassSymbol, SymbolEqualityComparer.Default)
                    .Select(g => g.First())
                    .ToImmutableArray());
    }
}
