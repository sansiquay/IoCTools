#nullable enable
namespace RegistrationProject.Extensions.Generated;

using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Configuration;
using RegistrationProject.Services;

public static class GeneratedServiceCollectionExtensions
{
    public static IServiceCollection AddRegistrationProjectRegisteredServices(this IServiceCollection services, IConfiguration configuration)
    {
         if (string.Equals(configuration["Features:EnableBackground"], "true", StringComparison.OrdinalIgnoreCase))
         {
             services.AddSingleton<BackgroundMetricsService>();
         }
         services.AddSingleton<AnalyticsProcessor>();
         services.AddSingleton<IAnalyticsProcessor, AnalyticsProcessor>();
         services.AddScoped<NotificationDispatcher, NotificationDispatcher>();
         services.AddScoped<INotificationDispatcher>(provider => provider.GetRequiredService<NotificationDispatcher>());
         services.Configure<NotificationOptions>(options => configuration.GetSection("Notifications").Bind(options));
         return services;
    }
}
