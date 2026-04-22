namespace IoCTools.Generator.Analyzer;

using Microsoft.CodeAnalysis;

/// <summary>
///     Minimal duplicate of the generator's <c>DiagnosticDescriptors.InjectDeprecated</c>
///     descriptor so the analyzer assembly can both report IOC095 and have its code-fix
///     provider bind to it. Roslyn compares descriptors by string ID, so a parallel
///     descriptor here reports at the same ID as the generator's emission and the IDE
///     lightbulb binds correctly to the analyzer-emitted diagnostic.
/// </summary>
/// <remarks>
///     Any drift from <c>IoCTools.Generator.Diagnostics.DiagnosticDescriptors.InjectDeprecated</c>
///     is a bug. The descriptor equivalence is guarded by a regression test in
///     <c>IoCTools.Generator.Analyzer.Tests</c>.
/// </remarks>
internal static class AnalyzerDiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InjectDeprecated = new(
        "IOC095",
        "[Inject] is deprecated; use [DependsOn<T>]",
        "[Inject] on field '{0}' is deprecated. Use [DependsOn<{1}>] on the class. A code fix is available.",
        "IoCTools.Usage",
        DiagnosticSeverity.Warning,
        true,
        "Migrate to [DependsOn<T>] on the class. See migration guide for full deprecation timeline (1.6 warning → 1.7 error → 2.0 removed).",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/migration.md#migrating-from-15x-to-16x");
}
