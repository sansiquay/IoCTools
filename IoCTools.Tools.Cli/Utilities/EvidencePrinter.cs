namespace IoCTools.Tools.Cli;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

using CommandLine;

using Generator.Shared;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class EvidencePrinter
{
    public static async Task<EvidenceBundle> BuildAsync(ProjectContext context,
        EvidenceCommandOptions options,
        CancellationToken token)
    {
        var started = Stopwatch.StartNew();
        var inspector = new ServiceFieldInspector(context.Project);
        var reports = await inspector.GetFieldReportsAsync(null, Array.Empty<string>(), token);
        var target = options.TypeName == null
            ? null
            : reports.FirstOrDefault(r => string.Equals(r.TypeName, options.TypeName, StringComparison.Ordinal));

        var artifactWriter = await GeneratorArtifactWriter.CreateAsync(context, options.OutputDirectory, token);
        var registrations = BuildRegistrations(context, artifactWriter);
        var diagnostics = await DiagnosticRunner.RunAsync(context, token);
        var validators = ValidatorInspector.DiscoverValidators(context.Compilation);
        var configuration = BuildConfiguration(reports, options.SettingsPath);
        var migrationHints = BuildMigrationHints(context, options.TypeName, token);
        var generatedArtifacts = BuildGeneratedArtifacts(artifactWriter);
        started.Stop();

        var serviceRegistrations = registrations
            .Where(r => string.Equals(r.kind, nameof(RegistrationKind.Service), StringComparison.Ordinal))
            .ToArray();
        var configurationRegistrations = registrations
            .Where(r => string.Equals(r.kind, nameof(RegistrationKind.Configuration), StringComparison.Ordinal))
            .ToArray();

        var explicitDeps = target == null
            ? Array.Empty<GeneratedFieldInfo>()
            : target.DependencyFields.Where(d => !IsAutoDep(d.Attribution)).ToArray();
        var autoDepFields = target == null
            ? Array.Empty<GeneratedFieldInfo>()
            : target.DependencyFields.Where(d => IsAutoDep(d.Attribution)).ToArray();

        var hideAuto = options.AutoDepsFlags.HideAutoDeps;
        var onlyAuto = options.AutoDepsFlags.OnlyAutoDeps;

        var typeEvidence = target == null
            ? null
            : new EvidenceTypeEvidence(
                target.TypeName,
                target.FilePath,
                (onlyAuto ? Array.Empty<GeneratedFieldInfo>() : explicitDeps)
                    .Select(d => new EvidenceDependency(d.FieldName, d.TypeName, d.Source, d.IsExternal))
                    .ToArray(),
                (onlyAuto ? Array.Empty<GeneratedFieldInfo>() : target.ConfigurationFields.ToArray())
                    .Select(c => new EvidenceConfigurationBinding(
                        c.FieldName,
                        c.TypeName,
                        string.IsNullOrWhiteSpace(c.ConfigurationKey) ? "<inferred>" : c.ConfigurationKey!,
                        c.Required == true,
                        c.SupportsReloading == true))
                    .ToArray(),
                FilterRegistrations(registrations, target.TypeName),
                (hideAuto ? Array.Empty<GeneratedFieldInfo>() : autoDepFields)
                    .Select(d => new EvidenceAutoDep(
                        d.TypeName,
                        d.Attribution!.Value.ToTag(),
                        SuppressHint(d.TypeName, d.Attribution.Value)))
                    .ToArray());

        return new EvidenceBundle(
            new EvidenceProject(
                context.Project.FilePath ?? options.Common.ProjectPath,
                context.Project.Name,
                options.Common.Configuration,
                options.Common.Framework),
            new EvidenceServices(
                serviceRegistrations.Length,
                configurationRegistrations.Length,
                registrations),
            typeEvidence,
            new EvidenceDiagnostics(
                diagnostics.Count,
                diagnostics.Count(d => string.Equals(d.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
                diagnostics.Count(d => string.Equals(d.Severity, "Warning", StringComparison.OrdinalIgnoreCase)),
                diagnostics.Count(d => string.Equals(d.Severity, "Info", StringComparison.OrdinalIgnoreCase)),
                diagnostics.Any(d => string.Equals(d.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
                diagnostics.Select(d => new EvidenceDiagnostic(
                        d.Id,
                        d.Severity,
                        d.Message,
                        $"{d.FilePath}:{d.Line}:{d.Column}"))
                    .ToArray()),
            configuration,
            validators.Count == 0
                ? null
                : new EvidenceValidators(validators.Count, validators.Select(v => v.FullName).ToArray()),
            new EvidenceArtifacts(
                artifactWriter.OutputRoot,
                new EvidenceProfile(
                    started.Elapsed.TotalMilliseconds,
                    serviceRegistrations.Length,
                    configurationRegistrations.Length),
                generatedArtifacts,
                options.BaselineDirectory != null
                    ? BuildCompare(options.BaselineDirectory, artifactWriter.OutputRoot, generatedArtifacts)
                    : null),
            migrationHints);
    }

    public static void Write(EvidenceBundle bundle,
        OutputContext output)
    {
        if (output.IsJson)
        {
            output.WriteJson(bundle);
            return;
        }

        output.WriteLine("Project");
        output.WriteLine($"  Path: {bundle.project.path}");
        output.WriteLine($"  Name: {bundle.project.name}");
        output.WriteLine($"  Configuration: {bundle.project.configuration}");
        if (!string.IsNullOrWhiteSpace(bundle.project.framework))
            output.WriteLine($"  Framework: {bundle.project.framework}");

        output.WriteLine(string.Empty);
        output.WriteLine("Services");
        output.WriteLine($"  Service registrations: {bundle.services.serviceCount}");
        output.WriteLine($"  Configuration bindings: {bundle.services.configurationCount}");
        foreach (var registration in bundle.services.registrations.Take(5))
        {
            var service = registration.serviceType ?? registration.implementationType ?? "(unknown)";
            var implementation = registration.implementationType ?? registration.serviceType ?? "(unknown)";
            var lifetime = registration.lifetime ?? registration.kind;
            output.WriteLine($"  - [{lifetime}] {service} => {implementation}");
        }

        if (bundle.typeEvidence != null)
        {
            output.WriteLine(string.Empty);
            output.WriteLine("Type Evidence");
            output.WriteLine($"  Service: {bundle.typeEvidence.typeName}");
            output.WriteLine($"  File: {bundle.typeEvidence.filePath}");
            output.WriteLine("  Dependencies:");
            if (bundle.typeEvidence.dependencies.Count == 0)
                output.WriteLine("    (none)");
            else
                foreach (var dependency in bundle.typeEvidence.dependencies)
                    output.WriteLine($"    - {dependency.typeName} => {dependency.fieldName} [{dependency.source}]");

            output.WriteLine("  Configuration:");
            if (bundle.typeEvidence.configuration.Count == 0)
                output.WriteLine("    (none)");
            else
                foreach (var config in bundle.typeEvidence.configuration)
                    output.WriteLine(
                        $"    - {config.typeName} => {config.fieldName} (key: {config.configurationKey}, {(config.required ? "required" : "optional")}{(config.supportsReloading ? ", reload" : string.Empty)})");

            if (bundle.typeEvidence.autoDeps.Count > 0)
            {
                output.WriteLine("  Auto-dependencies:");
                output.WriteLine("    Type | Source Tag | Suppress With");
                foreach (var autoDep in bundle.typeEvidence.autoDeps)
                    output.WriteLine($"    {autoDep.typeName} | {autoDep.source} | {autoDep.suppress}");
            }
        }

        output.WriteLine(string.Empty);
        output.WriteLine("Diagnostics");
        output.WriteLine(
            $"  Total: {bundle.diagnostics.total} (Errors: {bundle.diagnostics.errorCount}, Warnings: {bundle.diagnostics.warningCount}, Info: {bundle.diagnostics.infoCount})");

        output.WriteLine(string.Empty);
        output.WriteLine("Configuration");
        output.WriteLine($"  Required bindings: {bundle.configuration.requiredBindings}");
        output.WriteLine($"  Settings keys discovered: {bundle.configuration.settingsKeysDiscovered}");
        if (bundle.configuration.missingKeys.Count == 0)
        {
            output.WriteLine("  Missing keys: none");
        }
        else
        {
            output.WriteLine("  Missing keys:");
            foreach (var key in bundle.configuration.missingKeys)
                output.WriteLine($"    - {key}");
        }

        if (bundle.validators != null)
        {
            output.WriteLine(string.Empty);
            output.WriteLine("Validators");
            output.WriteLine($"  Count: {bundle.validators.count}");
        }

        output.WriteLine(string.Empty);
        output.WriteLine("Artifacts");
        output.WriteLine($"  Output directory: {bundle.artifacts.outputDirectory}");
        output.WriteLine($"  Generated artifacts: {bundle.artifacts.generatedArtifacts.Count}");
        foreach (var artifact in bundle.artifacts.generatedArtifacts.Take(5))
        {
            var fingerprintPrefix = artifact.fingerprint[..Math.Min(12, artifact.fingerprint.Length)];
            output.WriteLine($"  - {artifact.fileName} [{fingerprintPrefix}]");
        }

        if (bundle.artifacts.compare != null)
        {
            var changed = bundle.artifacts.compare.deltas.Count(delta =>
                !string.Equals(delta.status, "unchanged", StringComparison.OrdinalIgnoreCase));
            output.WriteLine($"  Compare baseline: {bundle.artifacts.compare.baselineDirectory}");
            output.WriteLine($"  Compare deltas: {changed} changed, added, or removed artifacts");
        }

        if (bundle.migrationHints.Count > 0)
        {
            output.WriteLine(string.Empty);
            output.WriteLine("Migration Hints");
            foreach (var hint in bundle.migrationHints)
                output.WriteLine($"  - {hint.message}");
        }
    }

    private static bool IsAutoDep(AutoDepAttribution? attr) =>
        attr is { } a && a.Kind != AutoDepSourceKind.Explicit;

    private static string SuppressHint(string depTypeName, AutoDepAttribution attribution) => attribution.Kind switch
    {
        AutoDepSourceKind.AutoBuiltinILogger => "[NoAutoDepOpen(typeof(ILogger<>))]",
        AutoDepSourceKind.AutoOpenUniversal => "[NoAutoDepOpen(typeof(<OpenShape>))]",
        AutoDepSourceKind.AutoUniversal => $"[NoAutoDep<{depTypeName}>]",
        AutoDepSourceKind.AutoTransitive => $"[NoAutoDep<{depTypeName}>]",
        AutoDepSourceKind.AutoProfile => $"[NoAutoDep<{depTypeName}>] or remove from profile",
        _ => string.Empty
    };

    private static IReadOnlyList<EvidenceRegistration> BuildRegistrations(ProjectContext context,
        GeneratorArtifactWriter artifacts)
    {
        var hintName = HintNameBuilder.GetExtensionHint(context.Project);
        if (!artifacts.TryGetFile(hintName, out var path))
            return Array.Empty<EvidenceRegistration>();

        var summary = RegistrationSummaryBuilder.Build(path!);
        return summary.Records.Select(record => new EvidenceRegistration(
                record.Kind.ToString(),
                record.ServiceType,
                record.ImplementationType,
                record.Lifetime,
                record.IsConditional,
                record.UsesFactory))
            .ToArray();
    }

    private static IReadOnlyList<EvidenceGeneratedArtifact> BuildGeneratedArtifacts(GeneratorArtifactWriter artifacts)
    {
        return artifacts.GetArtifacts()
            .Select(artifact => new EvidenceGeneratedArtifact(
                artifact.ArtifactId,
                Path.GetFileName(artifact.Path),
                artifact.Path,
                ComputeFingerprint(artifact.Path),
                new FileInfo(artifact.Path).Length))
            .ToArray();
    }

    private static IReadOnlyList<EvidenceRegistration> FilterRegistrations(IReadOnlyList<EvidenceRegistration> registrations,
        string typeName)
    {
        return registrations.Where(r =>
                TypeFilterUtility.Matches(r.serviceType, typeName) || TypeFilterUtility.Matches(r.implementationType, typeName))
            .ToArray();
    }

    private static EvidenceConfiguration BuildConfiguration(IReadOnlyList<ServiceFieldReport> reports,
        string? settingsPath)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var report in reports)
        foreach (var cfg in report.ConfigurationFields)
        {
            var key = string.IsNullOrWhiteSpace(cfg.ConfigurationKey)
                ? ConfigAuditPrinter.InferSectionKeyFromTypeName(cfg.TypeName)
                : cfg.ConfigurationKey!;
            keys.Add(key);
        }

        var settingsKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(settingsPath) && File.Exists(settingsPath))
            try
            {
                using var stream = File.OpenRead(settingsPath);
                using var doc = JsonDocument.Parse(stream);
                Flatten(doc.RootElement, settingsKeys, string.Empty);
            }
            catch
            {
                // Evidence output preserves discovered configuration truth rather than surfacing file read failures.
            }

        var missing = settingsKeys.Count == 0
            ? keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray()
            : keys.Where(k => !settingsKeys.Contains(k))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return new EvidenceConfiguration(
            keys.Count,
            settingsKeys.Count,
            missing,
            keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static void Flatten(JsonElement element,
        HashSet<string> keys,
        string prefix)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var next = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}:{property.Name}";
                    Flatten(property.Value, keys, next);
                }

                break;
            default:
                if (!string.IsNullOrEmpty(prefix))
                    keys.Add(prefix);
                break;
        }
    }

    private static IReadOnlyList<EvidenceMigrationHint> BuildMigrationHints(ProjectContext context,
        string? typeFilter,
        CancellationToken token)
    {
        var hints = new List<EvidenceMigrationHint>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var semanticModel = context.Compilation.GetSemanticModel(tree);
            var root = tree.GetRoot(token);
            foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    if (semanticModel.GetDeclaredSymbol(variable, token) is not IFieldSymbol symbol)
                        continue;

                    var service = symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        .Replace("global::", string.Empty, StringComparison.Ordinal);
                    if (!string.IsNullOrWhiteSpace(typeFilter) && !TypeFilterUtility.Matches(service, typeFilter!))
                        continue;

                    foreach (var attribute in symbol.GetAttributes())
                    {
                        var attributeName = attribute.AttributeClass?.Name;
                        if (attributeName is "InjectAttribute" or "Inject")
                        {
                            var key = $"{service}|Inject|{symbol.Name}";
                            if (seen.Add(key))
                            {
                                hints.Add(new EvidenceMigrationHint(
                                    service,
                                    "Inject",
                                    symbol.Name,
                                    $"{service} uses [Inject] field '{symbol.Name}'; never use [Inject] in new code. Prefer [DependsOn] for {symbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}."));
                            }
                        }
                        else if (attributeName is "InjectConfigurationAttribute" or "InjectConfiguration")
                        {
                            var configurationKey = attribute.ConstructorArguments.Length > 0 &&
                                                   attribute.ConstructorArguments[0].Value is string configuredKey
                                ? configuredKey
                                : null;
                            var key = $"{service}|InjectConfiguration|{symbol.Name}";
                            if (seen.Add(key))
                            {
                                var details = configurationKey == null
                                    ? string.Empty
                                    : $" for '{configurationKey}'";
                                hints.Add(new EvidenceMigrationHint(
                                    service,
                                    "InjectConfiguration",
                                    symbol.Name,
                                    $"{service} uses InjectConfiguration on '{symbol.Name}'{details}; never use InjectConfiguration in new code. Prefer [DependsOnConfiguration] or [DependsOnOptions]."));
                            }
                        }
                    }
                }
            }
        }

        return hints;
    }

    private static EvidenceCompare BuildCompare(string baselineDirectory,
        string outputDirectory,
        IReadOnlyList<EvidenceGeneratedArtifact> generatedArtifacts)
    {
        var changedArtifacts = new List<string>();
        var deltas = new List<EvidenceArtifactDelta>();
        var baselineArtifacts = new Dictionary<string, EvidenceGeneratedArtifact>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(baselineDirectory))
        {
            foreach (var path in Directory.GetFiles(baselineDirectory, "*.g.cs", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                baselineArtifacts[fileName] = new EvidenceGeneratedArtifact(
                    fileName,
                    fileName,
                    path,
                    ComputeFingerprint(path),
                    new FileInfo(path).Length);
            }
        }

        var currentArtifacts = generatedArtifacts.ToDictionary(artifact => artifact.artifactId, StringComparer.OrdinalIgnoreCase);
        var allKeys = new HashSet<string>(baselineArtifacts.Keys, StringComparer.OrdinalIgnoreCase);
        allKeys.UnionWith(currentArtifacts.Keys);

        foreach (var key in allKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            baselineArtifacts.TryGetValue(key, out var baselineArtifact);
            currentArtifacts.TryGetValue(key, out var currentArtifact);

            var status = baselineArtifact == null
                ? "added"
                : currentArtifact == null
                    ? "removed"
                    : string.Equals(baselineArtifact.fingerprint, currentArtifact.fingerprint, StringComparison.Ordinal)
                        ? "unchanged"
                        : "changed";

            if (!string.Equals(status, "unchanged", StringComparison.Ordinal))
                changedArtifacts.Add(key);

            deltas.Add(new EvidenceArtifactDelta(
                key,
                currentArtifact?.fileName ?? baselineArtifact?.fileName ?? key,
                status,
                baselineArtifact?.path,
                currentArtifact?.path,
                baselineArtifact?.fingerprint,
                currentArtifact?.fingerprint));
        }

        return new EvidenceCompare(outputDirectory, baselineDirectory, changedArtifacts, deltas);
    }

    private static string ComputeFingerprint(string path)
    {
        var hash = SHA256.HashData(File.ReadAllBytes(path));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
