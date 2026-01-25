namespace IoCTools.Generator.Utilities;

/// <summary>
///     Centralized registry for configuration-related type checking.
///     Consolidates duplicated type lists from ConfigurationValidator and ConfigurationInjectionInfo.
/// </summary>
internal static class ConfigurationTypeRegistry
{
    /// <summary>
    ///     Types that can be bound from configuration without issues.
    ///     Primitive types and common value types.
    /// </summary>
    public static readonly HashSet<string> SupportedPrimitiveTypes = new(StringComparer.Ordinal)
    {
        "System.String",
        "string",
        "System.Int32",
        "int",
        "System.Int64",
        "long",
        "System.Int16",
        "short",
        "System.Byte",
        "byte",
        "System.Boolean",
        "bool",
        "System.Double",
        "double",
        "System.Single",
        "float",
        "System.Decimal",
        "decimal",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.TimeSpan",
        "System.Guid",
        "System.Uri"
    };

    /// <summary>
    ///     Collection types that support configuration binding.
    /// </summary>
    public static readonly HashSet<string> SupportedCollectionTypes = new(StringComparer.Ordinal)
    {
        "System.Collections.Generic.List<>",
        "System.Collections.Generic.IList<>",
        "System.Collections.Generic.ICollection<>",
        "System.Collections.Generic.IEnumerable<>",
        "System.Collections.Generic.IReadOnlyList<>",
        "System.Collections.Generic.IReadOnlyCollection<>",
        "System.Collections.Generic.Dictionary<,>",
        "System.Collections.Generic.IDictionary<,>"
    };

    /// <summary>
    ///     Options pattern types from Microsoft.Extensions.Options.
    /// </summary>
    public static readonly HashSet<string> OptionsPatternTypes = new(StringComparer.Ordinal)
    {
        "Microsoft.Extensions.Options.IOptions<>",
        "Microsoft.Extensions.Options.IOptionsSnapshot<>",
        "Microsoft.Extensions.Options.IOptionsMonitor<>"
    };

    /// <summary>
    ///     Checks if a type name is a supported primitive type for configuration binding.
    /// </summary>
    public static bool IsPrimitiveType(string typeName) => SupportedPrimitiveTypes.Contains(typeName);

    /// <summary>
    ///     Checks if a type name is a supported collection type for configuration binding.
    /// </summary>
    public static bool IsCollectionType(string typeName) => SupportedCollectionTypes.Contains(typeName);

    /// <summary>
    ///     Checks if a type name is an Options pattern type.
    /// </summary>
    public static bool IsOptionsPattern(string typeName) => OptionsPatternTypes.Contains(typeName);
}
