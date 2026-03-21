namespace IoCTools.Tools.Cli;

using System.Text.Json;

internal static class GraphPrinter
{
    public static void Write(RegistrationSummary summary,
        string format,
        string? typeFilter,
        OutputContext output)
    {
        var records = string.IsNullOrWhiteSpace(typeFilter)
            ? summary.Records
            : summary.Records.Where(r =>
                string.Equals(r.ServiceType, typeFilter, StringComparison.Ordinal) ||
                string.Equals(r.ImplementationType, typeFilter, StringComparison.Ordinal)).ToList();

        format = format.ToLowerInvariant();

        // JSON output via --json flag OR --format json
        if (output.IsJson || format == "json")
        {
            var payload = records.Select(r => new
            {
                r.Kind,
                r.ServiceType,
                r.ImplementationType,
                r.Lifetime,
                r.IsConditional,
                r.ConditionExpression
            });
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
            foreach (var r in records)
            {
                if (r.Kind == RegistrationKind.Configuration) continue;
                var service = Sanitize(r.ServiceType);
                var impl = Sanitize(r.ImplementationType ?? r.ServiceType ?? "impl");
                if (service == impl) continue; // Skip self-edges
                output.WriteLine($"  {service} --> {impl}");
            }
        }
        else
        {
            // fallback PlantUML-ish
            output.WriteLine("@startuml");
            foreach (var r in records)
            {
                if (r.Kind == RegistrationKind.Configuration) continue;
                var service = Sanitize(r.ServiceType);
                var impl = Sanitize(r.ImplementationType ?? r.ServiceType ?? "impl");
                if (service == impl) continue; // Skip self-edges
                output.WriteLine($"{service} --> {impl}");
            }
            output.WriteLine("@enduml");
        }
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown";
        return value.Replace('.', '_').Replace('<', '_').Replace('>', '_').Replace(':', '_');
    }
}
