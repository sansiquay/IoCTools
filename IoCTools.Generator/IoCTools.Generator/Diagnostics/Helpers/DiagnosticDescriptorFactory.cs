namespace IoCTools.Generator.Diagnostics.Helpers;

using System.Collections.Concurrent;

internal static class DiagnosticDescriptorFactory
{
    private static readonly ConcurrentDictionary<(string id, DiagnosticSeverity severity), DiagnosticDescriptor> Cache = new();

    public static DiagnosticDescriptor WithSeverity(DiagnosticDescriptor baseDescriptor,
        DiagnosticSeverity severity)
    {
        // Fast path: same severity means use the base descriptor directly
        if (baseDescriptor.DefaultSeverity == severity)
            return baseDescriptor;

        // Cache path: return cached descriptor or create and cache new one
        return Cache.GetOrAdd((baseDescriptor.Id, severity), _ => new DiagnosticDescriptor(
            baseDescriptor.Id,
            baseDescriptor.Title,
            baseDescriptor.MessageFormat,
            baseDescriptor.Category,
            severity,
            baseDescriptor.IsEnabledByDefault,
            baseDescriptor.Description,
            baseDescriptor.HelpLinkUri,
            baseDescriptor.CustomTags.ToArray()));
    }
}
