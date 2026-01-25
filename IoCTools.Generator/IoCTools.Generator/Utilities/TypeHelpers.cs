namespace IoCTools.Generator.Utilities;

internal static class TypeHelpers
{
    internal static bool IsFrameworkTypeAdapted(string typeName) => FrameworkTypeUtilities.IsFrameworkType(typeName);

    internal static bool IsCollectionTypeAdapted(string typeName) =>
        CollectionUtilities.IsCollectionTypeAdapted(typeName);

    internal static string? ExtractServiceNameFromType(string dependencyType) =>
        TypeNameUtilities.ExtractServiceNameFromType(dependencyType);

    internal static string FormatTypeNameForDiagnostic(ITypeSymbol typeSymbol) =>
        TypeNameUtilities.FormatTypeNameForDiagnostic(typeSymbol);

    internal static string? ExtractInnerTypeFromIEnumerable(string enumerableTypeName) =>
        CollectionUtilities.ExtractInnerTypeFromIEnumerable(enumerableTypeName);

    internal static EnumerableTypeInfo? ExtractIEnumerableFromGenericArguments(string typeName) =>
        CollectionUtilities.ExtractIEnumerableFromGenericArguments(typeName);

    internal static EnumerableTypeInfo? ExtractIEnumerableFromWrappedType(string typeName) =>
        CollectionUtilities.ExtractIEnumerableFromWrappedType(typeName);

    internal static bool IsConstructedGenericTypeSimple(string typeName) =>
        typeName.Contains('<') && typeName.Contains('>') && !typeName.EndsWith("<>", StringComparison.Ordinal);

    internal static string ExtractBaseTypeNameFromConstructed(string constructedType)
    {
        var angleIndex = constructedType.IndexOf('<');
        return angleIndex >= 0 ? constructedType.Substring(0, angleIndex) : constructedType;
    }

    internal static string? ExtractBaseGenericInterface(string constructedType)
    {
        // Simple helper: converts "IService<string>" to "IService<T>"
        // This is used for IEnumerable<T> inner type resolution
        // For full Roslyn-based resolution, use INamedTypeSymbol.ConstructedFrom.ToDisplayString()
        if (!constructedType.Contains('<') || !constructedType.Contains('>')) return null;

        var angleStart = constructedType.IndexOf('<');
        var baseName = constructedType.Substring(0, angleStart);

        // Count type parameters to determine generic arity
        var typeArgsString = constructedType.Substring(angleStart + 1, constructedType.LastIndexOf('>') - angleStart - 1);
        var paramCount = CountTopLevelTypeParameters(typeArgsString);

        // Build open generic name (IService<T> or IService<T1, T2>, etc.)
        if (paramCount == 1)
            return $"{baseName}<T>";
        else
        {
            var typeParams = new List<string>();
            for (int i = 0; i < paramCount; i++)
                typeParams.Add(i == 0 ? "T" : $"T{i + 1}");
            return $"{baseName}<{string.Join(", ", typeParams)}>";
        }
    }

    internal static int CountTopLevelTypeParameters(string typeParamsString)
    {
        if (string.IsNullOrWhiteSpace(typeParamsString)) return 0;
        var count = 0;
        var depth = 0;
        var lastStart = 0;
        for (var i = 0; i < typeParamsString.Length; i++)
        {
            var c = typeParamsString[i];
            if (c == '<')
            {
                depth++;
            }
            else if (c == '>')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                count++;
                lastStart = i + 1;
            }
        }

        if (lastStart < typeParamsString.Length && typeParamsString.Substring(lastStart).Trim().Length > 0) count++;
        return count;
    }

    internal static string ExtractSimpleTypeNameFromFullName(string fullTypeName) =>
        TypeNameUtilities.ExtractSimpleTypeNameFromFullName(fullTypeName);

    internal class EnumerableTypeInfo
    {
        public EnumerableTypeInfo(string innerType,
            string fullEnumerableType)
        {
            InnerType = innerType;
            FullEnumerableType = fullEnumerableType;
        }

        public string InnerType { get; }
        public string FullEnumerableType { get; }
    }
}
