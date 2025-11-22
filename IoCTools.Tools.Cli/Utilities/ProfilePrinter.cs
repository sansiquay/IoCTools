namespace IoCTools.Tools.Cli;

internal static class ProfilePrinter
{
    public static void Write(TimeSpan elapsed,
        string projectPath,
        string? type)
    {
        Console.WriteLine($"Project: {projectPath}");
        if (!string.IsNullOrWhiteSpace(type))
            Console.WriteLine($"Type filter: {type}");
        Console.WriteLine($"Generator warm + analysis time: {elapsed.TotalMilliseconds:F0} ms");
    }
}
