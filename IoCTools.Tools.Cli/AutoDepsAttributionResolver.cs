namespace IoCTools.Tools.Cli;

using System.Collections.Immutable;

using Generator.Shared;

using Microsoft.CodeAnalysis;

/// <summary>
/// Bridges the CLI to the shared <see cref="AutoDepsResolver"/> so CLI printers can surface
/// per-dependency <see cref="AutoDepAttribution"/> alongside the existing field metadata.
/// </summary>
/// <remarks>
/// The resolver expects MSBuild properties keyed as <c>build_property.{Name}</c>. The CLI
/// surfaces these from the <see cref="Microsoft.CodeAnalysis.Project"/>'s
/// <c>AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions</c> when available; the
/// dictionary is an empty fallback so the resolver defaults hold.
/// </remarks>
internal static class AutoDepsAttributionResolver
{
    private static readonly string[] RelevantKeys =
    {
        "build_property.IoCToolsAutoDepsDisable",
        "build_property.IoCToolsAutoDepsExcludeGlob",
        "build_property.IoCToolsAutoDepsReport",
        "build_property.IoCToolsAutoDetectLogger",
        "build_property.IoCToolsInjectDeprecationSeverity"
    };

    public static IReadOnlyDictionary<string, string> BuildMsBuildProperties(Project project)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var provider = project.AnalyzerOptions?.AnalyzerConfigOptionsProvider;
            if (provider == null) return dict;
            var globals = provider.GlobalOptions;
            foreach (var key in RelevantKeys)
            {
                if (globals.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    dict[key] = value;
            }
        }
        catch
        {
            // If AnalyzerOptions is unavailable (test workspace / old roslyn),
            // return an empty dictionary and let the resolver apply defaults.
        }

        return dict;
    }

    /// <summary>
    /// Builds a lookup keyed by dependency type fully-qualified name so the CLI can
    /// side-channel <see cref="AutoDepAttribution"/> onto existing field metadata
    /// without restructuring <see cref="ServiceFieldReport.DependencyFields"/>.
    /// </summary>
    /// <remarks>
    /// In CLI context, the <see cref="Compilation"/> returned by <c>Project.GetCompilationAsync</c>
    /// includes output from source generators that have already emitted constructors for
    /// IoCTools-enabled partial classes. The resolver's <c>HasManualConstructor</c> gate would
    /// otherwise treat the generator-emitted constructor as user-authored and skip auto-deps
    /// entirely. Here we detect whether every constructor declaration lives in a <c>.g.cs</c>
    /// syntax tree and, if so, resolve against a <see cref="Compilation"/> clone that excludes
    /// those trees. That mirrors the generator's own pre-emit view.
    /// </remarks>
    public static ImmutableDictionary<string, AutoDepAttribution> ResolveAttributions(
        Compilation compilation,
        INamedTypeSymbol serviceSymbol,
        IReadOnlyDictionary<string, string> msbuildProperties)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, AutoDepAttribution>(StringComparer.Ordinal);
        try
        {
            var effectiveCompilation = StripGeneratedTrees(compilation);
            var effectiveSymbol = serviceSymbol;
            if (!ReferenceEquals(effectiveCompilation, compilation))
            {
                var metadataName = serviceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Replace("global::", string.Empty, StringComparison.Ordinal);
                var resolved = effectiveCompilation.GetTypeByMetadataName(metadataName);
                if (resolved != null)
                    effectiveSymbol = resolved;
            }

            var entries = AutoDepsResolver.ResolveForServiceWithSymbols(effectiveCompilation, effectiveSymbol, msbuildProperties);
            foreach (var entry in entries)
            {
                var key = FormatTypeKey(entry.DepType);
                builder[key] = entry.PrimarySource;
            }
        }
        catch
        {
            // Resolver must never throw from the CLI path; attribution is advisory.
        }

        return builder.ToImmutable();
    }

    private static Compilation StripGeneratedTrees(Compilation compilation)
    {
        var generatedTrees = compilation.SyntaxTrees
            .Where(t => !string.IsNullOrEmpty(t.FilePath) &&
                        (t.FilePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
                         t.FilePath.Contains("/generated/", StringComparison.OrdinalIgnoreCase) ||
                         t.FilePath.Contains("\\generated\\", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (generatedTrees.Length == 0) return compilation;
        return compilation.RemoveSyntaxTrees(generatedTrees);
    }

    public static string FormatTypeKey(ITypeSymbol symbol)
    {
        var formatted = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return formatted.Replace("global::", string.Empty, StringComparison.Ordinal);
    }
}
