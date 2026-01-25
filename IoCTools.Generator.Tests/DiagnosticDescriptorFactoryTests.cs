using IoCTools.Generator.Diagnostics.Helpers;

using Microsoft.CodeAnalysis;

using Xunit;

namespace IoCTools.Generator.Tests;

public class DiagnosticDescriptorFactoryTests
{
    [Fact]
    public void WithSeverity_SameSeverityAsDefault_ReturnsBaseDescriptor()
    {
        var descriptor = new DiagnosticDescriptor(
            "Test001",
            "Test Title",
            "Test Message",
            "Test",
            DiagnosticSeverity.Error,
            true);

        var result = DiagnosticDescriptorFactory.WithSeverity(descriptor, DiagnosticSeverity.Error);

        // Fast path: should return the same instance
        Assert.Same(descriptor, result);
    }

    [Fact]
    public void WithSeverity_DifferentSeverity_ReturnsNewDescriptor()
    {
        var descriptor = new DiagnosticDescriptor(
            "Test001",
            "Test Title",
            "Test Message",
            "Test",
            DiagnosticSeverity.Error,
            true);

        var result = DiagnosticDescriptorFactory.WithSeverity(descriptor, DiagnosticSeverity.Warning);

        // Different severity: should return a new descriptor with updated severity
        Assert.NotSame(descriptor, result);
        Assert.Equal("Test001", result.Id);
        Assert.Equal(DiagnosticSeverity.Warning, result.DefaultSeverity);
    }

    [Fact]
    public void WithSeverity_CachedDescriptor_ReturnsSameReference()
    {
        var descriptor = new DiagnosticDescriptor(
            "Test002",
            "Test Title",
            "Test Message",
            "Test",
            DiagnosticSeverity.Error,
            true);

        var result1 = DiagnosticDescriptorFactory.WithSeverity(descriptor, DiagnosticSeverity.Warning);
        var result2 = DiagnosticDescriptorFactory.WithSeverity(descriptor, DiagnosticSeverity.Warning);

        // Both calls with same severity should return the same cached instance
        Assert.Same(result1, result2);
    }

    [Fact]
    public void WithSeverity_DifferentSeverities_ReturnsDifferentReferences()
    {
        var descriptor = new DiagnosticDescriptor(
            "Test003",
            "Test Title",
            "Test Message",
            "Test",
            DiagnosticSeverity.Error,
            true);

        var warningResult = DiagnosticDescriptorFactory.WithSeverity(descriptor, DiagnosticSeverity.Warning);
        var infoResult = DiagnosticDescriptorFactory.WithSeverity(descriptor, DiagnosticSeverity.Info);

        // Different severities should return different cached instances
        Assert.NotSame(warningResult, infoResult);
        Assert.Equal(DiagnosticSeverity.Warning, warningResult.DefaultSeverity);
        Assert.Equal(DiagnosticSeverity.Info, infoResult.DefaultSeverity);
    }

    [Fact]
    public void WithSeverity_MultipleDescriptors_CachesIndependently()
    {
        var descriptor1 = new DiagnosticDescriptor(
            "Test004",
            "Test Title 1",
            "Test Message 1",
            "Test",
            DiagnosticSeverity.Error,
            true);

        var descriptor2 = new DiagnosticDescriptor(
            "Test005",
            "Test Title 2",
            "Test Message 2",
            "Test",
            DiagnosticSeverity.Error,
            true);

        var result1 = DiagnosticDescriptorFactory.WithSeverity(descriptor1, DiagnosticSeverity.Warning);
        var result2 = DiagnosticDescriptorFactory.WithSeverity(descriptor2, DiagnosticSeverity.Warning);

        // Different descriptors should cache independently
        Assert.NotSame(result1, result2);
        Assert.Equal("Test004", result1.Id);
        Assert.Equal("Test005", result2.Id);
    }

    [Theory]
    [InlineData(DiagnosticSeverity.Error)]
    [InlineData(DiagnosticSeverity.Warning)]
    [InlineData(DiagnosticSeverity.Info)]
    [InlineData(DiagnosticSeverity.Hidden)]
    public void WithSeverity_AllSeverities_CachedCorrectly(DiagnosticSeverity severity)
    {
        var descriptor = new DiagnosticDescriptor(
            "Test006",
            "Test Title",
            "Test Message",
            "Test",
            DiagnosticSeverity.Error,
            true);

        var result1 = DiagnosticDescriptorFactory.WithSeverity(descriptor, severity);
        var result2 = DiagnosticDescriptorFactory.WithSeverity(descriptor, severity);

        // Same severity should return same cached reference
        Assert.Same(result1, result2);
        Assert.Equal(severity, result1.DefaultSeverity);
        Assert.Equal(severity, result2.DefaultSeverity);
    }

    [Fact]
    public void WithSeverity_ThreadSafe_Cache()
    {
        var descriptor = new DiagnosticDescriptor(
            "Test007",
            "Test Title",
            "Test Message",
            "Test",
            DiagnosticSeverity.Error,
            true);

        // Simulate concurrent access from multiple threads
        var results = new System.Collections.Generic.List<DiagnosticDescriptor>();
        var lockObj = new object();

        Parallel.For(0, 100, _ =>
        {
            for (var i = 0; i < 10; i++)
            {
                var result = DiagnosticDescriptorFactory.WithSeverity(descriptor, DiagnosticSeverity.Warning);
                lock (lockObj)
                {
                    results.Add(result);
                }
            }
        });

        // All results should be the same cached instance
        var distinctResults = results.Distinct().ToList();
        Assert.Single(distinctResults);
    }
}
