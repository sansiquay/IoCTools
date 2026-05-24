namespace IoCTools.Tools.Cli.Tests;

using System;
using System.Globalization;
using System.Text.Json;

using FluentAssertions;

using Infrastructure;

using Xunit;

/// <summary>
/// Verifies the agent-receipt headers (<c>schema_version</c>, <c>generated_at</c>) are
/// emitted at the top level of every <c>--json</c> output across the CLI surfaces called
/// out in the contract: <c>evidence</c>, <c>validator-graph</c>,
/// <c>validator-graph --why</c>, and <c>suppress</c>.
/// </summary>
[Collection("CLI Execution")]
public sealed class JsonReceiptHeadersTests
{
    private static string RegistrationProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "RegistrationProject",
            "RegistrationProject.csproj");

    private static string AutoDepsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "AutoDepsProject",
            "AutoDepsProject.csproj");

    #region Helper

    private static (string schemaVersion, string generatedAt, JsonElement root) ParseReceipt(string stdout)
    {
        // The receipt envelope is a single JSON document spanning the entire stdout.
        var trimmed = stdout.Trim();
        using var doc = JsonDocument.Parse(trimmed);
        var root = doc.RootElement.Clone();
        root.ValueKind.Should().Be(JsonValueKind.Object,
            "receipt envelope must be a JSON object so headers can sit at the top level");

        root.TryGetProperty("schema_version", out var schemaProp)
            .Should().BeTrue("schema_version header is required on every --json receipt");
        root.TryGetProperty("generated_at", out var genProp)
            .Should().BeTrue("generated_at header is required on every --json receipt");

        var schemaVersion = schemaProp.GetString()!;
        var generatedAt = genProp.GetString()!;

        schemaVersion.Should().Be(OutputContext.JsonSchemaVersion,
            "schema_version must match the constant on OutputContext so callers can pin");

        // Parse generated_at as ISO8601 UTC (yyyy-MM-ddTHH:mm:ssZ).
        DateTimeOffset.TryParseExact(
            generatedAt,
            "yyyy-MM-ddTHH:mm:ssZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed).Should().BeTrue(
            $"generated_at must be ISO8601 UTC (yyyy-MM-ddTHH:mm:ssZ); was '{generatedAt}'");
        parsed.Offset.Should().Be(TimeSpan.Zero, "generated_at must be UTC");

        return (schemaVersion, generatedAt, root);
    }

    #endregion

    #region Unit — helper directly

    [Fact]
    public void SerializeWithReceiptHeaders_object_payload_merges_headers_at_top_level()
    {
        var json = OutputContext.SerializeWithReceiptHeaders(new { foo = 1, bar = "baz" });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("schema_version").GetString().Should().Be(OutputContext.JsonSchemaVersion);
        root.GetProperty("generated_at").GetString().Should().MatchRegex(
            @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$");
        root.GetProperty("foo").GetInt32().Should().Be(1);
        root.GetProperty("bar").GetString().Should().Be("baz");
    }

    [Fact]
    public void SerializeWithReceiptHeaders_array_payload_wraps_under_data()
    {
        var json = OutputContext.SerializeWithReceiptHeaders(new[] { 1, 2, 3 });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("schema_version").GetString().Should().Be(OutputContext.JsonSchemaVersion);
        root.GetProperty("generated_at").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("data").GetArrayLength().Should().Be(3);
    }

    #endregion

    #region Surface — evidence

    [Fact]
    public async Task Evidence_json_emits_receipt_headers()
    {
        var result = await CliTestHost.RunAsync(
            "evidence",
            "--project", RegistrationProjectPath,
            "--json");

        result.ExitCode.Should().Be(0);
        var (_, _, root) = ParseReceipt(result.Stdout);
        // Existing payload shape is preserved.
        root.TryGetProperty("project", out _).Should().BeTrue();
        root.TryGetProperty("services", out _).Should().BeTrue();
    }

    #endregion

    #region Surface — validator-graph

    [Fact]
    public async Task ValidatorGraph_json_emits_receipt_headers()
    {
        var result = await CliTestHost.RunAsync(
            "validator-graph",
            "--project", RegistrationProjectPath,
            "--json");

        result.ExitCode.Should().Be(0);
        // The graph payload is an array; the receipt envelope must still expose
        // schema_version + generated_at and place the array under `data`.
        var (_, _, root) = ParseReceipt(result.Stdout);
        root.TryGetProperty("data", out var data).Should().BeTrue(
            "array payloads are wrapped under `data` so the receipt envelope stays a JSON object");
        data.ValueKind.Should().Be(JsonValueKind.Array);
    }

    #endregion

    #region Surface — validator-graph --why

    [Fact]
    public async Task ValidatorGraphWhy_json_emits_receipt_headers()
    {
        // --why expects a validator name. We pass a name that may or may not exist in the
        // project; either way the JSON receipt must carry the headers.
        var result = await CliTestHost.RunAsync(
            "validator-graph",
            "--project", RegistrationProjectPath,
            "--why", "NoSuchValidator",
            "--json");

        result.ExitCode.Should().Be(0);
        ParseReceipt(result.Stdout);
    }

    #endregion

    #region Surface — suppress

    [Fact]
    public async Task Suppress_json_emits_receipt_headers()
    {
        var result = await CliTestHost.RunAsync(
            "suppress",
            "--project", AutoDepsProjectPath,
            "--json");

        result.ExitCode.Should().Be(0);
        var (_, _, root) = ParseReceipt(result.Stdout);
        // Existing payload shape is preserved.
        root.TryGetProperty("rules", out _).Should().BeTrue();
        root.TryGetProperty("editorconfig", out _).Should().BeTrue();
    }

    #endregion
}
