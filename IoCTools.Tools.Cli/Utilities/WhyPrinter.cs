namespace IoCTools.Tools.Cli;

internal static class WhyPrinter
{
    public static void Write(ServiceFieldReport report,
        string dependency)
    {
        Console.WriteLine($"Service: {report.TypeName}");
        var matches = report.DependencyFields.Where(d => d.TypeName.Contains(dependency, StringComparison.OrdinalIgnoreCase))
            .Select(d => ("Dependency", d.FieldName, d.TypeName, d.Source, d.IsExternal)).ToList();
        matches.AddRange(report.ConfigurationFields
            .Where(c => c.TypeName.Contains(dependency, StringComparison.OrdinalIgnoreCase) ||
                        (c.ConfigurationKey?.Contains(dependency, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(c => ("Configuration", c.FieldName, c.TypeName, c.ConfigurationKey ?? "<section>", false)));

        if (matches.Count == 0)
        {
            Console.WriteLine($"No generated dependency matched '{dependency}'.");

            // Suggest close matches with correct casing
            var suggestions = GetSuggestions(dependency, report);
            if (suggestions.Count > 0)
            {
                Console.WriteLine("Did you mean:");
                foreach (var suggestion in suggestions.Take(3))
                    Console.WriteLine($"  - {suggestion}");
            }

            return;
        }

        foreach (var match in matches)
        {
            Console.WriteLine($"- Kind: {match.Item1}");
            Console.WriteLine($"  Field: {match.Item2}");
            Console.WriteLine($"  Type:  {match.Item3}");
            Console.WriteLine($"  Source: {match.Item4}");
            if (match.Item1 == "Dependency")
                Console.WriteLine($"  External: {match.Item5}");
        }
    }

    private static List<string> GetSuggestions(string dependency, ServiceFieldReport report)
    {
        var suggestions = new List<string>();

        // Suggest from dependency types
        foreach (var field in report.DependencyFields)
        {
            if (field.TypeName.Equals(dependency, StringComparison.OrdinalIgnoreCase))
                suggestions.Add(field.TypeName);
            else if (field.TypeName.IndexOf(dependency, StringComparison.OrdinalIgnoreCase) >= 0)
                suggestions.Add(field.TypeName);
        }

        // Suggest from configuration types
        foreach (var field in report.ConfigurationFields)
        {
            if (field.TypeName.Equals(dependency, StringComparison.OrdinalIgnoreCase))
                suggestions.Add(field.TypeName);
            else if (field.TypeName.IndexOf(dependency, StringComparison.OrdinalIgnoreCase) >= 0)
                suggestions.Add(field.TypeName);

            if (field.ConfigurationKey != null &&
                field.ConfigurationKey.IndexOf(dependency, StringComparison.OrdinalIgnoreCase) >= 0)
                suggestions.Add($"Configuration key: {field.ConfigurationKey}");
        }

        return suggestions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
