namespace IoCTools.Tools.Cli;

internal static class DoctorPrinter
{
    public static void Write(IReadOnlyList<DiagnosticSummary> diagnostics,
        bool fixableOnly)
    {
        var filtered = fixableOnly
            ? diagnostics.Where(d => d.Severity == "Info" || d.Severity == "Warning").ToList()
            : diagnostics.ToList();

        if (filtered.Count == 0)
        {
            Console.WriteLine("No diagnostics found.");
            return;
        }

        foreach (var diag in filtered.OrderBy(d => d.Severity).ThenBy(d => d.Id))
        {
            Console.WriteLine($"[{diag.Severity}] {diag.Id}: {diag.Message}");
            Console.WriteLine($"  at {diag.FilePath}:{diag.Line}:{diag.Column}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {filtered.Count} diagnostics ({diagnostics.Count} before filters)");
    }
}
