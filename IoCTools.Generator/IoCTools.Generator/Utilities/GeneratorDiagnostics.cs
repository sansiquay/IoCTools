namespace IoCTools.Generator.Utilities;

/// <summary>
/// Delegate for reporting diagnostics, enabling validators to be tested independently
/// of SourceProductionContext. Pass context.ReportDiagnostic when calling from the pipeline.
/// </summary>
internal delegate void ReportDiagnosticDelegate(Diagnostic diagnostic);

internal static class GeneratorDiagnostics
{
    public static void Report(SourceProductionContext context,
        string id,
        string title,
        string message)
    {
        var descriptor = new DiagnosticDescriptor(id, title, "IoCTools Generator: {0}", "IoCTools",
            DiagnosticSeverity.Warning, true);
        var diagnostic = Diagnostic.Create(descriptor, Location.None, message);
        context.ReportDiagnostic(diagnostic);
    }
}
