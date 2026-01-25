namespace IoCTools.Generator.Tests;


public class HostedServiceLifetimeTests
{
    [Fact]
    public void BackgroundService_NoLifetime_NoDiagnostic()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;

namespace Test;

public class Worker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC072").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC070").Should().BeEmpty();
    }

    [Fact]
    public void HostedService_WithLifetime_WarnsIOC072()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;

namespace Test;

[Singleton]
public class Worker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC072").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void HostedService_WithAdditionalInterface_RequiresLifetime()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IWorkerTelemetry {}

public class Worker : BackgroundService, IWorkerTelemetry
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC070").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
        result.GetDiagnosticsByCode("IOC072").Should().BeEmpty();
    }
}

