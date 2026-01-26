namespace IoCTools.Generator.Utilities;

/// <summary>
///     Centralized utilities for configuration-related naming operations.
///     Consolidates duplicated suffix stripping logic from AttributeParser,
///     ConfigurationOptionsScanner, and ConfigurationInjectionInfo.
/// </summary>
internal static class ConfigurationNamingUtilities
{
    /// <summary>
    ///     Default suffixes that are commonly stripped from configuration type names.
    /// </summary>
    private static readonly string[] DefaultConfigurationSuffixes = { "Settings", "Configuration", "Options" };

    /// <summary>
    ///     Default suffixes for section name inference (includes Config, Object).
    /// </summary>
    private static readonly string[] DefaultSectionNameSuffixes = { "Settings", "Configuration", "Config", "Options", "Object" };

    /// <summary>
    ///     Gets the default configuration suffixes.
    /// </summary>
    public static string[] GetDefaultConfigurationSuffixes() => DefaultConfigurationSuffixes;

    /// <summary>
    ///     Gets the default section name suffixes.
    /// </summary>
    public static string[] GetDefaultSectionNameSuffixes() => DefaultSectionNameSuffixes;

    /// <summary>
    ///     Removes common configuration suffixes from a type name.
    ///     Removes duplicated suffixes greedily, but leaves one trailing suffix if present.
    ///     Example: "JitterConfigurationOptions" -> "JitterConfiguration"
    ///              "OptionsOptions" -> "Options"
    /// </summary>
    /// <param name="typeName">The type name to strip suffixes from.</param>
    /// <returns>The type name with configuration suffixes removed.</returns>
    public static string StripConfigurationSuffixes(string typeName)
    {
        return StripConfigurationSuffixes(typeName, DefaultConfigurationSuffixes);
    }

    /// <summary>
    ///     Removes common configuration suffixes from a type name using custom suffixes.
    ///     Removes duplicated suffixes greedily, but leaves one trailing suffix if present.
    /// </summary>
    /// <param name="typeName">The type name to strip suffixes from.</param>
    /// <param name="suffixes">The suffixes to strip.</param>
    /// <returns>The type name with configuration suffixes removed.</returns>
    public static string StripConfigurationSuffixes(string typeName, IEnumerable<string> suffixes)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return typeName;

        var trimmed = typeName;
        var strippedCount = 0;

        while (true)
        {
            var matched = suffixes.FirstOrDefault(s => trimmed.EndsWith(s, StringComparison.Ordinal));
            if (matched == null) break;

            // Stop after removing one suffix; collapse duplicates but keep a single suffix if present.
            if (strippedCount > 0) break;

            // If removing the suffix would drop everything, bail out
            if (trimmed.Length == matched.Length) break;

            trimmed = trimmed.Substring(0, trimmed.Length - matched.Length);
            strippedCount++;
        }

        // If we stripped once, leave the remaining name without reapplying the suffix (collapsing duplicates)
        return string.IsNullOrWhiteSpace(trimmed) ? typeName : trimmed;
    }

    /// <summary>
    ///     Infers a section name from a type name by stripping common configuration suffixes.
    ///     This is used for configuration section name inference and strips all matching suffixes.
    /// </summary>
    /// <param name="typeName">The type name to strip suffixes from.</param>
    /// <returns>The type name with configuration suffixes removed.</returns>
    public static string InferSectionNameFromType(string typeName)
    {
        return InferSectionNameFromType(typeName, DefaultSectionNameSuffixes);
    }

    /// <summary>
    ///     Infers a section name from a type name by stripping custom configuration suffixes.
    ///     This is used for configuration section name inference and strips all matching suffixes.
    /// </summary>
    /// <param name="typeName">The type name to strip suffixes from.</param>
    /// <param name="suffixes">The suffixes to strip.</param>
    /// <returns>The type name with configuration suffixes removed.</returns>
    public static string InferSectionNameFromType(string typeName, IEnumerable<string> suffixes)
    {
        foreach (var suffix in suffixes)
            if (typeName.EndsWith(suffix, StringComparison.Ordinal))
                return typeName.Substring(0, typeName.Length - suffix.Length);

        return typeName;
    }
}
