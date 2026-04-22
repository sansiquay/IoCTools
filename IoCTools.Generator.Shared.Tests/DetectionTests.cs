namespace IoCTools.Generator.Shared.Tests;

using System.Linq;
using FluentAssertions;
using IoCTools.Generator.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

public sealed class DetectionTests
{
    private static Compilation CreateCompilation(string source, bool includeLogging)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
        };
        if (includeLogging)
        {
            references = references.Append(
                MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Logging.ILogger<>).Assembly.Location))
                .ToArray();
        }
        return CSharpCompilation.Create("TestAsm", new[] { syntaxTree }, references);
    }

    [Fact]
    public void ILogger_open_generic_detected_when_MEL_referenced()
    {
        var compilation = CreateCompilation("namespace X { class Y { } }", includeLogging: true);
        AutoDepsResolver.IsBuiltinILoggerAvailable(compilation).Should().BeTrue();
    }

    [Fact]
    public void ILogger_not_detected_when_MEL_missing()
    {
        var compilation = CreateCompilation("namespace X { class Y { } }", includeLogging: false);
        AutoDepsResolver.IsBuiltinILoggerAvailable(compilation).Should().BeFalse();
    }

    [Fact]
    public void User_defined_ILogger_does_not_false_positive()
    {
        var compilation = CreateCompilation(
            "namespace NotMicrosoft { public interface ILogger<T> { } }", includeLogging: false);
        AutoDepsResolver.IsBuiltinILoggerAvailable(compilation).Should().BeFalse();
    }
}
