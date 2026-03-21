namespace IoCTools.Tools.Cli;

/// <summary>
/// ANSI escape sequence color utility for terminal output.
/// Colors are automatically disabled when output is redirected or NO_COLOR is set.
/// </summary>
internal static class AnsiColor
{
    private static readonly bool Enabled = !Console.IsOutputRedirected
        && Environment.GetEnvironmentVariable("NO_COLOR") == null;

    public static string Red(string text) => Enabled ? $"\x1b[31m{text}\x1b[0m" : text;
    public static string Yellow(string text) => Enabled ? $"\x1b[33m{text}\x1b[0m" : text;
    public static string Cyan(string text) => Enabled ? $"\x1b[36m{text}\x1b[0m" : text;
    public static string Green(string text) => Enabled ? $"\x1b[32m{text}\x1b[0m" : text;
    public static string Blue(string text) => Enabled ? $"\x1b[34m{text}\x1b[0m" : text;
    public static string Gray(string text) => Enabled ? $"\x1b[90m{text}\x1b[0m" : text;
    public static string Bold(string text) => Enabled ? $"\x1b[1m{text}\x1b[0m" : text;

    /// <summary>Colors a severity label per D-09: red Error, yellow Warning, cyan Info.</summary>
    public static string Severity(string severity) => severity switch
    {
        "Error" => Red(severity),
        "Warning" => Yellow(severity),
        "Info" => Cyan(severity),
        _ => severity
    };

    /// <summary>Colors a lifetime label per D-09: green Singleton, blue Scoped, gray Transient.</summary>
    public static string Lifetime(string lifetime) => lifetime switch
    {
        "Singleton" => Green(lifetime),
        "Scoped" => Blue(lifetime),
        "Transient" => Gray(lifetime),
        _ => lifetime
    };
}
