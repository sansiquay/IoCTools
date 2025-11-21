namespace IoCTools.Generator.Analysis;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

using Utilities;

/// <summary>
///     Determines whether a dependency type should be treated as external.
/// </summary>
internal static class ExternalServiceAnalyzer
{
    public static bool IsTypeExternal(
        ITypeSymbol dependencyType,
        HashSet<string>? allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations)
    {
        if (allImplementations == null || allRegisteredServices == null)
            return false;

        // Built-in DI helper patterns and framework services are never external
        var dependencyTypeName = dependencyType.ToDisplayString();
        if (IsAdvancedDIPattern(dependencyType) || TypeHelpers.IsFrameworkTypeAdapted(dependencyTypeName) ||
            IsKnownBuiltinService(dependencyType))
            return false;

        // If the service is already registered or implemented in the solution/references, it's internal
        if (allRegisteredServices.Contains(dependencyTypeName)) return false;

        if (!allImplementations.TryGetValue(dependencyTypeName, out var implementations))
            return false;

        return implementations.Any(impl => impl.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.ExternalServiceAttribute"));
    }

    private static bool IsAdvancedDIPattern(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType) return false;
        var typeName = namedType.OriginalDefinition.ToDisplayString();
        return typeName == "System.Func<>" ||
               typeName == "System.Lazy<>" ||
               typeName.StartsWith("System.Func<") ||
               typeName.StartsWith("System.Lazy<") ||
               (type.CanBeReferencedByName && type.NullableAnnotation == NullableAnnotation.Annotated);
    }

    private static bool IsKnownBuiltinService(ITypeSymbol type)
    {
        var display = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return display.StartsWith("global::Microsoft.Extensions.Logging.ILogger") ||
               display == "global::Microsoft.Extensions.Logging.ILoggerFactory" ||
               display == "global::Microsoft.Extensions.Configuration.IConfiguration" ||
               display.StartsWith("global::Microsoft.Extensions.Options.IOptions<") ||
               display.StartsWith("global::Microsoft.Extensions.Options.IOptionsSnapshot<") ||
               display.StartsWith("global::Microsoft.Extensions.Options.IOptionsMonitor<") ||
               display == "global::System.IServiceProvider" ||
               display == "global::Microsoft.Extensions.DependencyInjection.IServiceScopeFactory" ||
               display == "global::Microsoft.Extensions.Hosting.IHostEnvironment" ||
               display == "global::Microsoft.AspNetCore.Hosting.IWebHostEnvironment";
    }
}
