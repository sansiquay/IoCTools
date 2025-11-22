namespace IoCTools.Tools.Cli;

using Generator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class DiagnosticRunner
{
    public static async Task<IReadOnlyList<DiagnosticSummary>> RunAsync(ProjectContext context,
        CancellationToken token)
    {
        var generator = new DependencyInjectionGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());
        driver = driver.RunGenerators(context.Compilation, token);
        var runResult = driver.GetRunResult();

        var results = new List<DiagnosticSummary>();
        foreach (var diag in runResult.Diagnostics)
        {
            var location = diag.Location.GetLineSpan();
            results.Add(new DiagnosticSummary(
                diag.Id,
                diag.Severity.ToString(),
                diag.GetMessage(),
                location.Path,
                location.StartLinePosition.Line + 1,
                location.StartLinePosition.Character + 1));
        }

        return results;
    }
}

internal sealed record DiagnosticSummary(
    string Id,
    string Severity,
    string Message,
    string FilePath,
    int Line,
    int Column);
