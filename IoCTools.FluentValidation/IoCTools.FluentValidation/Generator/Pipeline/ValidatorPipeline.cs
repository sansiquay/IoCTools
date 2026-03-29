namespace IoCTools.FluentValidation.Generator.Pipeline;

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Models;
using Utilities;

/// <summary>
/// Incremental pipeline for discovering FluentValidation validators with IoCTools lifetime attributes.
/// </summary>
internal static class ValidatorPipeline
{
    /// <summary>
    /// Builds an <see cref="IncrementalValuesProvider{T}"/> that discovers validators
    /// inheriting AbstractValidator&lt;T&gt; with IoCTools lifetime attributes.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    /// <returns>A collected provider of <see cref="ValidatorClassInfo"/>.</returns>
    internal static IncrementalValueProvider<ImmutableArray<ValidatorClassInfo>> Build(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) =>
                {
                    var typeDecl = (TypeDeclarationSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                    if (symbol == null || symbol.IsAbstract || symbol.IsStatic || symbol.TypeKind != TypeKind.Class)
                        return (ValidatorClassInfo?)null;

                    // Per D-07: Must have IoCTools lifetime attribute
                    if (!FluentValidationTypeChecker.HasLifetimeAttribute(symbol))
                        return null;

                    // Per D-07: Must inherit from AbstractValidator<T>
                    var validatedType = FluentValidationTypeChecker.GetAbstractValidatorTypeArgument(symbol);
                    if (validatedType == null)
                        return null;

                    var lifetime = FluentValidationTypeChecker.GetLifetimeFromAttributes(symbol);

                    return new ValidatorClassInfo(
                        symbol,
                        typeDecl,
                        ctx.SemanticModel,
                        validatedType,
                        lifetime);
                })
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x!.Value)
            .Collect();
    }
}
