namespace IoCTools.Tools.Cli;

internal static class WhyPrinter
{
    public static void Write(ServiceFieldReport report,
        string dependency)
    {
        Console.WriteLine($"Service: {report.TypeName}");
        var matches = report.DependencyFields.Where(d => d.TypeName.Contains(dependency, StringComparison.Ordinal))
            .Select(d => ("Dependency", d.FieldName, d.TypeName, d.Source, d.IsExternal)).ToList();
        matches.AddRange(report.ConfigurationFields
            .Where(c => c.TypeName.Contains(dependency, StringComparison.Ordinal) ||
                        (c.ConfigurationKey?.Contains(dependency, StringComparison.Ordinal) ?? false))
            .Select(c => ("Configuration", c.FieldName, c.TypeName, c.ConfigurationKey ?? "<section>", false)));

        if (matches.Count == 0)
        {
            Console.WriteLine($"No generated dependency matched '{dependency}'.");
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
}
