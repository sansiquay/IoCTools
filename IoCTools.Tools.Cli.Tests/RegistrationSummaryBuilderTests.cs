namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Xunit;

public sealed class RegistrationSummaryBuilderTests
{
    [Fact]
    public void FilterByType_WithSimpleName_MatchesExactType()
    {
        // Arrange
        var source = @"using Microsoft.Extensions.DependencyInjection;
public static class Extensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<AnalyticsProcessor>();
        services.AddScoped<NotificationDispatcher>();
        return services;
    }
}";
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, source);
        var summary = RegistrationSummaryBuilder.Build(tempFile);

        // Act
        var filtered = RegistrationSummaryBuilder.FilterByType(summary, "AnalyticsProcessor");

        // Assert
        filtered.Records.Should().HaveCount(1);
        filtered.Records[0].ServiceType.Should().Be("AnalyticsProcessor");
    }

    [Fact]
    public void FilterByType_WithSimpleName_MatchesQualifiedName()
    {
        // Arrange
        var source = @"using Microsoft.Extensions.DependencyInjection;
namespace MyNamespace.Services;
public static class Extensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<MyNamespace.Services.AnalyticsProcessor>();
        services.AddScoped<NotificationDispatcher>();
        return services;
    }
}";
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, source);
        var summary = RegistrationSummaryBuilder.Build(tempFile);

        // Act
        var filtered = RegistrationSummaryBuilder.FilterByType(summary, "AnalyticsProcessor");

        // Assert
        filtered.Records.Should().HaveCount(1);
        filtered.Records[0].ServiceType.Should().Be("MyNamespace.Services.AnalyticsProcessor");
    }

    [Fact]
    public void FilterByType_DoesNotMatchTypeInCommentsOrStrings()
    {
        // Arrange
        var source = @"using Microsoft.Extensions.DependencyInjection;
public static class Extensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        // TODO: Add AnalyticsProcessor later
        var typeName = ""AnalyticsProcessor""; // String literal, not a registration
        services.AddSingleton<NotificationDispatcher>();
        return services;
    }
}";
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, source);
        var summary = RegistrationSummaryBuilder.Build(tempFile);

        // Act
        var filtered = RegistrationSummaryBuilder.FilterByType(summary, "AnalyticsProcessor");

        // Assert
        filtered.Records.Should().BeEmpty();
    }

    [Fact]
    public void FilterByType_MatchesImplementationType()
    {
        // Arrange
        var source = @"using Microsoft.Extensions.DependencyInjection;
public static class Extensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IAnalyticsProcessor, AnalyticsProcessor>();
        services.AddScoped<NotificationDispatcher>();
        return services;
    }
}";
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, source);
        var summary = RegistrationSummaryBuilder.Build(tempFile);

        // Act
        var filtered = RegistrationSummaryBuilder.FilterByType(summary, "AnalyticsProcessor");

        // Assert
        filtered.Records.Should().HaveCount(1);
        filtered.Records[0].ServiceType.Should().Be("IAnalyticsProcessor");
        filtered.Records[0].ImplementationType.Should().Be("AnalyticsProcessor");
    }

    [Fact]
    public void FilterByType_WithNonMatchingType_ReturnsEmpty()
    {
        // Arrange
        var source = @"using Microsoft.Extensions.DependencyInjection;
public static class Extensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<AnalyticsProcessor>();
        services.AddScoped<NotificationDispatcher>();
        return services;
    }
}";
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, source);
        var summary = RegistrationSummaryBuilder.Build(tempFile);

        // Act
        var filtered = RegistrationSummaryBuilder.FilterByType(summary, "NonExistentService");

        // Assert
        filtered.Records.Should().BeEmpty();
    }

    [Fact]
    public void FilterByType_WithEmptyFilter_ReturnsAllRecords()
    {
        // Arrange
        var source = @"using Microsoft.Extensions.DependencyInjection;
public static class Extensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<AnalyticsProcessor>();
        services.AddScoped<NotificationDispatcher>();
        return services;
    }
}";
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, source);
        var summary = RegistrationSummaryBuilder.Build(tempFile);
        var originalCount = summary.Records.Count;

        // Act
        var filtered = RegistrationSummaryBuilder.FilterByType(summary, "");

        // Assert
        filtered.Records.Should().HaveCount(originalCount);
    }

    [Fact]
    public void FilterByType_NoPartialMatches()
    {
        // Arrange
        var source = @"using Microsoft.Extensions.DependencyInjection;
public static class Extensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<AnalyticsProcessor>();
        services.AddScoped<NotificationDispatcher>();
        return services;
    }
}";
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, source);
        var summary = RegistrationSummaryBuilder.Build(tempFile);

        // Act - "Analytics" should NOT match "AnalyticsProcessor"
        var filtered = RegistrationSummaryBuilder.FilterByType(summary, "Analytics");

        // Assert
        filtered.Records.Should().BeEmpty();
    }
}
