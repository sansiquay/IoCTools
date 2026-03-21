namespace IoCTools.Tools.Cli;

using System.Text.Json;

internal sealed record MatchResult(string Kind, string FieldName, string TypeName, string Source, bool IsExternal);

internal static class WhyPrinter
{
    public static void Write(ServiceFieldReport report,
        string dependency,
        OutputContext output)
    {
        var matches = new List<MatchResult>();
        foreach (var d in report.DependencyFields.Where(d => d.TypeName.Contains(dependency, StringComparison.OrdinalIgnoreCase)))
            matches.Add(new MatchResult("Dependency", d.FieldName, d.TypeName, d.Source ?? string.Empty, d.IsExternal));
        foreach (var c in report.ConfigurationFields
            .Where(c => c.TypeName.Contains(dependency, StringComparison.OrdinalIgnoreCase) ||
                        (c.ConfigurationKey?.Contains(dependency, StringComparison.OrdinalIgnoreCase) ?? false)))
            matches.Add(new MatchResult("Configuration", c.FieldName, c.TypeName, c.ConfigurationKey ?? "<section>", false));

        if (output.IsJson)
        {
            var payload = new
            {
                typeName = report.TypeName,
                dependency,
                matches = matches.Select(m => new
                {
                    kind = m.Kind,
                    fieldName = m.FieldName,
                    typeName = m.TypeName,
                    source = m.Source,
                    isExternal = m.IsExternal
                })
            };
            output.WriteJson(payload);
            return;
        }

        output.WriteLine($"Service: {report.TypeName}");

        if (matches.Count == 0)
        {
            output.WriteLine($"No generated dependency matched '{dependency}'.");

            // Suggest close matches using shared utility
            var availableTypes = report.DependencyFields
                .Select(d => d.TypeName)
                .Concat(report.ConfigurationFields.Select(c => c.TypeName))
                .Concat(report.ConfigurationFields
                    .Where(c => c.ConfigurationKey != null)
                    .Select(c => $"Configuration key: {c.ConfigurationKey}"));
            FuzzySuggestionUtility.PrintSuggestions(output, dependency, availableTypes);

            return;
        }

        foreach (var match in matches)
        {
            output.WriteLine($"- Kind: {match.Kind}");
            output.WriteLine($"  Field: {match.FieldName}");
            output.WriteLine($"  Type:  {match.TypeName}");
            output.WriteLine($"  Source: {match.Source}");
            if (match.Kind == "Dependency")
                output.WriteLine($"  External: {match.IsExternal}");
        }
    }
}
