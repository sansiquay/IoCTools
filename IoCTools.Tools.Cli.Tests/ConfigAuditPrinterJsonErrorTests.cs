namespace IoCTools.Tools.Cli.Tests;

using System.Text;
using System.Text.Json;

using FluentAssertions;

using Xunit;

/// <summary>
/// Tests for issue #30: ConfigAuditPrinter JSON mode must surface settings read/parse errors
/// in the JSON payload (<c>settingsReadError</c> field) instead of silently ignoring them.
///
/// Revert-RED contract:
///   Restoring <c>catch { // Silently ignore ... }</c> in the JSON branch causes:
///   • <see cref="JsonMode_WithInvalidJsonSettings_IncludesErrorInPayload"/> — no non-null
///     settingsReadError → RED.
///   • <see cref="JsonMode_WithInvalidJsonSettings_WritesErrorToStderr"/> — no stderr output → RED.
///   • <see cref="JsonMode_WithValidSettings_HasNullSettingsReadError"/> — settingsReadError field
///     absent (field not present in pre-fix payload shape) → RED.
///
/// All tests drive the REAL <see cref="ConfigAuditPrinter.Write"/> — not a reimplementation.
/// </summary>
[Collection("CLI Execution")]
public sealed class ConfigAuditPrinterJsonErrorTests
{
    /// <summary>
    /// Produces one configuration field report so ConfigAuditPrinter populates <c>keys</c>
    /// and reaches the settings-read path in both JSON and text modes.
    /// Without at least one key, text mode returns early with "No configuration bindings found"
    /// before touching the settings file.
    /// </summary>
    private static IReadOnlyList<ServiceFieldReport> OneConfigReport()
    {
        var field = new GeneratedFieldInfo(
            FieldName: "_testSection",
            TypeName: "TestSectionSettings",
            Kind: GeneratedFieldKind.Configuration,
            Source: "test",
            ConfigurationKey: "TestSection",
            DefaultValue: null,
            Required: null,
            SupportsReloading: null,
            IsExternal: false);

        return new[]
        {
            new ServiceFieldReport(
                TypeName: "TestService",
                FilePath: "TestService.cs",
                DependencyFields: Array.Empty<GeneratedFieldInfo>(),
                ConfigurationFields: new[] { field })
        };
    }

    private static string CaptureJsonOutput(Action action)
    {
        var origOut = Console.Out;
        var sb = new StringBuilder();
        Console.SetOut(new StringWriter(sb));
        try
        {
            action();
            return sb.ToString();
        }
        finally
        {
            Console.SetOut(origOut);
        }
    }

    private static string CaptureStderr(Action action)
    {
        var origErr = Console.Error;
        var sb = new StringBuilder();
        Console.SetError(new StringWriter(sb));
        try
        {
            action();
            return sb.ToString();
        }
        finally
        {
            Console.SetError(origErr);
        }
    }

    [Fact]
    public void JsonMode_WithValidSettings_HasNullSettingsReadError()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ioc-test-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(tempFile, """{ "TestSection": "value" }""");

            var output = OutputContext.Create(isJson: true, isVerbose: false);
            var stdout = CaptureJsonOutput(() =>
                ConfigAuditPrinter.Write(OneConfigReport(), tempFile, output));

            var json = JsonDocument.Parse(stdout);
            json.RootElement.TryGetProperty("settingsReadError", out var errorProp).Should().BeTrue(
                "settingsReadError field must always be present in the JSON payload");
            errorProp.ValueKind.Should().Be(JsonValueKind.Null,
                "settingsReadError must be null when settings file is readable");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void JsonMode_WithInvalidJsonSettings_IncludesErrorInPayload()
    {
        // Arrange: invalid JSON → JsonDocument.Parse throws inside the try block
        var tempFile = Path.Combine(Path.GetTempPath(), $"ioc-test-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(tempFile, "{ this is not valid json }");

            var output = OutputContext.Create(isJson: true, isVerbose: false);
            var stdout = CaptureJsonOutput(() =>
                ConfigAuditPrinter.Write(OneConfigReport(), tempFile, output));

            // Assert: settingsReadError must be a non-null string.
            // Reverting the fix (restoring silent catch) → settingsReadError absent/null → RED.
            var json = JsonDocument.Parse(stdout);
            json.RootElement.TryGetProperty("settingsReadError", out var errorProp).Should().BeTrue(
                "JSON payload must include settingsReadError when settings cannot be parsed");
            errorProp.ValueKind.Should().Be(JsonValueKind.String,
                "settingsReadError must be a non-null string when parse fails");
            errorProp.GetString().Should().NotBeNullOrEmpty(
                "settingsReadError must contain the exception message");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void JsonMode_WithInvalidJsonSettings_WritesErrorToStderr()
    {
        // Mirrors text-mode WriteError: error always goes to stderr so it is visible in CI.
        // Reverting the fix (silent catch) → no stderr → RED.
        var tempFile = Path.Combine(Path.GetTempPath(), $"ioc-test-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(tempFile, "{ bad json }");

            var output = OutputContext.Create(isJson: true, isVerbose: false);
            // Capture both stdout (WriteJson) and stderr (WriteError) separately
            var origOut = Console.Out;
            var origErr = Console.Error;
            var sbErr = new StringBuilder();
            Console.SetOut(new StringWriter(new StringBuilder())); // suppress JSON stdout
            Console.SetError(new StringWriter(sbErr));
            try
            {
                ConfigAuditPrinter.Write(OneConfigReport(), tempFile, output);
            }
            finally
            {
                Console.SetOut(origOut);
                Console.SetError(origErr);
            }

            sbErr.ToString().Should().NotBeEmpty(
                "a settings parse error must be written to stderr via output.WriteError");
            sbErr.ToString().Should().Contain(tempFile,
                "the error message must include the settings file path");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void TextMode_WithInvalidJsonSettings_WritesError()
    {
        // Regression guard: text-mode WriteError behaviour must be preserved.
        var tempFile = Path.Combine(Path.GetTempPath(), $"ioc-test-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(tempFile, "{ not json }");

            var output = OutputContext.Create(isJson: false, isVerbose: false);
            var stderr = CaptureStderr(() =>
                ConfigAuditPrinter.Write(OneConfigReport(), tempFile, output));

            stderr.Should().Contain("Failed to read settings file",
                "text mode must report settings read errors on stderr");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
