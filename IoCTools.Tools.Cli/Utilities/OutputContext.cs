namespace IoCTools.Tools.Cli;

using System.Diagnostics;
using System.Text.Json;

/// <summary>
/// Cross-cutting output routing for --json and --verbose flags.
/// When --json is active, human-readable output is suppressed and JSON is emitted at the end.
/// When --verbose is active, debug messages go to stderr.
/// </summary>
internal sealed class OutputContext
{
    public bool IsJson { get; }
    public bool IsVerbose { get; }
    private readonly Stopwatch _stopwatch;

    private OutputContext(bool isJson, bool isVerbose)
    {
        IsJson = isJson;
        IsVerbose = isVerbose;
        _stopwatch = Stopwatch.StartNew();
    }

    public static OutputContext Create(bool isJson, bool isVerbose)
    {
        var ctx = new OutputContext(isJson, isVerbose);
        if (isVerbose)
            ctx.Verbose($"Command started at {DateTime.UtcNow:O}");
        return ctx;
    }

    /// <summary>Write human-readable output. Suppressed when --json is active.</summary>
    public void WriteLine(string text)
    {
        if (!IsJson)
            Console.WriteLine(text);
    }

    /// <summary>Write human-readable output without newline. Suppressed when --json is active.</summary>
    public void Write(string text)
    {
        if (!IsJson)
            Console.Write(text);
    }

    /// <summary>Write verbose debug info to stderr. Only when --verbose is active.</summary>
    public void Verbose(string message)
    {
        if (IsVerbose)
            Console.Error.WriteLine($"[verbose] {_stopwatch.Elapsed.TotalMilliseconds:F0}ms: {message}");
    }

    /// <summary>Write JSON payload to stdout. Only when --json is active.</summary>
    public void WriteJson<T>(T payload)
    {
        if (IsJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(payload,
                new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    /// <summary>Write warning/error to stderr (always visible regardless of --json).</summary>
    public void WriteError(string message)
    {
        Console.Error.WriteLine(message);
    }

    /// <summary>Report elapsed time in verbose mode.</summary>
    public void ReportTiming(string label)
    {
        if (IsVerbose)
            Console.Error.WriteLine($"[verbose] {label}: {_stopwatch.Elapsed.TotalMilliseconds:F0}ms");
    }
}
