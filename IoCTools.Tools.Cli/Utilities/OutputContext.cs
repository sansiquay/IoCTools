namespace IoCTools.Tools.Cli;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Cross-cutting output routing for --json and --verbose flags.
/// When --json is active, human-readable output is suppressed and JSON is emitted at the end.
/// When --verbose is active, debug messages go to stderr.
/// </summary>
internal sealed class OutputContext
{
    /// <summary>
    /// Schema version of the agent-receipt envelope for --json outputs.
    /// Bump when the shape of the receipt envelope (schema_version/generated_at) changes.
    /// Payload-specific shape changes do not require bumping this.
    /// </summary>
    public const string JsonSchemaVersion = "1.0";

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
    /// <remarks>
    /// Wraps the supplied payload with agent-receipt headers (<c>schema_version</c>,
    /// <c>generated_at</c>) at the top level. If <paramref name="payload"/> serializes to a
    /// JSON object, the headers are merged in-place; otherwise the payload is wrapped under
    /// a <c>data</c> field so the receipt envelope remains a JSON object.
    /// </remarks>
    public void WriteJson<T>(T payload)
    {
        if (!IsJson)
            return;

        Console.WriteLine(SerializeWithReceiptHeaders(payload));
    }

    /// <summary>
    /// Serialize a payload with the agent-receipt envelope. Visible for testing and for
    /// the legacy <c>--format json</c> path in <see cref="GraphPrinter"/>.
    /// </summary>
    internal static string SerializeWithReceiptHeaders<T>(T payload)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var rawJson = JsonSerializer.Serialize(payload, options);
        var node = JsonNode.Parse(rawJson);

        JsonObject envelope;
        if (node is JsonObject obj)
        {
            envelope = obj;
        }
        else
        {
            envelope = new JsonObject { ["data"] = node };
        }

        // Insert headers at the top of the envelope so receipts read schema_version first.
        var ordered = new JsonObject
        {
            ["schema_version"] = JsonSchemaVersion,
            ["generated_at"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        };
        foreach (var kvp in envelope.ToArray())
        {
            // Preserve insertion order from the original payload, skipping any pre-existing
            // header keys (defensive — callers should not be setting these).
            if (kvp.Key is "schema_version" or "generated_at")
                continue;
            envelope.Remove(kvp.Key);
            ordered[kvp.Key] = kvp.Value;
        }

        return ordered.ToJsonString(options);
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
