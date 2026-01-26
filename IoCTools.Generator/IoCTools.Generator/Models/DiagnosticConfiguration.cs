namespace IoCTools.Generator.Models;

using System.Text.RegularExpressions;

public class DiagnosticConfiguration
{
    public DiagnosticSeverity NoImplementationSeverity { get; set; } = DiagnosticSeverity.Error;
    public DiagnosticSeverity ManualImplementationSeverity { get; set; } = DiagnosticSeverity.Error;
    public DiagnosticSeverity LifetimeValidationSeverity { get; set; } = DiagnosticSeverity.Error;
    public bool DiagnosticsEnabled { get; set; } = true;
    public bool LifetimeValidationEnabled { get; set; } = true;

    // Compiled regex patterns for matching cross-assembly interfaces to ignore
    // These allow configuration of interfaces that are provided by external assemblies
    // without requiring IOC001/IOC002 diagnostics
    public Regex[] CompiledIgnoredPatterns { get; set; } = Array.Empty<Regex>();

    // Framework base types that should be excluded from MissedOpportunityValidator suggestions
    // These are types that have their own registration mechanisms (ASP.NET Core, EF Core, etc.)
    public HashSet<string> FrameworkBaseTypes { get; set; } = GetDefaultFrameworkBaseTypes();

    /// <summary>
    ///     Gets the default set of framework base types that should not be suggested for IoCTools attributes.
    /// </summary>
    internal static HashSet<string> GetDefaultFrameworkBaseTypes() => new(StringComparer.Ordinal)
    {
        // ASP.NET Core Authentication
        "Microsoft.AspNetCore.Authentication.AuthenticationHandler",
        "AuthenticationHandler",

        // ASP.NET Core MVC/Controllers
        "Microsoft.AspNetCore.Mvc.ControllerBase",
        "Microsoft.AspNetCore.Mvc.Controller",
        "ControllerBase",
        "Controller",

        // ASP.NET Core Razor Pages
        "Microsoft.AspNetCore.Mvc.RazorPages.PageModel",
        "PageModel",

        // ASP.NET Core SignalR
        "Microsoft.AspNetCore.SignalR.Hub",
        "Hub",

        // ASP.NET Core Minimal API Endpoint Filters
        "Microsoft.AspNetCore.Http.IEndpointFilter",
        "IEndpointFilter",

        // Entity Framework Core
        "Microsoft.EntityFrameworkCore.DbContext",
        "DbContext",

        // Mediator/MediatR handlers (registered by Mediator infrastructure)
        "Mediator.IRequestHandler",
        "Mediator.INotificationHandler",
        "Mediator.IStreamRequestHandler",
        "Mediator.IPipelineBehavior",
        "MediatR.IRequestHandler",
        "MediatR.INotificationHandler",
        "MediatR.IStreamRequestHandler",
        "MediatR.IPipelineBehavior",

        // gRPC
        "Grpc.Core.ClientBase",
        "ClientBase",

        // ASP.NET Core Tag Helpers
        "Microsoft.AspNetCore.Razor.TagHelpers.TagHelper",
        "TagHelper",

        // ASP.NET Core View Components
        "Microsoft.AspNetCore.Mvc.ViewComponent",
        "ViewComponent",

        // ASP.NET Core Health Checks
        "Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck",
        "IHealthCheck",

        // Hosted services are handled by IoCTools but should not emit IOC068
        "Microsoft.Extensions.Hosting.BackgroundService",
        "BackgroundService",
        "Microsoft.Extensions.Hosting.IHostedService",
        "IHostedService"
    };
}
