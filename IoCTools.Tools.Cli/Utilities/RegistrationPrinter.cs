namespace IoCTools.Tools.Cli;

using System.Text.Json;

internal static class RegistrationPrinter
{
    private const int MaxRows = 50;

    public static void Write(RegistrationSummary summary, OutputContext output)
    {
        if (output.IsJson)
        {
            var payload = summary.Records.Select(r => new
            {
                kind = r.Kind.ToString(),
                serviceType = r.ServiceType,
                implementationType = r.ImplementationType,
                lifetime = r.Lifetime,
                isConditional = r.IsConditional,
                conditionExpression = r.ConditionExpression,
                usesFactory = r.UsesFactory,
                methodName = r.MethodName
            });
            output.WriteJson(payload);
            return;
        }

        output.WriteLine($"Extension Path: {summary.ExtensionPath}");

        var serviceRecords = summary.Records.Where(r => r.Kind == RegistrationKind.Service).ToList();
        var configRecords = summary.Records.Where(r => r.Kind == RegistrationKind.Configuration).ToList();

        output.WriteLine($"Service Registrations: {serviceRecords.Count}");
        PrintServices(serviceRecords, output);

        output.WriteLine(string.Empty);
        output.WriteLine($"Configuration Bindings: {configRecords.Count}");
        PrintConfigurations(configRecords, output);
    }

    private static void PrintServices(IReadOnlyList<RegistrationRecord> records, OutputContext output)
    {
        if (records.Count == 0)
        {
            output.WriteLine("  (none)");
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
                : record.IsConditional
                    ? " (conditional)"
                    : string.Empty;

            output.WriteLine($"  - [{AnsiColor.Lifetime(lifetime)}] {service} => {implementation}{factorySuffix}{conditionalSuffix}");
        }

        if (records.Count > MaxRows)
            output.WriteLine($"  ... {records.Count - MaxRows} more (use services-path to view full source).");
    }

    private static void PrintConfigurations(IReadOnlyList<RegistrationRecord> records, OutputContext output)
    {
        if (records.Count == 0)
        {
            output.WriteLine("  (none)");
            return;
        }

        foreach (var record in records.Take(MaxRows))
        {
            var configuredType = record.ServiceType ?? "unknown";
            output.WriteLine($"  - Configure<{configuredType}>()");
        }

        if (records.Count > MaxRows)
            output.WriteLine($"  ... {records.Count - MaxRows} more (use services-path to view full source).");
    }
}
