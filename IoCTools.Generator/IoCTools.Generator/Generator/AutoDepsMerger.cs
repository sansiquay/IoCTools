namespace IoCTools.Generator.Generator;

using System.Collections.Generic;

using IoCTools.Generator.Shared;
using IoCTools.Generator.Utilities;

/// <summary>
/// Bridges the <see cref="AutoDepsResolver"/> output into the generator's
/// <see cref="InheritanceHierarchyDependencies"/> model. Each resolved entry is surfaced as a
/// Level-0 <see cref="DependencySource.DependsOn"/> dependency so the existing constructor
/// rendering picks it up without further changes. Attribution (universal vs profile vs
/// built-in ILogger etc.) is NOT stored on the hierarchy — it remains in the resolver output
/// for downstream diagnostics and the CLI report.
/// </summary>
internal static class AutoDepsMerger
{
    public static void MergeAutoDepsIntoHierarchy(
        InheritanceHierarchyDependencies hierarchy,
        Compilation compilation,
        INamedTypeSymbol serviceSymbol,
        IReadOnlyDictionary<string, string> autoDepsOptions)
    {
        var resolved = AutoDepsResolver.ResolveForServiceWithSymbols(
            compilation, serviceSymbol, autoDepsOptions);

        if (resolved.IsDefaultOrEmpty) return;

        // Dedup against any dep type already present in the hierarchy so double-adding can't
        // occur if a user explicitly wrote [DependsOn<T>] for a type that the resolver also
        // surfaced. The resolver itself drops DependsOn-matching entries via
        // ReconcileAgainstDependsOn, so this guard is belt-and-suspenders.
        var existingTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var dep in hierarchy.AllDependencies)
            existingTypes.Add(dep.ServiceType);

        foreach (var entry in resolved)
        {
            if (existingTypes.Contains(entry.DepType)) continue;
            existingTypes.Add(entry.DepType);

            var fieldName = AttributeParser.GenerateFieldName(
                TypeUtilities.GetMeaningfulTypeName(entry.DepType),
                "CamelCase",
                stripI: true,
                prefix: "_");

            hierarchy.AllDependencies.Add((entry.DepType, fieldName, DependencySource.DependsOn));
            hierarchy.DerivedDependencies.Add((entry.DepType, fieldName, DependencySource.DependsOn));
            hierarchy.AllDependenciesWithExternalFlag.Add(
                (entry.DepType, fieldName, DependencySource.DependsOn, false));
            hierarchy.RawAllDependencies.Add(
                (entry.DepType, fieldName, DependencySource.DependsOn, 0));
        }
    }
}
