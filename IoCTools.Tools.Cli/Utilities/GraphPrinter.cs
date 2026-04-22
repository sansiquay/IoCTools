namespace IoCTools.Tools.Cli;

using System.Text.Json;

using Generator.Shared;

using IoCTools.Tools.Cli.CommandLine;

internal static class GraphPrinter
{
    /// <summary>
    /// Legacy entry point -- kept so existing runner wiring and tests compile. Produces the
    /// same output as before when <paramref name="autoDepRows"/> is null / empty and
    /// <paramref name="autoDepsFlags"/> is the empty sentinel.
    /// </summary>
    public static void Write(RegistrationSummary summary,
        string format,
        string? typeFilter,
        OutputContext output) =>
        Write(summary, format, typeFilter, output, Array.Empty<AutoDepGraphRow>(), CommonAutoDepsOptions.Empty);

    public static void Write(RegistrationSummary summary,
        string format,
        string? typeFilter,
        OutputContext output,
        IReadOnlyList<AutoDepGraphRow> autoDepRows,
        CommonAutoDepsOptions autoDepsFlags)
    {
        var records = string.IsNullOrWhiteSpace(typeFilter)
            ? summary.Records
            : summary.Records.Where(r =>
                string.Equals(r.ServiceType, typeFilter, StringComparison.Ordinal) ||
                string.Equals(r.ImplementationType, typeFilter, StringComparison.Ordinal)).ToList();

        format = format.ToLowerInvariant();

        // --hide-auto-deps drops all auto-* rows; --only-auto-deps keeps them only.
        var filteredAutoDepRows = autoDepsFlags switch
        {
            { HideAutoDeps: true } => Array.Empty<AutoDepGraphRow>(),
            { OnlyAutoDeps: true } => autoDepRows.Where(r => r.Attribution.Kind != AutoDepSourceKind.Explicit).ToArray(),
            _ => autoDepRows.ToArray()
        };

        var filteredRecords = autoDepsFlags switch
        {
            { OnlyAutoDeps: true } => new List<RegistrationRecord>(),
            _ => records.ToList()
        };

        // JSON output via --json flag OR --format json
        if (output.IsJson || format == "json")
        {
            var payload = new
            {
                registrations = filteredRecords.Select(r => new
                {
                    r.Kind,
                    r.ServiceType,
                    r.ImplementationType,
                    r.Lifetime,
                    r.IsConditional,
                    r.ConditionExpression,
                    source = "explicit"
                }),
                autoDeps = filteredAutoDepRows.Select(row => new
                {
                    service = row.ServiceTypeName,
                    dependency = row.DependencyTypeName,
                    source = row.Attribution.ToTag(),
                    profile = row.Attribution.Kind == AutoDepSourceKind.AutoProfile ? row.Attribution.SourceName : null,
                    assembly = row.Attribution.Kind == AutoDepSourceKind.AutoTransitive ? row.Attribution.AssemblyName : null
                })
            };
            // If --json flag, use WriteJson; otherwise direct output for backwards compat with --format json
            if (output.IsJson)
                output.WriteJson(payload);
            else
                Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        if (format == "mermaid")
        {
            output.WriteLine("graph TD");
            foreach (var r in filteredRecords)
            {
                if (r.Kind == RegistrationKind.Configuration) continue;
                var service = Sanitize(r.ServiceType);
                var impl = Sanitize(r.ImplementationType ?? r.ServiceType ?? "impl");
                if (service == impl) continue; // Skip self-edges
                output.WriteLine($"  {service} --> {impl}");
            }

            foreach (var row in filteredAutoDepRows)
            {
                var service = Sanitize(row.ServiceTypeName);
                var dep = Sanitize(row.DependencyTypeName);
                output.WriteLine($"  {service} -.->|{row.Attribution.ToTag()}| {dep}");
            }
        }
        else
        {
            // fallback PlantUML-ish
            output.WriteLine("@startuml");
            foreach (var r in filteredRecords)
            {
                if (r.Kind == RegistrationKind.Configuration) continue;
                var service = Sanitize(r.ServiceType);
                var impl = Sanitize(r.ImplementationType ?? r.ServiceType ?? "impl");
                if (service == impl) continue; // Skip self-edges
                output.WriteLine($"{service} --> {impl}");
            }

            foreach (var row in filteredAutoDepRows)
            {
                var marker = MarkerFor(row.Attribution);
                var service = Sanitize(row.ServiceTypeName);
                var dep = Sanitize(row.DependencyTypeName);
                output.WriteLine($"{service} ..> {dep} : {marker} {row.Attribution.ToTag()}");
            }
            output.WriteLine("@enduml");
        }

        // Terminal legend so users can decode the markers even in plain-text mode.
        if (filteredAutoDepRows.Length > 0 && format != "json")
        {
            output.WriteLine(string.Empty);
            output.WriteLine("Legend: i = auto-universal/auto-builtin, P = auto-profile, T = auto-transitive");
        }
    }

    private static string MarkerFor(AutoDepAttribution attribution) => attribution.Kind switch
    {
        AutoDepSourceKind.AutoProfile => "P",
        AutoDepSourceKind.AutoTransitive => "T",
        AutoDepSourceKind.AutoUniversal => "i",
        AutoDepSourceKind.AutoOpenUniversal => "i",
        AutoDepSourceKind.AutoBuiltinILogger => "i",
        _ => string.Empty
    };

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown";
        return value.Replace('.', '_').Replace('<', '_').Replace('>', '_').Replace(':', '_');
    }
}

/// <summary>
/// Carries one auto-dep contribution discovered by the resolver. The graph printer
/// surfaces these as secondary "..&gt;" edges (plantuml) or dotted edges (mermaid) so
/// the core service→impl topology stays unchanged but the auto-dep provenance is visible.
/// </summary>
internal sealed record AutoDepGraphRow(
    string ServiceTypeName,
    string DependencyTypeName,
    AutoDepAttribution Attribution);
