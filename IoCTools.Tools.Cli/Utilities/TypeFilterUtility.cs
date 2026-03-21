namespace IoCTools.Tools.Cli;

using System.Text.RegularExpressions;

/// <summary>
/// Unified type name matching with wildcard support (CLI-05).
/// Replaces divergent MatchesTypeName and TypeMatchesFilter implementations.
/// </summary>
internal static class TypeFilterUtility
{
    /// <summary>
    /// Matches a type name against a filter pattern.
    /// Supports * (any chars) and ? (single char) wildcards.
    /// Without wildcards, uses legacy exact + suffix match behavior for backward compatibility.
    /// Matches against fully-qualified name without global:: prefix.
    /// </summary>
    public static bool Matches(string? typeName, string pattern)
    {
        if (typeName == null || string.IsNullOrWhiteSpace(pattern)) return false;

        // Strip global:: prefix
        if (typeName.StartsWith("global::", StringComparison.Ordinal))
            typeName = typeName.Substring("global::".Length, typeName.Length - "global::".Length);

        // If no wildcards, use legacy behavior for backward compatibility
        if (pattern.IndexOf('*') < 0 && pattern.IndexOf('?') < 0)
            return ExactOrSuffixMatch(typeName, pattern);

        // Wildcard-to-regex, mirrors DiagnosticUtilities.CompileIgnoredTypePatterns algorithm
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return Regex.IsMatch(typeName, regexPattern, RegexOptions.IgnoreCase);
    }

    private static bool ExactOrSuffixMatch(string typeName, string filter)
    {
        // Exact match
        if (string.Equals(typeName, filter, StringComparison.OrdinalIgnoreCase)) return true;

        // Suffix match: "MyService" matches "Namespace.MyService"
        if (typeName.EndsWith('.' + filter, StringComparison.OrdinalIgnoreCase)) return true;

        // Name-only match (no namespace in filter)
        var lastDot = typeName.LastIndexOf('.');
        if (lastDot >= 0 && string.Equals(typeName.Substring(lastDot + 1, typeName.Length - lastDot - 1), filter, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
