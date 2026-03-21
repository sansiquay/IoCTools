namespace IoCTools.Tools.Cli;

using System.Text.Json;

internal static class ExplainPrinter
{
    public static void Write(ServiceFieldReport report, OutputContext output)
    {
        if (output.IsJson)
        {
            var payload = new
            {
                typeName = report.TypeName,
                filePath = report.FilePath,
                dependencies = report.DependencyFields.Select(d => new
                {
                    typeName = d.TypeName,
                    fieldName = d.FieldName,
                    source = d.Source,
                    isExternal = d.IsExternal
                }),
                configuration = report.ConfigurationFields.Select(c => new
                {
                    typeName = c.TypeName,
                    fieldName = c.FieldName,
                    configurationKey = c.ConfigurationKey ?? "<inferred>",
                    required = c.Required == true,
                    supportsReloading = c.SupportsReloading == true
                })
            };
            output.WriteJson(payload);
            return;
        }

        output.WriteLine($"Service: {report.TypeName}");
        output.WriteLine($"File: {report.FilePath}");

        if (report.DependencyFields.Count == 0 && report.ConfigurationFields.Count == 0)
        {
            output.WriteLine("No IoCTools-generated dependencies found.");
            return;
        }

        if (report.DependencyFields.Count > 0)
        {
            output.WriteLine("Dependencies:");
            foreach (var dep in report.DependencyFields)
                output.WriteLine($"  - {dep.TypeName} => {dep.FieldName} [{dep.Source}]" +
                                  (dep.IsExternal ? " (external)" : string.Empty));
        }

        if (report.ConfigurationFields.Count > 0)
        {
            output.WriteLine("Configuration:");
            foreach (var cfg in report.ConfigurationFields)
            {
                var key = string.IsNullOrWhiteSpace(cfg.ConfigurationKey) ? "<inferred>" : cfg.ConfigurationKey;
                var required = cfg.Required == true ? "required" : "optional";
                var reload = cfg.SupportsReloading == true ? ", reload" : string.Empty;
                output.WriteLine($"  - {cfg.TypeName} => {cfg.FieldName} (key: {key}, {required}{reload})");
            }
        }
    }
}
