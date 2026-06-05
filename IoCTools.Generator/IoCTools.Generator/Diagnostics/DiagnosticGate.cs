namespace IoCTools.Generator.Diagnostics;

using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
///     Centralized scope-gating helper. Every IoCTools diagnostic that distinguishes
///     between production and test consumers routes through <see cref="ShouldReport" />
///     so the rule itself does not have to embed test-detection logic.
///     <para>
///         Test-project detection follows the standard Roslyn pattern: <c>Microsoft.NET.Test.Sdk</c>
///         sets the <c>IsTestProject</c> MSBuild property automatically. IoCTools forwards that
///         property to analyzers via <c>build/IoCTools.Generator.targets</c> as
///         <c>CompilerVisibleProperty Include="IsTestProject"</c>, which surfaces here as
///         <c>build_property.IsTestProject</c>. No naming heuristics, no test-framework reference
///         detection.
///     </para>
/// </summary>
internal static class DiagnosticGate
{
    private const string IsTestProjectKey = "build_property.IsTestProject";

    /// <summary>
    ///     Maps diagnostic IDs to their declared <see cref="AnalysisScope" />. Diagnostics not
    ///     listed here default to <see cref="AnalysisScope.Both" />, which is the safe choice
    ///     for correctness defects.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, AnalysisScope> ScopeById =
        new Dictionary<string, AnalysisScope>(StringComparer.Ordinal)
        {
            // Production DI graph, registration, style, and opportunity diagnostics. Test projects
            // frequently register fakes, omit full app graphs, construct semantic harnesses, or
            // define throwaway helper services. These diagnostics are useful in app/library projects
            // and noisy or misleading in test projects.
            { "IOC001", AnalysisScope.Production },
            { "IOC002", AnalysisScope.Production },
            { "IOC003", AnalysisScope.Production },
            { "IOC005", AnalysisScope.Production },
            { "IOC006", AnalysisScope.Production },
            { "IOC008", AnalysisScope.Production },
            { "IOC009", AnalysisScope.Production },
            { "IOC012", AnalysisScope.Production },
            { "IOC013", AnalysisScope.Production },
            { "IOC015", AnalysisScope.Production },
            { "IOC027", AnalysisScope.Production },
            { "IOC032", AnalysisScope.Production },
            { "IOC033", AnalysisScope.Production },
            { "IOC034", AnalysisScope.Production },
            { "IOC035", AnalysisScope.Production },
            { "IOC039", AnalysisScope.Production },
            { "IOC040", AnalysisScope.Production },
            { "IOC042", AnalysisScope.Production },
            { "IOC043", AnalysisScope.Production },
            { "IOC046", AnalysisScope.Production },
            { "IOC047", AnalysisScope.Production },
            { "IOC052", AnalysisScope.Production },
            { "IOC053", AnalysisScope.Production },
            { "IOC054", AnalysisScope.Production },
            { "IOC055", AnalysisScope.Production },
            { "IOC056", AnalysisScope.Production },
            { "IOC057", AnalysisScope.Production },
            { "IOC058", AnalysisScope.Production },
            { "IOC059", AnalysisScope.Production },
            { "IOC060", AnalysisScope.Production },
            { "IOC061", AnalysisScope.Production },
            { "IOC062", AnalysisScope.Production },
            { "IOC063", AnalysisScope.Production },
            { "IOC064", AnalysisScope.Production },
            { "IOC065", AnalysisScope.Production },
            { "IOC067", AnalysisScope.Production },
            { "IOC068", AnalysisScope.Production },
            { "IOC069", AnalysisScope.Production },
            { "IOC070", AnalysisScope.Production },
            { "IOC071", AnalysisScope.Production },
            { "IOC072", AnalysisScope.Production },
            { "IOC074", AnalysisScope.Production },
            { "IOC075", AnalysisScope.Production },
            { "IOC076", AnalysisScope.Production },
            { "IOC078", AnalysisScope.Production },
            { "IOC079", AnalysisScope.Production },
            { "IOC081", AnalysisScope.Production },
            { "IOC082", AnalysisScope.Production },
            { "IOC083", AnalysisScope.Production },
            { "IOC084", AnalysisScope.Production },
            { "IOC085", AnalysisScope.Production },
            { "IOC086", AnalysisScope.Production },
            { "IOC087", AnalysisScope.Production },
            { "IOC090", AnalysisScope.Production },
            { "IOC091", AnalysisScope.Production },
            { "IOC092", AnalysisScope.Production },
            { "IOC094", AnalysisScope.Production },
            { "IOC096", AnalysisScope.Production },
            { "IOC097", AnalysisScope.Production },
            { "IOC098", AnalysisScope.Production },
            { "IOC099", AnalysisScope.Production },
            { "IOC103", AnalysisScope.Production },
            { "IOC104", AnalysisScope.Production },
            { "IOC105", AnalysisScope.Production },
            { "IOC106", AnalysisScope.Production },
            { "IOC107", AnalysisScope.Production },
            { "IOC108", AnalysisScope.Production },
            { "IOC110", AnalysisScope.Production },
            { "IOC113", AnalysisScope.Production },

            // Test fixture diagnostics are useful only in test projects.
            { "TDIAG01", AnalysisScope.Test },
            { "TDIAG02", AnalysisScope.Test },
            { "TDIAG03", AnalysisScope.Test },
            { "TDIAG04", AnalysisScope.Test },
            { "TDIAG05", AnalysisScope.Test },
            { "TDIAG06", AnalysisScope.Test },
            { "TDIAG07", AnalysisScope.Test },
            { "TDIAG08", AnalysisScope.Test },

            // All other diagnostics are implicit AnalysisScope.Both. Examples:
            //   - IOC073 (open-generic IHostedService): broken codegen — fires everywhere.
            //   - IOC109 (inaccessible IHostedService): broken codegen — fires everywhere.
            // Production DI health rules, including lifetime-capture guidance, are listed above so
            // test projects focus on fixture diagnostics and generated-code correctness.
        };

