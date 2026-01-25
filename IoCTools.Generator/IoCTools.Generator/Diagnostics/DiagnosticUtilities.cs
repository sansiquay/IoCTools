namespace IoCTools.Generator.Diagnostics;

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

        return config;
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
