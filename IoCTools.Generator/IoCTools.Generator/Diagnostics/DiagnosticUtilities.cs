namespace IoCTools.Generator.Diagnostics;

using System.Text.RegularExpressions;

using Helpers;

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

        // Parse IoCToolsFrameworkBaseTypes for custom framework base type exclusions
        if (tryGetValue("build_property.IoCToolsFrameworkBaseTypes", out var frameworkTypes))
            config.FrameworkBaseTypes = ParseFrameworkBaseTypes(frameworkTypes);

        // Parse IoCToolsExcludedNamespacePrefixes for cross-assembly scanning exclusions
        if (tryGetValue("build_property.IoCToolsExcludedNamespacePrefixes", out var excludedPrefixes))
            config.ExcludedNamespacePrefixes = ParseExcludedNamespacePrefixes(excludedPrefixes);

        // Parse IoCToolsConfigurationSuffixes for configuration type name processing
        if (tryGetValue("build_property.IoCToolsConfigurationSuffixes", out var configSuffixes))
            config.ConfigurationSuffixes = ParseConfigurationSuffixes(configSuffixes);

        // Parse IoCToolsSectionNameSuffixes for section name inference
        if (tryGetValue("build_property.IoCToolsSectionNameSuffixes", out var sectionSuffixes))
            config.SectionNameSuffixes = ParseSectionNameSuffixes(sectionSuffixes);

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

    internal static HashSet<string> ParseFrameworkBaseTypes(string frameworkTypesString)
    {
        if (string.IsNullOrWhiteSpace(frameworkTypesString))
            return Models.DiagnosticConfiguration.GetDefaultFrameworkBaseTypes();

        var types = frameworkTypesString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (types.Length == 0)
            return Models.DiagnosticConfiguration.GetDefaultFrameworkBaseTypes();

        // Start with defaults and merge with custom types
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in Models.DiagnosticConfiguration.GetDefaultFrameworkBaseTypes())
            result.Add(type);

        foreach (var type in types)
        {
            var trimmedType = type.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedType))
                result.Add(trimmedType);
        }

        return result;
    }

    internal static HashSet<string> ParseExcludedNamespacePrefixes(string excludedPrefixesString)
    {
        if (string.IsNullOrWhiteSpace(excludedPrefixesString))
            return Models.DiagnosticConfiguration.GetDefaultExcludedNamespacePrefixes();

        var prefixes = excludedPrefixesString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (prefixes.Length == 0)
            return Models.DiagnosticConfiguration.GetDefaultExcludedNamespacePrefixes();

        // Start with defaults and merge with custom prefixes
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prefix in Models.DiagnosticConfiguration.GetDefaultExcludedNamespacePrefixes())
            result.Add(prefix);

        foreach (var prefix in prefixes)
        {
            var trimmedPrefix = prefix.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedPrefix))
                result.Add(trimmedPrefix);
        }

        return result;
    }

    internal static HashSet<string> ParseConfigurationSuffixes(string suffixesString)
    {
        if (string.IsNullOrWhiteSpace(suffixesString))
            return Models.DiagnosticConfiguration.GetDefaultConfigurationSuffixes();

        var suffixes = suffixesString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (suffixes.Length == 0)
            return Models.DiagnosticConfiguration.GetDefaultConfigurationSuffixes();

        // Start with defaults and merge with custom suffixes
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var suffix in Models.DiagnosticConfiguration.GetDefaultConfigurationSuffixes())
            result.Add(suffix);

        foreach (var suffix in suffixes)
        {
            var trimmedSuffix = suffix.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedSuffix))
                result.Add(trimmedSuffix);
        }

        return result;
    }

    internal static HashSet<string> ParseSectionNameSuffixes(string suffixesString)
    {
        if (string.IsNullOrWhiteSpace(suffixesString))
            return Models.DiagnosticConfiguration.GetDefaultSectionNameSuffixes();

        var suffixes = suffixesString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (suffixes.Length == 0)
            return Models.DiagnosticConfiguration.GetDefaultSectionNameSuffixes();

        // Start with defaults and merge with custom suffixes
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var suffix in Models.DiagnosticConfiguration.GetDefaultSectionNameSuffixes())
            result.Add(suffix);

        foreach (var suffix in suffixes)
        {
            var trimmedSuffix = suffix.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedSuffix))
                result.Add(trimmedSuffix);
        }

        return result;
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