    /// <summary>
    ///     Returns <c>true</c> if a diagnostic with the given descriptor should be reported in
    ///     the consuming project. Production-scoped diagnostics are suppressed in test projects;
    ///     test-scoped diagnostics are suppressed in non-test projects; <see cref="AnalysisScope.Both" />
    ///     diagnostics always report.
    /// </summary>
    public static bool ShouldReport(AnalyzerConfigOptionsProvider options,
        DiagnosticDescriptor descriptor)
    {
        if (descriptor is null) return true;
        var scope = GetScope(descriptor.Id);
        if (scope == AnalysisScope.Both) return true;

        var isTest = IsTestProject(options);
        return scope switch
        {
            AnalysisScope.Production => !isTest,
            AnalysisScope.Test => isTest,
            _ => true
        };
    }

    /// <summary>
    ///     Same as <see cref="ShouldReport" />, but takes the boolean directly for call sites
    ///     that have already resolved <c>IsTestProject</c> once for a batch of diagnostics.
    /// </summary>
    public static bool ShouldReport(bool isTestProject, DiagnosticDescriptor descriptor)
    {
        if (descriptor is null) return true;
        var scope = GetScope(descriptor.Id);
        return scope switch
        {
            AnalysisScope.Production => !isTestProject,
            AnalysisScope.Test => isTestProject,
            _ => true
        };
    }

    /// <summary>
    ///     Reads the <c>IsTestProject</c> MSBuild property forwarded as a compiler-visible
    ///     property. Returns <c>false</c> when the property is missing or unparseable, which
    ///     matches the safe default (treat unknown projects as production so production-scoped
    ///     diagnostics still fire).
    /// </summary>
    public static bool IsTestProject(AnalyzerConfigOptionsProvider options)
    {
        if (options is null) return false;
        return options.GlobalOptions.TryGetValue(IsTestProjectKey, out var raw)
               && bool.TryParse(raw, out var parsed)
               && parsed;
    }

    /// <summary>
    ///     Looks up the declared scope for a diagnostic ID. Defaults to
    ///     <see cref="AnalysisScope.Both" /> when the ID is not registered.
    /// </summary>
    public static AnalysisScope GetScope(string diagnosticId)
        => diagnosticId is not null && ScopeById.TryGetValue(diagnosticId, out var scope)
            ? scope
            : AnalysisScope.Both;
}
