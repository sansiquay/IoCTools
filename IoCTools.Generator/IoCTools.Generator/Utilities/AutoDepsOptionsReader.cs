namespace IoCTools.Generator.Utilities;

using System.Collections.Immutable;

using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Extracts the subset of MSBuild properties relevant to the auto-deps resolver into an
/// equatable immutable dictionary so it can participate in the incremental pipeline without
/// invalidating the whole generator on unrelated property changes.
/// </summary>
internal static class AutoDepsOptionsReader
{
    private static readonly string[] RelevantKeys =
    {
        "build_property.IoCToolsAutoDepsDisable",
        "build_property.IoCToolsAutoDepsExcludeGlob",
        "build_property.IoCToolsAutoDepsReport",
        "build_property.IoCToolsAutoDetectLogger",
        "build_property.IoCToolsInjectDeprecationSeverity"
    };

    public static ImmutableDictionary<string, string> Read(AnalyzerConfigOptions options)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();
        foreach (var key in RelevantKeys)
        {
            if (options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                builder[key] = value;
        }

        return builder.ToImmutable();
    }
}
