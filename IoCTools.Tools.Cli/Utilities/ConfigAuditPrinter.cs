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
            var key = string.IsNullOrWhiteSpace(cfg.ConfigurationKey)
                ? InferSectionKeyFromTypeName(cfg.TypeName)
                : cfg.ConfigurationKey!;
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

    /// <summary>
    /// Infers the configuration section key from a type name, matching the logic used
    /// by the generator's ConfigurationInjectionInfo.InferSectionNameFromType().
    /// </summary>
    internal static string InferSectionKeyFromTypeName(string typeName)
    {
        // Handle IOptions<T>, IOptionsSnapshot<T> patterns
        if (typeName.StartsWith("IOptions", StringComparison.Ordinal) ||
            typeName.StartsWith("Microsoft.Extensions.Options.IOptions", StringComparison.Ordinal))
        {
            var start = typeName.IndexOf('<');
            if (start > 0)
            {
                var end = typeName.LastIndexOf('>');
                if (end > start)
                    typeName = typeName.Substring(start + 1, end - start - 1);
            }
        }

        // Handle generic types with backtick notation (e.g., IOptions`1<T>)
        var genericIndex = typeName.IndexOf('`');
        if (genericIndex > 0)
            typeName = typeName.Substring(0, genericIndex);

        // Extract the simple type name (without namespace)
        var lastDot = typeName.LastIndexOf('.');
        if (lastDot >= 0)
            typeName = typeName.Substring(lastDot + 1);

        // Remove common configuration suffixes (matching generator logic)
        if (typeName.EndsWith("Settings"))
            return typeName.Substring(0, typeName.Length - "Settings".Length);
        if (typeName.EndsWith("Configuration"))
            return typeName.Substring(0, typeName.Length - "Configuration".Length);
        if (typeName.EndsWith("Config"))
            return typeName.Substring(0, typeName.Length - "Config".Length);
        if (typeName.EndsWith("Options"))
            return typeName.Substring(0, typeName.Length - "Options".Length);
        if (typeName.EndsWith("Object"))
            return typeName.Substring(0, typeName.Length - "Object".Length);

        return typeName;
    }
}
