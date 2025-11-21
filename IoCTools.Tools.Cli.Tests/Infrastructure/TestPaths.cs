namespace IoCTools.Tools.Cli.Tests.Infrastructure;

internal static class TestPaths
{
    public static string RepoRoot { get; } = LocateRepoRoot();

    public static string ResolveRepoPath(params string[] segments)
    {
        var path = RepoRoot;
        foreach (var segment in segments)
            path = Path.Combine(path, segment);
        return path;
    }

    public static string CreateTempDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "IoCToolsCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static string LocateRepoRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "IoCTools.sln")))
                return directory;
            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("Unable to locate repository root (IoCTools.sln not found).");
    }
}
