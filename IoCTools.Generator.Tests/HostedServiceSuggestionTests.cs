namespace IoCTools.Generator.Tests;


public class HostedServiceSuggestionTests
{
    [Fact]
    public void HostedServiceWithMultipleInterfaces_SuggestsRegisterAsAll()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Hosting;

namespace Test;

public interface IOne { }
public interface ITwo { }

[Singleton]
public class Worker : BackgroundService, IOne, ITwo
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var suggestions = result.GetDiagnosticsByCode("IOC074");
        suggestions.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Info);
    }

    [Fact]
    public void HostedServiceWithRegisterAsAll_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Hosting;

namespace Test;

public interface IOne { }
public interface ITwo { }

[Singleton]
[RegisterAsAll]
public class Worker : BackgroundService, IOne, ITwo
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC074").Should().BeEmpty();
    }
}
