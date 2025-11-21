namespace IoCTools.Tools.Cli;

using System.Linq;

internal static class RegistrationPrinter
{
    private const int MaxRows = 50;

    public static void Write(RegistrationSummary summary)
    {
        Console.WriteLine($"Extension Path: {summary.ExtensionPath}");

        var serviceRecords = summary.Records.Where(r => r.Kind == RegistrationKind.Service).ToList();
        var configRecords = summary.Records.Where(r => r.Kind == RegistrationKind.Configuration).ToList();

        Console.WriteLine($"Service Registrations: {serviceRecords.Count}");
        PrintServices(serviceRecords);

        Console.WriteLine();
        Console.WriteLine($"Configuration Bindings: {configRecords.Count}");
        PrintConfigurations(configRecords);
    }

    private static void PrintServices(IReadOnlyList<RegistrationRecord> records)
    {
        if (records.Count == 0)
        {
            Console.WriteLine("  (none)");
            return;
        }

        var take = Math.Min(records.Count, MaxRows);
        for (var i = 0; i < take; i++)
        {
            var record = records[i];
            var service = record.ServiceType ?? record.ImplementationType ?? "(factory)";
            var implementation = record.ImplementationType ?? record.ServiceType ?? "(factory)";
            var lifetime = record.Lifetime ?? record.MethodName;
            var factorySuffix = record.UsesFactory ? " via factory" : string.Empty;
            var conditionalSuffix = record.IsConditional && !string.IsNullOrWhiteSpace(record.ConditionExpression)
                ? $" when {record.ConditionExpression}"
                : record.IsConditional ? " (conditional)" : string.Empty;

            Console.WriteLine($"  - [{lifetime}] {service} => {implementation}{factorySuffix}{conditionalSuffix}");
        }

        if (records.Count > MaxRows)
            Console.WriteLine($"  ... {records.Count - MaxRows} more (use services-path to view full source).");
    }

    private static void PrintConfigurations(IReadOnlyList<RegistrationRecord> records)
    {
        if (records.Count == 0)
        {
            Console.WriteLine("  (none)");
            return;
        }

        foreach (var record in records.Take(MaxRows))
        {
            var configuredType = record.ServiceType ?? "unknown";
            Console.WriteLine($"  - Configure<{configuredType}>()");
        }

        if (records.Count > MaxRows)
            Console.WriteLine($"  ... {records.Count - MaxRows} more (use services-path to view full source).");
    }
}
