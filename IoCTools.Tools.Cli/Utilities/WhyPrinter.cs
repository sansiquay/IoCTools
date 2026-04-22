namespace IoCTools.Tools.Cli;

using System.Text.Json;

using Generator.Shared;

using IoCTools.Tools.Cli.CommandLine;

internal sealed record MatchResult(
    string Kind,
    string FieldName,
    string TypeName,
    string Source,
    bool IsExternal,
    AutoDepAttribution? Attribution);

internal static class WhyPrinter
{
    public static void Write(ServiceFieldReport report,
        string dependency,
        OutputContext output,
        CommonAutoDepsOptions? autoDepsFlags = null)
    {
        autoDepsFlags ??= CommonAutoDepsOptions.Empty;

        var matches = new List<MatchResult>();
        foreach (var d in report.DependencyFields.Where(d => d.TypeName.Contains(dependency, StringComparison.OrdinalIgnoreCase)))
        {
            if (autoDepsFlags.HideAutoDeps && IsAutoDep(d.Attribution)) continue;
            if (autoDepsFlags.OnlyAutoDeps && !IsAutoDep(d.Attribution)) continue;
            matches.Add(new MatchResult("Dependency", d.FieldName, d.TypeName, d.Source ?? string.Empty, d.IsExternal, d.Attribution));
        }

        if (!autoDepsFlags.OnlyAutoDeps)
        {
            foreach (var c in report.ConfigurationFields
                .Where(c => c.TypeName.Contains(dependency, StringComparison.OrdinalIgnoreCase) ||
                            (c.ConfigurationKey?.Contains(dependency, StringComparison.OrdinalIgnoreCase) ?? false)))
                matches.Add(new MatchResult("Configuration", c.FieldName, c.TypeName, c.ConfigurationKey ?? "<section>", false, null));
        }

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
                    sourceTag = m.Attribution?.ToTag() ?? "explicit",
                    isExternal = m.IsExternal,
                    attribution = m.Attribution is { } attr
                        ? new
                        {
                            kind = attr.Kind.ToString(),
                            sourceName = attr.SourceName,
                            assemblyName = attr.AssemblyName
                        }
                        : null
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

            if (match.Attribution is { } attribution && attribution.Kind != AutoDepSourceKind.Explicit)
                WriteAttributionBlock(output, match.TypeName, attribution, report.TypeName);
        }
    }

    private static bool IsAutoDep(AutoDepAttribution? attr) =>
        attr is { } a && a.Kind != AutoDepSourceKind.Explicit;

    private static void WriteAttributionBlock(OutputContext output, string typeName, AutoDepAttribution attribution, string serviceName)
    {
        output.WriteLine($"  source: {attribution.ToTag()}");
        switch (attribution.Kind)
        {
            case AutoDepSourceKind.AutoBuiltinILogger:
                output.WriteLine("  reason: Microsoft.Extensions.Logging.ILogger<T> detected in references");
                output.WriteLine($"  closed to: {typeName} (service's concrete type)");
                output.WriteLine("  disable detection: IoCToolsAutoDetectLogger=false");
                output.WriteLine("  suppress here: [NoAutoDepOpen(typeof(ILogger<>))]");
                break;
            case AutoDepSourceKind.AutoUniversal:
                output.WriteLine($"  reason: [assembly: AutoDep<{typeName}>]");
                output.WriteLine($"  suppress here: [NoAutoDep<{typeName}>]");
                break;
            case AutoDepSourceKind.AutoOpenUniversal:
                output.WriteLine("  reason: [assembly: AutoDepOpen(typeof(...))] matched this service's open shape");
                output.WriteLine("  suppress here: [NoAutoDepOpen(typeof(<OpenShape>))]");
                break;
            case AutoDepSourceKind.AutoProfile:
                output.WriteLine($"  reason: provided by profile '{attribution.SourceName}'");
                output.WriteLine($"  suppress here: [NoAutoDep<{typeName}>] or remove service from profile");
                break;
            case AutoDepSourceKind.AutoTransitive:
                output.WriteLine($"  reason: referenced assembly '{attribution.AssemblyName}' declared [assembly: AutoDep<...>(Scope = Transitive)]");
                output.WriteLine($"  suppress here: [NoAutoDep<{typeName}>]");
                break;
        }
    }
}
