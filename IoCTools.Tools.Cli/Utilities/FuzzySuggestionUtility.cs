namespace IoCTools.Tools.Cli;

/// <summary>
/// Shared fuzzy type name suggestion utility (CLI-04).
/// Extracted from WhyPrinter to be usable by all commands.
/// </summary>
internal static class FuzzySuggestionUtility
{
    /// <summary>
    /// Gets type name suggestions for a given query from a collection of available type names.
    /// Uses case-insensitive substring matching.
    /// </summary>
    public static IReadOnlyList<string> GetSuggestions(string query, IEnumerable<string> availableTypes, int maxResults = 5)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<string>();

        var suggestions = availableTypes
            .Where(t => t.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();

        return suggestions;
    }

    /// <summary>
    /// Prints "Did you mean?" suggestions to the output context.
    /// </summary>
    public static void PrintSuggestions(OutputContext output, string query, IEnumerable<string> availableTypes)
    {
        var suggestions = GetSuggestions(query, availableTypes);
        if (suggestions.Count == 0) return;

        output.WriteLine("");
        output.WriteLine("Did you mean:");
        foreach (var suggestion in suggestions)
            output.WriteLine($"  - {suggestion}");
    }
}
