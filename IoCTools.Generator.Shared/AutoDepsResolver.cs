namespace IoCTools.Generator.Shared;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

public static partial class AutoDepsResolver
{
    private const string MelIloggerMetadataName = "Microsoft.Extensions.Logging.ILogger`1";

    public static bool IsBuiltinILoggerAvailable(Compilation compilation)
    {
        if (compilation == null) throw new ArgumentNullException(nameof(compilation));
        return compilation.GetTypeByMetadataName(MelIloggerMetadataName) is { };
    }

    internal static INamedTypeSymbol? GetBuiltinILoggerSymbol(Compilation compilation) =>
        compilation.GetTypeByMetadataName(MelIloggerMetadataName);

    /// <summary>
    /// Resolves the auto-dependency set for a single service according to the spec's
    /// Resolution Order (steps 1-7). Called per service by the generator pipeline.
    /// </summary>
    /// <remarks>
    /// MSBuild properties are passed as a plain dictionary keyed by the full MSBuild property
    /// name (e.g. <c>"build_property.IoCToolsAutoDepsDisable"</c>) so the same resolver is
    /// reusable from the CLI without taking a dependency on <c>AnalyzerConfigOptions</c>.
    /// </remarks>
    public static AutoDepsResolverOutput ResolveForService(
        Compilation compilation,
        INamedTypeSymbol serviceSymbol,
        IReadOnlyDictionary<string, string> msbuildProperties)
    {
        if (compilation is null) throw new ArgumentNullException(nameof(compilation));
        if (serviceSymbol is null) throw new ArgumentNullException(nameof(serviceSymbol));
        if (msbuildProperties is null) throw new ArgumentNullException(nameof(msbuildProperties));

        // Kill switch
        if (GetBool(msbuildProperties, "build_property.IoCToolsAutoDepsDisable") == true)
            return AutoDepsResolverOutput.Empty;

        // Exclude glob — matched against the service's containing namespace
        var excludeGlob = GetString(msbuildProperties, "build_property.IoCToolsAutoDepsExcludeGlob");
        if (!string.IsNullOrEmpty(excludeGlob))
        {
            var ns = serviceSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (GlobMatch(ns, excludeGlob!, out _))
                return AutoDepsResolverOutput.Empty;
        }

        // Manual constructor — skip auto-deps entirely (step 6)
        if (ResolutionBuilder.HasManualConstructor(serviceSymbol))
            return AutoDepsResolverOutput.Empty;

        var builder = new ResolutionBuilder(compilation, serviceSymbol);

        // Step 1: universal (built-in + local + transitive)
        if (GetBool(msbuildProperties, "build_property.IoCToolsAutoDetectLogger") != false)
            builder.AddBuiltinILoggerIfAvailable();

        builder.AddUniversalFromAttributes();

        // Step 2: profiles (local + transitive)
        builder.ApplyProfiles();

        // Step 3+4: opt-outs (NoAutoDeps, NoAutoDep, NoAutoDepOpen)
        builder.ApplyOptOuts();

        // Step 5: DependsOn reconciliation
        builder.ReconcileAgainstDependsOn();

        return builder.Build();
    }

    /// <summary>
    /// Variant of <see cref="ResolveForService"/> that ALSO returns resolver-internal diagnostic
    /// signals (stale opt-outs, DependsOn overlaps, open-generic constraint violations,
    /// redundant profile attachments). Signals carry no <see cref="Location"/> -- the validator
    /// looks up the appropriate location by re-walking the compilation.
    /// </summary>
    public static (AutoDepsResolverOutput Output, ImmutableArray<AutoDepDiagnosticSignal> Signals)
        ResolveForServiceWithDiagnostics(
            Compilation compilation,
            INamedTypeSymbol serviceSymbol,
            IReadOnlyDictionary<string, string> msbuildProperties)
    {
        if (compilation is null) throw new ArgumentNullException(nameof(compilation));
        if (serviceSymbol is null) throw new ArgumentNullException(nameof(serviceSymbol));
        if (msbuildProperties is null) throw new ArgumentNullException(nameof(msbuildProperties));

        if (GetBool(msbuildProperties, "build_property.IoCToolsAutoDepsDisable") == true)
            return (AutoDepsResolverOutput.Empty, ImmutableArray<AutoDepDiagnosticSignal>.Empty);

        var excludeGlob = GetString(msbuildProperties, "build_property.IoCToolsAutoDepsExcludeGlob");
        if (!string.IsNullOrEmpty(excludeGlob))
        {
            var ns = serviceSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (GlobMatch(ns, excludeGlob!, out _))
                return (AutoDepsResolverOutput.Empty, ImmutableArray<AutoDepDiagnosticSignal>.Empty);
        }

        if (ResolutionBuilder.HasManualConstructor(serviceSymbol))
            return (AutoDepsResolverOutput.Empty, ImmutableArray<AutoDepDiagnosticSignal>.Empty);

        var builder = new ResolutionBuilder(compilation, serviceSymbol);

        if (GetBool(msbuildProperties, "build_property.IoCToolsAutoDetectLogger") != false)
            builder.AddBuiltinILoggerIfAvailable();

        builder.AddUniversalFromAttributes();
        builder.ApplyProfiles();
        builder.ApplyOptOuts();
        builder.ReconcileAgainstDependsOn();

        return (builder.Build(), builder.Signals);
    }

    private static bool? GetBool(IReadOnlyDictionary<string, string> props, string key) =>
        props.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : (bool?)null;

    private static string? GetString(IReadOnlyDictionary<string, string> props, string key) =>
        props.TryGetValue(key, out var v) ? v : null;
}
