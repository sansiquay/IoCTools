namespace IoCTools.FluentValidation.Models;

using System;
using System.Collections.Immutable;
using System.Linq;

using Generator.CompositionGraph;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Immutable pipeline model carrying information about a FluentValidation validator class.
/// Used across discovery, registration refinement, and diagnostic stages.
/// </summary>
internal readonly struct ValidatorClassInfo : IEquatable<ValidatorClassInfo>
{
    /// <summary>
    /// Initializes a new instance of <see cref="ValidatorClassInfo"/>.
    /// </summary>
    /// <param name="classSymbol">The validator class symbol.</param>
    /// <param name="classDeclaration">The syntax node for the validator class.</param>
    /// <param name="semanticModel">The semantic model for later analysis.</param>
    /// <param name="validatedType">The T from AbstractValidator&lt;T&gt;.</param>
    /// <param name="lifetime">The IoCTools lifetime attribute value, or null if none.</param>
    /// <param name="compositionEdges">Edges to child validators discovered in the class body.</param>
    public ValidatorClassInfo(
        INamedTypeSymbol classSymbol,
        TypeDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        INamedTypeSymbol validatedType,
        string? lifetime,
        ImmutableArray<CompositionEdge> compositionEdges = default)
    {
        ClassSymbol = classSymbol;
        ClassDeclaration = classDeclaration;
        SemanticModel = semanticModel;
        ValidatedType = validatedType;
        Lifetime = lifetime;
        CompositionEdges = compositionEdges.IsDefault ? ImmutableArray<CompositionEdge>.Empty : compositionEdges;
        FullyQualifiedName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        ValidatedTypeFullyQualifiedName = validatedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// The validator class symbol.
    /// </summary>
    public INamedTypeSymbol ClassSymbol { get; }

    /// <summary>
    /// The syntax node for the validator class declaration.
    /// </summary>
    public TypeDeclarationSyntax ClassDeclaration { get; }

    /// <summary>
    /// The semantic model for further analysis.
    /// </summary>
    public SemanticModel SemanticModel { get; }

    /// <summary>
    /// The T from AbstractValidator&lt;T&gt; — the type being validated.
    /// </summary>
    public INamedTypeSymbol ValidatedType { get; }

    /// <summary>
    /// The IoCTools lifetime: "Scoped", "Singleton", "Transient", or null if no lifetime attribute.
    /// </summary>
    public string? Lifetime { get; }

    /// <summary>
    /// Composition graph edges representing child validators invoked via
    /// SetValidator, Include, or SetInheritanceValidator.
    /// </summary>
    public ImmutableArray<CompositionEdge> CompositionEdges { get; }

    /// <summary>
    /// Cached fully qualified name of the validator class.
    /// </summary>
    public string FullyQualifiedName { get; }

    /// <summary>
    /// Cached fully qualified name of the validated type (T).
    /// </summary>
    public string ValidatedTypeFullyQualifiedName { get; }

    /// <summary>
    /// Compares by fully qualified names for incremental pipeline caching.
    /// </summary>
    public bool Equals(ValidatorClassInfo other) =>
        FullyQualifiedName == other.FullyQualifiedName &&
        ValidatedTypeFullyQualifiedName == other.ValidatedTypeFullyQualifiedName &&
        CompositionEdges.SequenceEqual(other.CompositionEdges);

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is ValidatorClassInfo other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 397 + (FullyQualifiedName?.GetHashCode() ?? 0);
            hash = hash * 397 + (ValidatedTypeFullyQualifiedName?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public static bool operator ==(ValidatorClassInfo left, ValidatorClassInfo right) =>
        left.Equals(right);

    public static bool operator !=(ValidatorClassInfo left, ValidatorClassInfo right) =>
        !left.Equals(right);
}
