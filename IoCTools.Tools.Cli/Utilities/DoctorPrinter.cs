namespace IoCTools.Tools.Cli;

using System.Text.Json;

internal static class DoctorPrinter
{
    public static void Write(IReadOnlyList<DiagnosticSummary> diagnostics,
        bool fixableOnly,
        OutputContext output) =>
        Write(diagnostics, fixableOnly, output, System.Array.Empty<DoctorPreflight.PreflightFinding>());

    public static void Write(IReadOnlyList<DiagnosticSummary> diagnostics,
        bool fixableOnly,
        OutputContext output,
        IReadOnlyList<DoctorPreflight.PreflightFinding> preflightFindings)
    {
        var filtered = fixableOnly
            ? diagnostics.Where(d => d.Severity == "Info" || d.Severity == "Warning").ToList()
            : diagnostics.ToList();

        if (output.IsJson)
        {
            var payload = new
            {
                preflight = preflightFindings.Select(f => new { category = f.Category, message = f.Message }),
                diagnostics = filtered.Select(d => new
                {
                    id = d.Id,
                    severity = d.Severity,
                    message = d.Message,
                    location = $"{d.FilePath}:{d.Line}:{d.Column}"
                })
            };
            output.WriteJson(payload);
            return;
        }

        if (preflightFindings.Count > 0)
        {
            output.WriteLine("Preflight:");
            foreach (var finding in preflightFindings)
                output.WriteLine($"  [{finding.Category}] {finding.Message}");
            output.WriteLine(string.Empty);
        }

        if (filtered.Count == 0 && preflightFindings.Count == 0)
        {
            output.WriteLine("No diagnostics found.");
            return;
        }

        if (filtered.Count == 0)
        {
            output.WriteLine($"Total: 0 diagnostics ({diagnostics.Count} before filters), {preflightFindings.Count} preflight finding(s)");
            return;
        }

        foreach (var diag in filtered.OrderBy(d => d.Severity).ThenBy(d => d.Id))
        {
            output.WriteLine($"[{AnsiColor.Severity(diag.Severity)}] {diag.Id}: {diag.Message}");
            output.WriteLine($"  at {diag.FilePath}:{diag.Line}:{diag.Column}");
        }

        output.WriteLine(string.Empty);
        output.WriteLine($"Total: {filtered.Count} diagnostics ({diagnostics.Count} before filters), {preflightFindings.Count} preflight finding(s)");
    }
}
