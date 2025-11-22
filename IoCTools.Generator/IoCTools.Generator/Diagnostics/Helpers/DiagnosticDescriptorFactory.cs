namespace IoCTools.Generator.Diagnostics.Helpers;

internal static class DiagnosticDescriptorFactory
{
    public static DiagnosticDescriptor WithSeverity(DiagnosticDescriptor baseDescriptor,
        DiagnosticSeverity severity)
        => new(
            baseDescriptor.Id,
            baseDescriptor.Title,
            baseDescriptor.MessageFormat,
            baseDescriptor.Category,
            severity,
            baseDescriptor.IsEnabledByDefault,
            baseDescriptor.Description,
            baseDescriptor.HelpLinkUri,
            baseDescriptor.CustomTags.ToArray());
}
