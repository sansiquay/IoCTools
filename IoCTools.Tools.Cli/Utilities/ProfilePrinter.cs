namespace IoCTools.Tools.Cli;

using System.Text.Json;

internal static class ProfilePrinter
{
    public static void Write(TimeSpan elapsed,
        string projectPath,
        string? type,
        OutputContext output)
    {
        if (output.IsJson)
        {
            var payload = new
            {
                projectPath,
                typeFilter = type,
                elapsedMilliseconds = elapsed.TotalMilliseconds
            };
            output.WriteJson(payload);
            return;
        }

        output.WriteLine($"Project: {projectPath}");
        if (!string.IsNullOrWhiteSpace(type))
            output.WriteLine($"Type filter: {type}");
        output.WriteLine($"Generator warm + analysis time: {elapsed.TotalMilliseconds:F0} ms");
    }
}
