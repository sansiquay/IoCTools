namespace IoCTools.Generator.Generator.Diagnostics.Validators;

internal static class UnnecessaryExternalDependencyValidator
{
    internal static void Validate(SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations)
    {
        var dependsOnAttributes = classSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn", StringComparison.Ordinal) == true)
            .Where(attr => !AttributeParser.IsDependsOnConfigurationAttribute(attr))
            .ToList();

        foreach (var attribute in dependsOnAttributes)
        {
            var (namingConvention, stripI, prefix, externalFlag, _) =
                AttributeParser.GetDependsOnOptionsFromAttribute(attribute);

            if (!externalFlag) continue;

            var typeArguments = attribute.AttributeClass?.TypeArguments ?? default;
            if (typeArguments.IsDefaultOrEmpty) continue;

            foreach (var genericTypeArgument in typeArguments)
            {
                var dependencyDisplay = genericTypeArgument.ToDisplayString();

                // Framework / first‑party DI services never need External.
                if (TypeHelpers.IsFrameworkTypeAdapted(dependencyDisplay))
                    Report(context, attribute, classSymbol, dependencyDisplay);
                else if (HasInternalImplementation(genericTypeArgument, allRegisteredServices, allImplementations))
                    Report(context, attribute, classSymbol, dependencyDisplay);
            }
        }
    }

    private static bool HasInternalImplementation(ITypeSymbol dependencyType,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations)
    {
        var typeName = dependencyType.ToDisplayString();

        // If the service is registered anywhere in the current solution/references, External is unnecessary.
        if (allRegisteredServices.Contains(typeName)) return true;

        if (!allImplementations.TryGetValue(typeName, out var implementations)) return false;

        // If at least one implementation is NOT marked [ExternalService], it's internally provided.
        return implementations.Any(impl => impl.GetAttributes().All(attr =>
            attr.AttributeClass?.ToDisplayString() != "IoCTools.Abstractions.Annotations.ExternalServiceAttribute"));
    }

    private static void Report(SourceProductionContext context,
        AttributeData attribute,
        INamedTypeSymbol classSymbol,
        string dependencyDisplay)
    {
        var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ??
                       classSymbol.Locations.FirstOrDefault();

        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnnecessaryExternalDependency,
            location, dependencyDisplay, classSymbol.Name);
        context.ReportDiagnostic(diagnostic);
    }
}
