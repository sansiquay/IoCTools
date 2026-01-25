namespace IoCTools.Generator.Models;

using System.Text.RegularExpressions;

public class DiagnosticConfiguration
{
    public DiagnosticSeverity NoImplementationSeverity { get; set; } = DiagnosticSeverity.Error;
    public DiagnosticSeverity ManualImplementationSeverity { get; set; } = DiagnosticSeverity.Error;
    public DiagnosticSeverity LifetimeValidationSeverity { get; set; } = DiagnosticSeverity.Error;
    public bool DiagnosticsEnabled { get; set; } = true;
    public bool LifetimeValidationEnabled { get; set; } = true;

    // Specific diagnostic severity overrides for educational examples
    public DiagnosticSeverity ConditionalServiceValidationSeverity { get; set; } = DiagnosticSeverity.Warning;
    public DiagnosticSeverity PartialClassValidationSeverity { get; set; } = DiagnosticSeverity.Warning;
    public DiagnosticSeverity BackgroundServiceValidationSeverity { get; set; } = DiagnosticSeverity.Warning;
    public DiagnosticSeverity InheritanceChainValidationSeverity { get; set; } = DiagnosticSeverity.Warning;

    // Compiled regex patterns for matching cross-assembly interfaces to ignore
    // These allow configuration of interfaces that are provided by external assemblies
    // without requiring IOC001/IOC002 diagnostics
    public Regex[] CompiledIgnoredPatterns { get; set; } = Array.Empty<Regex>();
}
