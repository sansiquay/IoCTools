namespace IoCTools.Tools.Cli;

using System.Text.Json;

internal static class ConfigAuditPrinter
{
    public static void Write(IReadOnlyList<ServiceFieldReport> reports,
        string? settingsPath)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var report in reports)
        foreach (var cfg in report.ConfigurationFields)
        {
            var key = string.IsNullOrWhiteSpace(cfg.ConfigurationKey) ? cfg.FieldName : cfg.ConfigurationKey!;
            keys.Add(key);
        }

        if (keys.Count == 0)
        {
            Console.WriteLine("No configuration bindings found in project.");
            return;
        }

        HashSet<string> settingsKeys = new(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(settingsPath) && File.Exists(settingsPath))
            try
            {
                using var stream = File.OpenRead(settingsPath);
                using var doc = JsonDocument.Parse(stream);
                Flatten(doc.RootElement, settingsKeys, string.Empty);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to read settings file '{settingsPath}': {ex.Message}");
            }

        var missing = settingsKeys.Count == 0
            ? keys
            : keys.Where(k => !settingsKeys.Contains(k)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("Configuration audit:");
        Console.WriteLine($"  Required bindings: {keys.Count}");
        if (settingsKeys.Count > 0)
            Console.WriteLine($"  Settings keys discovered: {settingsKeys.Count}");

        if (missing.Count == 0)
        {
            Console.WriteLine("  All keys present in provided settings.");
        }
        else
        {
            Console.WriteLine("  Missing keys:");
            foreach (var key in missing.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine($"    - {key}");
        }
    }

    private static void Flatten(JsonElement element,
        HashSet<string> keys,
        string prefix)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var next = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}:{prop.Name}";
                    Flatten(prop.Value, keys, next);
                }

                break;
            default:
                if (!string.IsNullOrEmpty(prefix)) keys.Add(prefix);
                break;
        }
    }
}
