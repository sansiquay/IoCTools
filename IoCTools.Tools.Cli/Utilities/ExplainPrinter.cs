namespace IoCTools.Tools.Cli;

internal static class ExplainPrinter
{
    public static void Write(ServiceFieldReport report)
    {
        Console.WriteLine($"Service: {report.TypeName}");
        Console.WriteLine($"File: {report.FilePath}");

        if (report.DependencyFields.Count == 0 && report.ConfigurationFields.Count == 0)
        {
            Console.WriteLine("No IoCTools-generated dependencies found.");
            return;
        }

        if (report.DependencyFields.Count > 0)
        {
            Console.WriteLine("Dependencies:");
            foreach (var dep in report.DependencyFields)
                Console.WriteLine($"  - {dep.TypeName} => {dep.FieldName} [{dep.Source}]" +
                                  (dep.IsExternal ? " (external)" : string.Empty));
        }

        if (report.ConfigurationFields.Count > 0)
        {
            Console.WriteLine("Configuration:");
            foreach (var cfg in report.ConfigurationFields)
            {
                var key = string.IsNullOrWhiteSpace(cfg.ConfigurationKey) ? "<inferred>" : cfg.ConfigurationKey;
                var required = cfg.Required == true ? "required" : "optional";
                var reload = cfg.SupportsReloading == true ? ", reload" : string.Empty;
                Console.WriteLine($"  - {cfg.TypeName} => {cfg.FieldName} (key: {key}, {required}{reload})");
            }
        }
    }
}
