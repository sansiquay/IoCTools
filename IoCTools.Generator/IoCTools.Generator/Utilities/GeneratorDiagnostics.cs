namespace IoCTools.Generator.Utilities;

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
