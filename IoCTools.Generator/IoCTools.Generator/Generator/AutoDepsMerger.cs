namespace IoCTools.Generator.Generator;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using IoCTools.Generator.CodeGeneration;
using IoCTools.Generator.Models;
using IoCTools.Generator.Shared;
using IoCTools.Generator.Utilities;

using Microsoft.CodeAnalysis;

/// <summary>
/// Bridges the <see cref="AutoDepsResolver"/> output into the generator's
/// <see cref="InheritanceHierarchyDependencies"/> model. The merger walks the inheritance
/// chain upward from the current service. For each ancestor that will itself receive a
/// generated constructor, the resolver is re-run with the same options so per-level
/// open-generic closure (e.g. <c>ILogger&lt;T&gt;</c>) picks up the correct concrete type for
/// that ancestor. Each resolved entry becomes a <see cref="DependencySource.DependsOn"/>
/// entry at the ancestor's level, which means the downstream constructor rendering --
/// particularly <see cref="BaseConstructorCallBuilder"/> -- sees the full hierarchy shape
/// and threads every ancestor's auto-deps through <c>base(...)</c>.
/// Attribution (universal vs profile vs built-in ILogger etc.) is NOT stored on the
/// hierarchy; it remains in the resolver output for downstream diagnostics and the CLI
/// report.
/// </summary>
internal static class AutoDepsMerger
{
    public static void MergeAutoDepsIntoHierarchy(
        InheritanceHierarchyDependencies hierarchy,
        Compilation compilation,
        INamedTypeSymbol serviceSymbol,
        IReadOnlyDictionary<string, string> autoDepsOptions)
    {
        // Walk the inheritance chain from the current service upward, merging each
        // ancestor's (including the current service) resolved auto-deps at the correct
        // level. Level 0 is the current service, Level 1 is its direct parent, etc.
        var current = serviceSymbol;
        var level = 0;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            // Only merge ancestors that will themselves go through the generator.
            // Otherwise their auto-deps wouldn't be provided by any generated ctor and
            // forwarding them through base() would be nonsensical.
            if (level == 0 || WillHaveGeneratedConstructor(current))
            {
                var resolved = AutoDepsResolver.ResolveForServiceWithSymbols(
                    compilation, current, autoDepsOptions);
                MergeResolvedEntries(hierarchy, resolved, level);
            }

            current = current.BaseType;
            level++;
        }
    }

    /// <summary>
    /// Computes the full set of resolved auto-dep entries for a single service symbol,
    /// carrying the level that an ancestor lookup should use (typically 0 since the caller
    /// is rendering that service's own constructor). Used by <see cref="BaseConstructorCallBuilder"/>
    /// to obtain the base class's merged dependency list in the same shape the base's own
    /// constructor would see.
    /// </summary>
    public static ImmutableArray<AutoDepResolvedSymbolEntry> ResolveEntries(
        Compilation compilation,
        INamedTypeSymbol serviceSymbol,
        IReadOnlyDictionary<string, string> autoDepsOptions)
    {
        return AutoDepsResolver.ResolveForServiceWithSymbols(compilation, serviceSymbol, autoDepsOptions);
    }

    private static void MergeResolvedEntries(
        InheritanceHierarchyDependencies hierarchy,
        ImmutableArray<AutoDepResolvedSymbolEntry> resolved,
        int level)
    {
        if (resolved.IsDefaultOrEmpty) return;

        // Existing types (by symbol identity) at this level -- prevent double-adding the
        // same auto-dep on a single level if the resolver produced duplicates (shouldn't
        // happen, but belt-and-suspenders).
        var existingAtLevel = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var raw in hierarchy.RawAllDependencies)
            if (raw.Level == level)
                existingAtLevel.Add(raw.ServiceType);

        // Types already present elsewhere in the hierarchy. Used to decide whether a
        // new auto-dep at this level needs a disambiguating field name (to avoid
        // collisions with an existing _logger from another level).
        var existingFieldNames = new HashSet<string>(
            hierarchy.RawAllDependencies.Select(d => d.FieldName));

        foreach (var entry in resolved)
        {
            if (existingAtLevel.Contains(entry.DepType)) continue;
            existingAtLevel.Add(entry.DepType);

            var fieldName = ComputeFieldName(entry.DepType, existingFieldNames);
            existingFieldNames.Add(fieldName);

            // Slot the entry into every collection the downstream generator reads.
            hierarchy.RawAllDependencies.Add(
                (entry.DepType, fieldName, DependencySource.DependsOn, level));
            hierarchy.AllDependenciesWithExternalFlag.Add(
                (entry.DepType, fieldName, DependencySource.DependsOn, false));

            // Avoid AllDependencies duplicates when the type is already present (e.g. an
            // explicit [DependsOn<T>] at a different level). Reconciliation against
            // explicit DependsOn happens inside the resolver itself, but inheritance can
            // still produce duplicates across levels for non-AutoDep entries.
            if (!hierarchy.AllDependencies.Any(d =>
                    SymbolEqualityComparer.Default.Equals(d.ServiceType, entry.DepType) &&
                    d.FieldName == fieldName))
            {
                hierarchy.AllDependencies.Add((entry.DepType, fieldName, DependencySource.DependsOn));
            }

            if (level == 0)
                hierarchy.DerivedDependencies.Add((entry.DepType, fieldName, DependencySource.DependsOn));
            else
                hierarchy.BaseDependencies.Add((entry.DepType, fieldName, DependencySource.DependsOn));
        }
    }

    /// <summary>
    /// Computes a field name for a resolved auto-dep entry.
    /// Starts with the standard <c>GenerateFieldName(GetMeaningfulTypeName(T))</c> lookup.
    /// If that name already exists in the hierarchy (typical for <c>ILogger&lt;TSelf&gt;</c>
    /// and <c>ILogger&lt;TBase&gt;</c>, which both strip to <c>_logger</c>), the closed type
    /// argument is appended (e.g. <c>_loggerOfBaseSvc</c>) so base() chaining can thread
    /// distinct instances for distinct closed generics.
    /// </summary>
    private static string ComputeFieldName(ITypeSymbol depType, HashSet<string> existing)
    {
        var baseName = AttributeParser.GenerateFieldName(
            TypeUtilities.GetMeaningfulTypeName(depType),
            "CamelCase",
            stripI: true,
            prefix: "_");

        if (!existing.Contains(baseName)) return baseName;

        // Disambiguate: append the concrete type arg(s) so ILogger<Base> and ILogger<Derived>
        // don't collide on _logger.
        if (depType is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length > 0)
        {
            var suffix = string.Join("And", named.TypeArguments.Select(t => t.Name));
            var disambiguated = $"{baseName}Of{suffix}";
            if (!existing.Contains(disambiguated)) return disambiguated;
        }

        // Last-resort numeric suffix.
        var counter = 2;
        while (existing.Contains($"{baseName}{counter}")) counter++;
        return $"{baseName}{counter}";
    }

    /// <summary>
    /// Mirrors <see cref="BaseConstructorCallBuilder.WillHaveGeneratedConstructor"/>'s private
    /// logic for detecting "ancestor will receive a generated constructor". Kept in sync
    /// with that method's heuristic: the generator emits a ctor for anything that carries
    /// a lifetime/registration attribute, <c>[DependsOn]</c>, <c>[Inject]</c> fields, etc.
    /// </summary>
    private static bool WillHaveGeneratedConstructor(INamedTypeSymbol type)
    {
        var hasInjectFields = type.GetMembers().OfType<IFieldSymbol>()
            .Any(f => f.GetAttributes().Any(a => a.AttributeClass?.Name == "InjectAttribute"));
        var hasInjectConfigFields = type.GetMembers().OfType<IFieldSymbol>()
            .Any(f => f.GetAttributes().Any(a => a.AttributeClass?.Name == "InjectConfigurationAttribute"));

        foreach (var a in type.GetAttributes())
        {
            var cls = a.AttributeClass;
            if (cls is null) continue;
            var name = cls.Name;
            if (name == "DependsOnAttribute" ||
                name == "ConditionalServiceAttribute" ||
                name == "RegisterAsAllAttribute" ||
                name == "ScopedAttribute" ||
                name == "SingletonAttribute" ||
                name == "TransientAttribute" ||
                name.StartsWith("RegisterAsAttribute"))
            {
                return true;
            }
        }

        return hasInjectFields || hasInjectConfigFields;
    }
}
