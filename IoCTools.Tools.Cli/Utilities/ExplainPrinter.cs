namespace IoCTools.Tools.Cli;

using System.Text.Json;

using Generator.Shared;

using IoCTools.Tools.Cli.CommandLine;

internal static class ExplainPrinter
{
    public static void Write(ServiceFieldReport report,
        OutputContext output,
        CommonAutoDepsOptions? autoDepsFlags = null)
    {
        autoDepsFlags ??= CommonAutoDepsOptions.Empty;

        var explicitDeps = report.DependencyFields.Where(d => !IsAutoDep(d.Attribution)).ToArray();
        var autoDeps = report.DependencyFields.Where(d => IsAutoDep(d.Attribution)).ToArray();

        if (output.IsJson)
        {
            var payload = new
            {
                typeName = report.TypeName,
                filePath = report.FilePath,
                dependencies = (autoDepsFlags.OnlyAutoDeps ? Array.Empty<GeneratedFieldInfo>() : explicitDeps).Select(d => new
                {
                    typeName = d.TypeName,
                    fieldName = d.FieldName,
                    source = d.Source,
                    sourceTag = d.Attribution?.ToTag() ?? "explicit",
                    isExternal = d.IsExternal
                }),
                autoDependencies = (autoDepsFlags.HideAutoDeps ? Array.Empty<GeneratedFieldInfo>() : autoDeps).Select(d => new
                {
                    typeName = d.TypeName,
                    fieldName = d.FieldName,
                    sourceTag = d.Attribution?.ToTag(),
                    narrative = NarrativeFor(d.TypeName, d.Attribution!.Value)
                }),
                configuration = (autoDepsFlags.OnlyAutoDeps ? Array.Empty<GeneratedFieldInfo>() : report.ConfigurationFields.ToArray()).Select(c => new
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

        if (!autoDepsFlags.OnlyAutoDeps && explicitDeps.Length > 0)
        {
            output.WriteLine("Dependencies:");
            foreach (var dep in explicitDeps)
                output.WriteLine($"  - {dep.TypeName} => {dep.FieldName} [{dep.Source}]" +
                                  (dep.IsExternal ? " (external)" : string.Empty));
        }

        if (!autoDepsFlags.HideAutoDeps && autoDeps.Length > 0)
        {
            output.WriteLine("Auto-dependencies:");
            foreach (var dep in autoDeps)
                output.WriteLine($"  - {NarrativeFor(dep.TypeName, dep.Attribution!.Value)}");
        }

        if (!autoDepsFlags.OnlyAutoDeps && report.ConfigurationFields.Count > 0)
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

    private static bool IsAutoDep(AutoDepAttribution? attr) =>
        attr is { } a && a.Kind != AutoDepSourceKind.Explicit;

    private static string NarrativeFor(string depType, AutoDepAttribution attribution) => attribution.Kind switch
    {
        AutoDepSourceKind.AutoBuiltinILogger =>
            $"{depType} is provided by built-in ILogger detection.",
        AutoDepSourceKind.AutoUniversal =>
            $"{depType} is provided by [assembly: AutoDep<{depType}>].",
        AutoDepSourceKind.AutoOpenUniversal =>
            $"{depType} is provided by [assembly: AutoDepOpen(typeof(...))] matched against this service's open shape.",
        AutoDepSourceKind.AutoTransitive =>
            $"{depType} is provided by the referenced assembly '{attribution.AssemblyName}' via a Scope.Transitive declaration.",
        AutoDepSourceKind.AutoProfile =>
            $"{depType} is provided by the '{attribution.SourceName}' profile, attached via [AutoDepsApply] / [AutoDepsApplyGlob].",
        _ => $"{depType} (explicit)"
    };
}
