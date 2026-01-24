namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Utilities;

internal static class MissedOpportunityValidator
{
    /// <summary>
    ///     Known framework base types that have their own registration mechanisms.
    ///     Classes inheriting from these should not be suggested for IoCTools attributes.
    /// </summary>
    private static readonly HashSet<string> FrameworkBaseTypes = new(StringComparer.Ordinal)
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

    internal static void Validate(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel,
        string implicitLifetime)
    {
        if (classSymbol.IsAbstract) return;

        // Skip if already opted-in or external/conditional/register-as
        var hasLifetime = ServiceDiscovery.GetLifetimeAttributes(classSymbol).HasAny;
        var hasDependsOn = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name?.StartsWith("DependsOn") == true);
        var hasInjectFields = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(classSymbol);
        var hasRegisterAsAll = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "RegisterAsAllAttribute");
        var hasRegisterAs = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name?.StartsWith("RegisterAsAttribute") == true);
        var hasConditional = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "ConditionalServiceAttribute");
        var isExternal = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "ExternalServiceAttribute");
        var hasSkipRegistration = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "SkipRegistrationAttribute" ||
                                                                       a.AttributeClass?.Name?.StartsWith("SkipRegistrationAttribute") == true);

        if (hasLifetime || hasDependsOn || hasInjectFields || hasRegisterAsAll || hasRegisterAs || hasConditional ||
            isExternal || hasSkipRegistration)
            return;

        // Skip classes that inherit from known framework base types
        if (InheritsFromFrameworkType(classSymbol))
            return;

        // Must be partial to benefit from generator; otherwise still suggest (info) since user can make it partial.

        var constructors = classSymbol.InstanceConstructors
            .Where(ctor => ctor.DeclaredAccessibility == Accessibility.Public || ctor.DeclaredAccessibility == Accessibility.Internal)
            .Where(ctor => !ctor.IsImplicitlyDeclared)
            .ToList();

        var ctor = constructors.FirstOrDefault(c => c.Parameters.Length > 0);
        if (ctor == null) return;

        // Skip if constructor calls base(...) with arguments - indicates framework integration
        if (HasBaseConstructorCallWithArguments(classDeclaration, ctor))
            return;

        // Require all parameters to be non-primitive reference or interface types
        var injectableParams = ctor.Parameters
            .Where(p => !p.Type.IsValueType && p.Type.SpecialType == SpecialType.None)
            .OfType<IParameterSymbol>()
            .ToList();

        if (injectableParams.Count != ctor.Parameters.Length) return;

        var paramInterfaces = injectableParams
            .Select(p => p.Type.ToDisplayString())
            .ToArray();

        var joined = string.Join(", ", paramInterfaces);

        var location = classDeclaration.Identifier.GetLocation();
        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.ConstructorCouldUseDependsOn,
            location,
            classSymbol.Name,
            joined);
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    ///     Checks if the class inherits from any known framework base type.
    /// </summary>
    private static bool InheritsFromFrameworkType(INamedTypeSymbol classSymbol)
    {
        // Check base type chain
        var current = classSymbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (IsFrameworkType(current))
                return true;
            current = current.BaseType;
        }

        // Check implemented interfaces
        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (IsFrameworkType(iface))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Checks if a type matches any known framework type by name.
    /// </summary>
    private static bool IsFrameworkType(INamedTypeSymbol type)
    {
        var fullName = type.ToDisplayString();
        var simpleName = type.Name;

        // Check exact full name match
        if (FrameworkBaseTypes.Contains(fullName))
            return true;

        // Check simple name match
        if (FrameworkBaseTypes.Contains(simpleName))
            return true;

        // For generic types, also check the constructed-from type
        if (type.IsGenericType)
        {
            var constructedFrom = type.ConstructedFrom;
            var constructedFullName = constructedFrom.ToDisplayString();
            var constructedSimpleName = constructedFrom.Name;

            if (FrameworkBaseTypes.Contains(constructedFullName) ||
                FrameworkBaseTypes.Contains(constructedSimpleName))
                return true;

            // Check with arity suffix (e.g., "AuthenticationHandler`1")
            var nameWithArity = constructedSimpleName + "`" + type.TypeArguments.Length;
            if (FrameworkBaseTypes.Any(f => f.EndsWith(nameWithArity, StringComparison.Ordinal) ||
                                            f.EndsWith("." + nameWithArity, StringComparison.Ordinal)))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Checks if a constructor has a base(...) initializer with arguments.
    ///     This is a strong signal that the class is integrating with a framework base class.
    /// </summary>
    private static bool HasBaseConstructorCallWithArguments(TypeDeclarationSyntax classDeclaration,
        IMethodSymbol ctorSymbol)
    {
        // Find the constructor syntax that matches this symbol
        var ctorSyntaxes = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => c.ParameterList.Parameters.Count == ctorSymbol.Parameters.Length);

        foreach (var ctorSyntax in ctorSyntaxes)
        {
            // Check if this constructor has a base(...) initializer with arguments
            if (ctorSyntax.Initializer is { ThisOrBaseKeyword.Text: "base" } initializer &&
                initializer.ArgumentList.Arguments.Count > 0)
            {
                return true;
            }
        }

        return false;
    }
}
