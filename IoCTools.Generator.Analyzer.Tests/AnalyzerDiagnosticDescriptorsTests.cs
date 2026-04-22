namespace IoCTools.Generator.Analyzer.Tests;

using FluentAssertions;
using IoCTools.Generator.Analyzer;
using Xunit;

/// <summary>
///     Guards against drift between the analyzer's duplicated IOC095 descriptor
///     and the generator's canonical descriptor. The two live in separate
///     assemblies (analyzer can't reference the generator's internal-static
///     descriptors directly) but must report identical metadata so consumers
///     see one IOC095 regardless of which source emitted it.
/// </summary>
public sealed class AnalyzerDiagnosticDescriptorsTests
{
    [Fact]
    public void InjectDeprecated_has_expected_id()
    {
        AnalyzerDiagnosticDescriptors.InjectDeprecated.Id.Should().Be("IOC095");
    }

    [Fact]
    public void InjectDeprecated_has_warning_severity()
    {
        AnalyzerDiagnosticDescriptors.InjectDeprecated.DefaultSeverity
            .Should().Be(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
    }

    [Fact]
    public void InjectDeprecated_has_usage_category()
    {
        AnalyzerDiagnosticDescriptors.InjectDeprecated.Category.Should().Be("IoCTools.Usage");
    }

    [Fact]
    public void InjectDeprecated_is_enabled_by_default()
    {
        AnalyzerDiagnosticDescriptors.InjectDeprecated.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void InjectDeprecated_title_matches_generator()
    {
        AnalyzerDiagnosticDescriptors.InjectDeprecated.Title.ToString()
            .Should().Be("[Inject] is deprecated; use [DependsOn<T>]");
    }

    [Fact]
    public void InjectDeprecated_message_format_matches_generator()
    {
        AnalyzerDiagnosticDescriptors.InjectDeprecated.MessageFormat.ToString()
            .Should().Be("[Inject] on field '{0}' is deprecated. Use [DependsOn<{1}>] on the class. A code fix is available.");
    }
}
