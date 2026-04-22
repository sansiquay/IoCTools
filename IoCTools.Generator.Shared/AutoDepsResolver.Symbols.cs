namespace IoCTools.Generator.Shared;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

/// <summary>
/// Symbol-bearing counterpart to <see cref="AutoDepResolvedEntry"/>.
/// Unlike <see cref="AutoDepResolvedEntry"/>, this carries the real <see cref="ITypeSymbol"/>
/// reference -- it is therefore not safe to cache across compilations, but is useful at emit
/// time where the generator needs actual symbols to feed into the constructor parameter list.
/// </summary>
public readonly struct AutoDepResolvedSymbolEntry
{
    public AutoDepResolvedSymbolEntry(ITypeSymbol depType, ImmutableArray<AutoDepAttribution> sources)
    {
        DepType = depType;
        Sources = sources.IsDefault ? ImmutableArray<AutoDepAttribution>.Empty : sources;
    }

    public ITypeSymbol DepType { get; }
    public ImmutableArray<AutoDepAttribution> Sources { get; }

    public AutoDepAttribution PrimarySource
    {
        get
        {
            if (Sources.IsDefaultOrEmpty) return default;
            // Precedence order for display:
            // explicit > auto-profile > auto-universal > auto-transitive > auto-builtin
            foreach (var kind in new[]
                     {
                         AutoDepSourceKind.Explicit,
                         AutoDepSourceKind.AutoProfile,
                         AutoDepSourceKind.AutoUniversal,
                         AutoDepSourceKind.AutoOpenUniversal,
                         AutoDepSourceKind.AutoTransitive,
                         AutoDepSourceKind.AutoBuiltinILogger
                     })
            {
                foreach (var s in Sources)
                    if (s.Kind == kind) return s;
            }
            return Sources[0];
        }
    }
}

public static partial class AutoDepsResolver
{
    /// <summary>
    /// Runs the full resolver pipeline and returns <see cref="ITypeSymbol"/>-carrying entries
    /// for each resolved dep. Functionally equivalent to <see cref="ResolveForService"/> but
    /// suitable for the generator emit path, which needs symbols to build constructor
    /// parameters. The equatable <see cref="ResolveForService"/> remains the public API for
    /// tests and incremental pipelines that rely on value-equality caching.
    /// </summary>
    public static ImmutableArray<AutoDepResolvedSymbolEntry> ResolveForServiceWithSymbols(
        Compilation compilation,
        INamedTypeSymbol serviceSymbol,
        IReadOnlyDictionary<string, string> msbuildProperties)
    {
        if (compilation is null) throw new ArgumentNullException(nameof(compilation));
        if (serviceSymbol is null) throw new ArgumentNullException(nameof(serviceSymbol));
        if (msbuildProperties is null) throw new ArgumentNullException(nameof(msbuildProperties));

        // Kill switch
        if (GetBool(msbuildProperties, "build_property.IoCToolsAutoDepsDisable") == true)
            return ImmutableArray<AutoDepResolvedSymbolEntry>.Empty;

        var excludeGlob = GetString(msbuildProperties, "build_property.IoCToolsAutoDepsExcludeGlob");
        if (!string.IsNullOrEmpty(excludeGlob))
        {
            var ns = serviceSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (GlobMatch(ns, excludeGlob!, out _))
                return ImmutableArray<AutoDepResolvedSymbolEntry>.Empty;
        }

        if (ResolutionBuilder.HasManualConstructor(serviceSymbol))
            return ImmutableArray<AutoDepResolvedSymbolEntry>.Empty;

        var builder = new ResolutionBuilder(compilation, serviceSymbol);

        if (GetBool(msbuildProperties, "build_property.IoCToolsAutoDetectLogger") != false)
            builder.AddBuiltinILoggerIfAvailable();

        builder.AddUniversalFromAttributes();
        builder.ApplyProfiles();
        builder.ApplyOptOuts();
        builder.ReconcileAgainstDependsOn();

        return builder.BuildWithSymbols();
    }
}
