namespace IoCTools.Generator.Generator;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using IoCTools.Generator.Shared;

using Microsoft.CodeAnalysis;

/// <summary>
/// Emits the human-readable auto-deps debug report that is prepended to each generated
/// constructor file when the <c>IoCToolsAutoDepsReport=true</c> MSBuild property is set.
///
/// The report is a comment block grouped by source kind (Universal, Transitive,
/// Profile:&lt;name&gt;, Explicit (DependsOn), Suppressed). Formatting matches the spec
/// in docs/superpowers/specs/2026-04-22-auto-deps-and-inject-deprecation-design.md.
/// Attribution metadata comes straight off the resolver's
/// <see cref="AutoDepResolvedSymbolEntry"/> output so we don't have to stash it on the
/// hierarchy model.
/// </summary>
internal static class AutoDepsReporter
{
    public static string BuildReport(
        INamedTypeSymbol serviceSymbol,
        ImmutableArray<AutoDepResolvedSymbolEntry> resolved,
        IEnumerable<string> suppressedTypeDescriptions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// === Auto-Deps Report for {serviceSymbol.Name} ===");

        var resolvedEntries = resolved.IsDefault ? ImmutableArray<AutoDepResolvedSymbolEntry>.Empty : resolved;

        var universal = resolvedEntries.Where(e =>
            e.PrimarySource.Kind == AutoDepSourceKind.AutoBuiltinILogger ||
            e.PrimarySource.Kind == AutoDepSourceKind.AutoUniversal ||
            e.PrimarySource.Kind == AutoDepSourceKind.AutoOpenUniversal).ToList();
        if (universal.Count > 0)
        {
            sb.AppendLine("// Universal:");
            foreach (var e in universal)
                sb.AppendLine(
                    $"//   - {e.DepType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}   ({FormatSource(e.PrimarySource)})");
        }

        var transitive = resolvedEntries
            .Where(e => e.PrimarySource.Kind == AutoDepSourceKind.AutoTransitive)
            .ToList();
        if (transitive.Count > 0)
        {
            sb.AppendLine("// Transitive:");
            foreach (var e in transitive)
                sb.AppendLine(
                    $"//   - {e.DepType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}   (from {e.PrimarySource.AssemblyName})");
        }

        var profileGroups = resolvedEntries
            .Where(e => e.PrimarySource.Kind == AutoDepSourceKind.AutoProfile)
            .GroupBy(e => e.PrimarySource.SourceName ?? string.Empty, System.StringComparer.Ordinal)
            .OrderBy(g => g.Key, System.StringComparer.Ordinal);
        foreach (var g in profileGroups)
        {
            sb.AppendLine($"// Profile: {g.Key}");
            foreach (var e in g)
                sb.AppendLine(
                    $"//   - {e.DepType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}");
        }

        var explicits = CollectExplicitDependsOn(serviceSymbol).ToList();
        if (explicits.Count > 0)
        {
            sb.AppendLine("// Explicit (DependsOn):");
            foreach (var t in explicits)
                sb.AppendLine(
                    $"//   - {t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}");
        }

        var suppressed = suppressedTypeDescriptions?.ToList() ?? new List<string>();
        sb.AppendLine("// Suppressed:");
        if (suppressed.Count > 0)
        {
            foreach (var s in suppressed)
                sb.AppendLine($"//   - {s}");
        }
        else
        {
            sb.AppendLine("//   (none)");
        }

        return sb.ToString();
    }

    private static string FormatSource(AutoDepAttribution a) => a.Kind switch
    {
        AutoDepSourceKind.AutoBuiltinILogger => "from AutoDepOpen(typeof(ILogger<>))",
        AutoDepSourceKind.AutoOpenUniversal => "from AutoDepOpen",
        AutoDepSourceKind.AutoUniversal => "from AutoDep<T>",
        _ => a.ToTag()
    };

    private static IEnumerable<ITypeSymbol> CollectExplicitDependsOn(INamedTypeSymbol svc)
    {
        foreach (var attr in svc.GetAttributes())
        {
            var cls = attr.AttributeClass;
            if (cls is null) continue;
            if (!cls.Name.StartsWith("DependsOnAttribute", System.StringComparison.Ordinal)) continue;
            if (cls.TypeArguments.IsDefaultOrEmpty) continue;
            foreach (var t in cls.TypeArguments)
                if (t is ITypeSymbol ts)
                    yield return ts;
        }
    }

    /// <summary>
    /// Scans the service symbol for <c>[NoAutoDeps]</c>, <c>[NoAutoDep&lt;T&gt;]</c>, and
    /// <c>[NoAutoDepOpen(typeof(T&lt;&gt;))]</c> attributes and returns a readable description
    /// for each so the reporter can list them under the Suppressed section.
    /// </summary>
    public static IEnumerable<string> CollectSuppressedDescriptions(INamedTypeSymbol svc)
    {
        foreach (var attr in svc.GetAttributes())
        {
            var cls = attr.AttributeClass;
            if (cls is null) continue;

            var name = cls.Name;

            if (name == "NoAutoDepsAttribute")
            {
                yield return "all (via [NoAutoDeps])";
                continue;
            }

            if (name.StartsWith("NoAutoDepAttribute", System.StringComparison.Ordinal)
                && !cls.TypeArguments.IsDefaultOrEmpty)
            {
                foreach (var t in cls.TypeArguments)
                    yield return $"{t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} (via [NoAutoDep<{t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}>])";
                continue;
            }

            if (name == "NoAutoDepOpenAttribute")
            {
                var typeArg = attr.ConstructorArguments.Length > 0
                    ? attr.ConstructorArguments[0].Value as ITypeSymbol
                    : null;
                var label = typeArg is null
                    ? "open-shape"
                    : $"{typeArg.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} open-shape";
                yield return $"{label} (via [NoAutoDepOpen])";
            }
        }
    }
}
