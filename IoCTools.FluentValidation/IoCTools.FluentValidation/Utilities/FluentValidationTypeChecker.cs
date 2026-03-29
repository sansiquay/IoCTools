namespace IoCTools.FluentValidation.Utilities;

using Microsoft.CodeAnalysis;

/// <summary>
/// Utility methods for detecting FluentValidation types by name.
/// Uses name-based detection to avoid requiring a FluentValidation package reference
/// in the generator project itself.
/// </summary>
internal static class FluentValidationTypeChecker
{
    private const string AbstractValidatorName = "AbstractValidator";
    private const string FluentValidationNamespace = "FluentValidation";
    private const string ValidatorInterfaceName = "IValidator";
    private const string IoCToolsAnnotationsNamespace = "IoCTools.Abstractions.Annotations";

    /// <summary>
    /// Walks the base type chain of the given class to find AbstractValidator&lt;T&gt;
    /// and returns the type argument T.
    /// </summary>
    /// <param name="classSymbol">The class to check.</param>
    /// <returns>The validated type T if the class extends AbstractValidator&lt;T&gt;, null otherwise.</returns>
    public static INamedTypeSymbol? GetAbstractValidatorTypeArgument(INamedTypeSymbol classSymbol)
    {
        var current = classSymbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (current.Name == AbstractValidatorName &&
                current.ContainingNamespace?.ToDisplayString() == FluentValidationNamespace &&
                current.IsGenericType &&
                current.TypeArguments.Length == 1)
            {
                return current.TypeArguments[0] as INamedTypeSymbol;
            }

            current = current.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Returns true if the given class extends AbstractValidator&lt;T&gt;.
    /// </summary>
    /// <param name="classSymbol">The class to check.</param>
    /// <returns>True if the class is a FluentValidation validator.</returns>
    public static bool IsAbstractValidator(INamedTypeSymbol classSymbol) =>
        GetAbstractValidatorTypeArgument(classSymbol) != null;

    /// <summary>
    /// Checks the class for IoCTools lifetime attributes and returns the lifetime string.
    /// </summary>
    /// <param name="classSymbol">The class to check.</param>
    /// <returns>"Scoped", "Singleton", "Transient", or null if no lifetime attribute.</returns>
    public static string? GetLifetimeFromAttributes(INamedTypeSymbol classSymbol)
    {
        foreach (var attribute in classSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass == null)
                continue;

            var containingNamespace = attrClass.ContainingNamespace?.ToDisplayString();
            if (containingNamespace != IoCToolsAnnotationsNamespace)
                continue;

            switch (attrClass.Name)
            {
                case "ScopedAttribute":
                    return "Scoped";
                case "SingletonAttribute":
                    return "Singleton";
                case "TransientAttribute":
                    return "Transient";
            }
        }

        return null;
    }

    /// <summary>
    /// Returns true if the class has any IoCTools lifetime attribute.
    /// </summary>
    /// <param name="classSymbol">The class to check.</param>
    /// <returns>True if the class has a lifetime attribute.</returns>
    public static bool HasLifetimeAttribute(INamedTypeSymbol classSymbol) =>
        GetLifetimeFromAttributes(classSymbol) != null;

    /// <summary>
    /// Returns true if the given interface symbol is FluentValidation's IValidator&lt;T&gt;.
    /// Used for D-08 registration refinement.
    /// </summary>
    /// <param name="interfaceSymbol">The interface to check.</param>
    /// <returns>True if the interface is IValidator&lt;T&gt;.</returns>
    public static bool IsValidatorInterface(INamedTypeSymbol interfaceSymbol) =>
        interfaceSymbol.Name == ValidatorInterfaceName &&
        interfaceSymbol.ContainingNamespace?.ToDisplayString() == FluentValidationNamespace &&
        interfaceSymbol.IsGenericType &&
        interfaceSymbol.TypeArguments.Length == 1;
}
