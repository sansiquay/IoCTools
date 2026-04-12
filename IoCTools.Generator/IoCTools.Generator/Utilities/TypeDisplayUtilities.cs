namespace IoCTools.Generator.Utilities;

using System.Text.RegularExpressions;

internal static class TypeDisplayUtilities
{
    /// <summary>
    ///     Extracts and sorts namespaces from a collection, filtering out empty entries.
    /// </summary>
    private static IEnumerable<string> GetSortedNamespaces(IEnumerable<string>? namespaces)
    {
        return (namespaces ?? Array.Empty<string>()).Where(ns => !string.IsNullOrEmpty(ns))
            .OrderByDescending(ns => ns.Length);
    }

    /// <summary>
    ///     Strips namespaces from a type name using sorted namespace list.
    ///     Sorts by length (longest first) to correctly handle nested namespaces.
    /// </summary>
    private static string StripNamespacesFromDisplayString(string fullTypeName, IEnumerable<string>? namespacesToStrip)
    {
        var sortedNamespaces = GetSortedNamespaces(namespacesToStrip);

        foreach (var ns in sortedNamespaces)
        {
            if (fullTypeName.StartsWith($"{ns}.", StringComparison.Ordinal))
                fullTypeName = fullTypeName.Substring(ns.Length + 1);
            fullTypeName = Regex.Replace(fullTypeName, $@"\b{Regex.Escape(ns)}\.", "");
        }

        return fullTypeName;
    }

    /// <summary>
    ///     Handles array type recursion by applying namespace stripping to element type.
    /// </summary>
    private static string ProcessArrayType(IArrayTypeSymbol arrayType, IEnumerable<string>? namespacesToStrip, bool forServiceRegistration)
    {
        var elementTypeName = WithoutNamespaces(arrayType.ElementType, namespacesToStrip, forServiceRegistration);
        var ranks = new string(',', arrayType.Rank - 1);
        return $"{elementTypeName}[{ranks}]";
    }

    // Mirrors ConstructorGenerator.Namespaces.RemoveNamespacesAndDots behavior
    public static string WithoutNamespaces(ITypeSymbol typeSymbol,
        IEnumerable<string>? namespacesToStrip,
        bool includeNullable = true)
    {
        if (typeSymbol == null) return "object";

        if (typeSymbol is IArrayTypeSymbol arrayType)
            return ProcessArrayType(arrayType, namespacesToStrip, forServiceRegistration: false);

        var misc = includeNullable
            ? SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
              SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            : SymbolDisplayMiscellaneousOptions.UseSpecialTypes;

        var format = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
            miscellaneousOptions: misc);

        var fullTypeName = typeSymbol.ToDisplayString(format);
        return StripNamespacesFromDisplayString(fullTypeName, namespacesToStrip);
    }

    // Mirrors ServiceRegistrationGenerator.Rendering.RemoveNamespacesAndDots behavior
    public static string WithoutNamespaces(ISymbol serviceType,
        IEnumerable<string>? uniqueNamespaces,
        bool forServiceRegistration)
    {
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

        if (serviceType is IArrayTypeSymbol arrayType)
            return ProcessArrayType(arrayType, uniqueNamespaces, forServiceRegistration);

        var qualificationStyle = forServiceRegistration
            ? SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces
            : SymbolDisplayTypeQualificationStyle.NameAndContainingTypes;

        var miscOptions = forServiceRegistration
            ? SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            : SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
              SymbolDisplayMiscellaneousOptions.UseSpecialTypes;

        var format = new SymbolDisplayFormat(
            typeQualificationStyle: qualificationStyle,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
            miscellaneousOptions: miscOptions);

        var fullTypeName = serviceType.ToDisplayString(format);
        return StripNamespacesFromDisplayString(fullTypeName, uniqueNamespaces);
    }
}
