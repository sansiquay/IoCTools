namespace IoCTools.Generator.Diagnostics;

using Helpers;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis.Diagnostics;

internal static class DiagnosticUtilities
{
    private delegate bool TryGetValueDelegate(string key, out string value);

    public static DiagnosticConfiguration GetDiagnosticConfiguration(GeneratorExecutionContext context)
        => ParseConfiguration(context.AnalyzerConfigOptions.GlobalOptions.TryGetValue);

    public static DiagnosticConfiguration GetDiagnosticConfiguration(Compilation _)
        => new()
        {
            DiagnosticsEnabled = true,
            NoImplementationSeverity = DiagnosticSeverity.Error,
            ManualImplementationSeverity = DiagnosticSeverity.Error,
            LifetimeValidationEnabled = true,
            LifetimeValidationSeverity = DiagnosticSeverity.Error
        };

    public static DiagnosticConfiguration GetDiagnosticConfiguration(AnalyzerConfigOptionsProvider options)
        => ParseConfiguration(options.GlobalOptions.TryGetValue);

    private static DiagnosticConfiguration ParseConfiguration(TryGetValueDelegate tryGetValue)
    {
        var config = new DiagnosticConfiguration();

        if (tryGetValue("build_property.IoCToolsNoImplementationSeverity", out var noImplSeverity))
            config.NoImplementationSeverity = ParseDiagnosticSeverity(noImplSeverity);
        if (tryGetValue("build_property.IoCToolsManualSeverity", out var manualSeverity))
            config.ManualImplementationSeverity = ParseDiagnosticSeverity(manualSeverity);
        if (tryGetValue("build_property.IoCToolsDisableDiagnostics", out var disableStr) &&
            bool.TryParse(disableStr, out var disable))
            config.DiagnosticsEnabled = !disable;
        if (tryGetValue("build_property.IoCToolsLifetimeValidationSeverity", out var lifetimeSeverity) &&
            !string.IsNullOrWhiteSpace(lifetimeSeverity))
            config.LifetimeValidationSeverity = ParseDiagnosticSeverity(lifetimeSeverity);
        if (tryGetValue("build_property.IoCToolsDisableLifetimeValidation", out var disableLifetimeStr) &&
            bool.TryParse(disableLifetimeStr, out var disableLifetime))
            config.LifetimeValidationEnabled = !disableLifetime;

        // Parse IoCToolsIgnoredTypePatterns for cross-assembly interface matching
        if (tryGetValue("build_property.IoCToolsIgnoredTypePatterns", out var ignoredPatterns))
            config.CompiledIgnoredPatterns = CompileIgnoredTypePatterns(ignoredPatterns);

        return config;
    }

    internal static Regex[] CompileIgnoredTypePatterns(string patternsString)
    {
        if (string.IsNullOrWhiteSpace(patternsString))
            return GetDefaultIgnoredPatterns();

        var patterns = patternsString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (patterns.Length == 0)
            return GetDefaultIgnoredPatterns();

        var compiled = new List<Regex>();
        foreach (var pattern in patterns)
        {
            var trimmedPattern = pattern.Trim();
            if (string.IsNullOrWhiteSpace(trimmedPattern))
                continue;

            // Convert glob-style wildcards to regex
            // *.Abstractions.* -> ^.*\.Abstractions\..*$
            // *.ILoggerService<*> -> ^.*\.ILoggerService<.*>$
            var regexPattern = "^" + Regex.Escape(trimmedPattern)
                .Replace("\\*", ".*")
                .Replace("<", "\\<")
                .Replace(">", "\\>") + "$";

            compiled.Add(new Regex(regexPattern, RegexOptions.Compiled));
        }

        return compiled.Count > 0 ? compiled.ToArray() : GetDefaultIgnoredPatterns();
    }

    internal static Regex[] GetDefaultIgnoredPatterns()
    {
        // Default patterns for cross-assembly clean-architecture scenarios
        return new[]
        {
            new Regex("^.*\\.Abstractions\\..*$", RegexOptions.Compiled),
            new Regex("^.*\\.Contracts\\..*$", RegexOptions.Compiled),
            new Regex("^.*\\.Interfaces\\..*$", RegexOptions.Compiled),
            new Regex("^.*\\.ILoggerService<.*>$", RegexOptions.Compiled)
        };
    }

    private static DiagnosticSeverity ParseDiagnosticSeverity(string severity)
        => severity.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            "hidden" => DiagnosticSeverity.Hidden,
            _ => DiagnosticSeverity.Error
        };

    public static DiagnosticDescriptor CreateDynamicDescriptor(DiagnosticDescriptor baseDescriptor,
        DiagnosticSeverity severity)
        => DiagnosticDescriptorFactory.WithSeverity(baseDescriptor, severity);
}
