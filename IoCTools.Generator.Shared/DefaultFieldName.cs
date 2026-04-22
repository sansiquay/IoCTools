namespace IoCTools.Generator.Shared;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

/// <summary>
///     Computes the default field name IoCTools' generator would emit for a dependency
///     of a given type. Used by <see cref="InjectMigrationRewriter" /> (and the CLI
///     <c>migrate-inject</c> subcommand) to decide whether an <c>[Inject]</c> field
///     uses the default name (in which case the migration can skip the
///     <c>memberName{N}</c> override).
/// </summary>
/// <remarks>
///     Mirrors the logic in <c>IoCTools.Generator.Utilities.AttributeParser.GenerateFieldName</c>
///     combined with <c>TypeUtilities.GetMeaningfulTypeName</c>. The two helpers are
///     physically separate (generator targets netstandard2.0 with Roslyn references;
///     this shared library is source-linked into the analyzer project). Any change to
///     the generator's naming logic MUST be mirrored here — covered by the round-trip
///     tests in <c>DefaultFieldNameTests</c>.
/// </remarks>
public static class DefaultFieldName
{
    private static readonly HashSet<string> CollectionTypeNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "IEnumerable",
        "IList",
        "ICollection",
        "List",
        "IReadOnlyList",
        "IReadOnlyCollection",
        "Array",
    };

    private static readonly HashSet<string> ReservedKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
        "checked", "class", "const", "continue", "decimal", "default", "delegate",
        "do", "double", "else", "enum", "event", "explicit", "extern", "false",
        "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
        "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "private",
        "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    /// <summary>
    ///     Computes the default field name for a dependency of the given type under
    ///     the given options. Default options match the generator's defaults:
    ///     CamelCase, stripI=true, prefix="_".
    /// </summary>
    public static string Compute(
        ITypeSymbol type,
        string namingConvention = "CamelCase",
        bool stripI = true,
        string prefix = "_")
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        var meaningfulName = GetMeaningfulTypeName(type);
        return Compute(meaningfulName, namingConvention, stripI, prefix);
    }

    /// <summary>
    ///     Convenience overload for callers that have already extracted a raw type
    ///     name (bypasses <see cref="GetMeaningfulTypeName" />).
    /// </summary>
    public static string Compute(
        string typeName,
        string namingConvention = "CamelCase",
        bool stripI = true,
        string prefix = "_")
    {
        if (typeName == null) throw new ArgumentNullException(nameof(typeName));
        var fieldBaseName = ExtractSemanticFieldName(typeName);
        var fieldName = ApplyPrefixToFieldName(fieldBaseName, namingConvention, prefix);
        return EscapeReservedKeyword(fieldName);
    }

    /// <summary>
    ///     Mirrors <c>TypeUtilities.GetMeaningfulTypeName</c>. Arrays become
    ///     <c>{Element}Array</c>; collection generics unwrap to their first type
    ///     argument. Everything else returns the symbol's raw name.
    /// </summary>
    public static string GetMeaningfulTypeName(ITypeSymbol type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        if (type is IArrayTypeSymbol arrayType)
            return GetMeaningfulTypeName(arrayType.ElementType) + "Array";

        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            if (CollectionTypeNames.Contains(named.Name) && named.TypeArguments.Length > 0)
                return GetMeaningfulTypeName(named.TypeArguments[0]);
        }

        return type.Name;
    }

    private static string ExtractSemanticFieldName(string originalTypeName)
    {
        // Always strip a leading 'I' when followed by an uppercase letter — this is
        // the interface-naming convention. stripI=false does NOT short-circuit here
        // because the generator also applies semantic naming unconditionally and
        // lets the naming convention adjust casing.
        if (originalTypeName.Length > 1 && originalTypeName[0] == 'I' && char.IsUpper(originalTypeName[1]))
            return originalTypeName.Substring(1);
        return originalTypeName;
    }

    private static string ApplyNamingConvention(string name, string namingConvention)
    {
        if (string.IsNullOrEmpty(name)) return name;
        switch (namingConvention)
        {
            case "CamelCase":
                return char.ToLowerInvariant(name[0]) + name.Substring(1);
            case "PascalCase":
                return char.ToUpperInvariant(name[0]) + name.Substring(1);
            case "SnakeCase":
                return Regex.Replace(name, @"(?<!^)([A-Z])", "_$1").ToLower();
            default:
                return name;
        }
    }

    private static string ApplyPrefixToFieldName(string fieldBaseName, string namingConvention, string prefix)
    {
        if (prefix == null) prefix = string.Empty;

        if (prefix.Length == 0)
            return ApplyNamingConvention(fieldBaseName, namingConvention);

        if (prefix == "_")
            return "_" + ApplyNamingConvention(fieldBaseName, namingConvention);

        if (prefix.EndsWith("_", StringComparison.Ordinal))
            return prefix + ApplyNamingConvention(fieldBaseName, namingConvention);

        // Custom prefix not ending with underscore: combine then apply convention, then re-add underscore.
        var combined = prefix + fieldBaseName;
        return "_" + ApplyNamingConvention(combined, namingConvention);
    }

    private static string EscapeReservedKeyword(string identifier)
    {
        if (ReservedKeywords.Contains(identifier)) return identifier + "Value";
        return identifier;
    }
}
