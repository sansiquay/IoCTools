namespace IoCTools.Tools.Cli;

using System.Text.Json;

internal static class GraphPrinter
{
    public static void Write(RegistrationSummary summary,
        string format,
        string? typeFilter)
    {
        var records = string.IsNullOrWhiteSpace(typeFilter)
            ? summary.Records
            : summary.Records.Where(r =>
                string.Equals(r.ServiceType, typeFilter, StringComparison.Ordinal) ||
                string.Equals(r.ImplementationType, typeFilter, StringComparison.Ordinal)).ToList();

        format = format.ToLowerInvariant();
        switch (format)
        {
            case "json":
                var payload = records.Select(r => new
                {
                    r.Kind,
                    r.ServiceType,
                    r.ImplementationType,
                    r.Lifetime,
                    r.IsConditional,
                    r.ConditionExpression
                });
                Console.WriteLine(JsonSerializer.Serialize(payload,
                    new JsonSerializerOptions { WriteIndented = true }));
                break;
            case "mermaid":
                Console.WriteLine("graph TD");
                foreach (var r in records)
                {
                    if (r.Kind == RegistrationKind.Configuration) continue;
                    var service = Sanitize(r.ServiceType);
                    var impl = Sanitize(r.ImplementationType ?? r.ServiceType ?? "impl");
                    if (service == impl) continue; // Skip self-edges
                    Console.WriteLine($"  {service} --> {impl}");
                }

                break;
            default:
                // fallback PlantUML-ish
                Console.WriteLine("@startuml");
                foreach (var r in records)
                {
                    if (r.Kind == RegistrationKind.Configuration) continue;
                    var service = Sanitize(r.ServiceType);
                    var impl = Sanitize(r.ImplementationType ?? r.ServiceType ?? "impl");
                    if (service == impl) continue; // Skip self-edges
                    Console.WriteLine($"{service} --> {impl}");
                }

                Console.WriteLine("@enduml");
                break;
        }
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown";
        return value.Replace('.', '_').Replace('<', '_').Replace('>', '_').Replace(':', '_');
    }
}
