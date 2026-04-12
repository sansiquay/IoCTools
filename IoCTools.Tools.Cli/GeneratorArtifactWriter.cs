namespace IoCTools.Tools.Cli;

using System.Globalization;

using Generator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal sealed class GeneratorArtifactWriter
{
    private readonly Dictionary<string, string> _hintToPath;

    private GeneratorArtifactWriter(string outputRoot,
        Dictionary<string, string> hintToPath)
    {
        OutputRoot = outputRoot;
        _hintToPath = hintToPath ?? throw new ArgumentNullException(nameof(hintToPath));
    }

    public string OutputRoot { get; }

    public bool TryGetFile(string hintName,
        out string? path) =>
        _hintToPath.TryGetValue(hintName, out path);

    public IReadOnlyList<GeneratedArtifact> GetArtifacts() =>
        _hintToPath
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new GeneratedArtifact(pair.Key, pair.Value))
            .ToArray();

    public static async Task<GeneratorArtifactWriter> CreateAsync(ProjectContext context,
        string? requestedOutputDirectory,
        CancellationToken cancellationToken)
    {
        var outputRoot = requestedOutputDirectory != null
            ? Path.GetFullPath(requestedOutputDirectory)
            : BuildDefaultOutputDirectory(context.Project);
        Directory.CreateDirectory(outputRoot);

        var stubDirectory = Environment.GetEnvironmentVariable("IOC_TOOLS_GENERATOR_STUB");
        if (!string.IsNullOrWhiteSpace(stubDirectory) && Directory.Exists(stubDirectory))
        {
            var stubFiles = Directory.EnumerateFiles(stubDirectory, "*.g.cs", SearchOption.TopDirectoryOnly)
                .ToList();
            if (stubFiles.Count > 0)
            {
                var stubMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var stub in stubFiles)
                {
                    var destination = Path.Combine(outputRoot, Path.GetFileName(stub));
                    File.Copy(stub, destination, true);
                    stubMap[Path.GetFileName(stub)] = destination;
                }

                return new GeneratorArtifactWriter(outputRoot, stubMap);
            }
        }

        var generator = new DependencyInjectionGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());
        driver = driver.RunGenerators(context.Compilation);
        var runResult = driver.GetRunResult();

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in runResult.Results)
        foreach (var source in result.GeneratedSources)
        {
            var path = Path.Combine(outputRoot, source.HintName);
            await File.WriteAllTextAsync(path, source.SourceText.ToString(), cancellationToken);
            map[source.HintName] = path;
        }

        return new GeneratorArtifactWriter(outputRoot, map);
    }

    private static string BuildDefaultOutputDirectory(Project project)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var tempRoot = Path.Combine(Path.GetTempPath(), "IoCTools.Tools.Cli");
        var projectSegment = SanitizePathSegment(project.Name?.Trim(), "project");
        return Path.Combine(tempRoot, projectSegment, timestamp);
    }

    private static string SanitizePathSegment(string? value,
        string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value!;
        foreach (var invalid in Path.GetInvalidFileNameChars())
            candidate = candidate.Replace(invalid, '_');
        return candidate;
    }
}

internal sealed record GeneratedArtifact(string ArtifactId, string Path);
