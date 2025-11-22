namespace IoCTools.Tools.Cli;

internal static class CompareRunner
{
    public static void WriteSnapshot(GeneratorArtifactWriter artifacts,
        string outputDir)
    {
        Console.WriteLine($"Snapshot written to: {artifacts.OutputRoot}");
        if (!string.Equals(artifacts.OutputRoot, outputDir, StringComparison.OrdinalIgnoreCase))
        {
            // already in desired directory via GeneratorArtifactWriter; nothing else required
        }
    }

    public static void Compare(string baselineDir,
        string newDir)
    {
        Console.WriteLine($"Comparing baseline '{baselineDir}' to '{newDir}'");
        var baselineFiles = Directory.Exists(baselineDir)
            ? Directory.GetFiles(baselineDir, "*.g.cs", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();
        var newFiles = Directory.Exists(newDir)
            ? Directory.GetFiles(newDir, "*.g.cs", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();

        var baselineMap = baselineFiles
            .Where(f => Path.GetFileName(f) != null)
            .ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText, StringComparer.OrdinalIgnoreCase);
        var newMap = newFiles
            .Where(f => Path.GetFileName(f) != null)
            .ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText, StringComparer.OrdinalIgnoreCase);

        var allKeys = new HashSet<string>(baselineMap.Keys, StringComparer.OrdinalIgnoreCase);
        allKeys.UnionWith(newMap.Keys);

        foreach (var key in allKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            baselineMap.TryGetValue(key, out var oldText);
            newMap.TryGetValue(key, out var newText);
            if (oldText == newText)
                continue;

            Console.WriteLine($"- {key}: changed");
        }

        if (!allKeys.Any()) Console.WriteLine("No generated artifacts found to compare.");
    }
}
