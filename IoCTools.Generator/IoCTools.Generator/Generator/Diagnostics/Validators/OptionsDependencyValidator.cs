namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System.Linq;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis;

using Models;

internal static class OptionsDependencyValidator
{
    internal static void Validate(SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        if (hierarchyDependencies?.RawAllDependencies == null) return;

        foreach (var dep in hierarchyDependencies.RawAllDependencies)
        {
            if (dep.ServiceType is not INamedTypeSymbol named) continue;
            if (!IsOptionsType(named)) continue;

            var location = classSymbol.Locations.FirstOrDefault();
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.OptionsDependencyNotSupported,
                location,
                named.ToDisplayString(),
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsOptionsType(INamedTypeSymbol named)
    {
        var def = named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return def.Contains("Microsoft.Extensions.Options.IOptions<") ||
               def.Contains("Microsoft.Extensions.Options.IOptionsSnapshot<") ||
               def.Contains("Microsoft.Extensions.Options.IOptionsMonitor<");
    }
}
