namespace IoCTools.Generator.Utilities;

internal static class ConfigurationOptionsScanner
{
    public static List<ConfigurationOptionsRegistration> GetConfigurationOptionsToRegister(
        SemanticModel semanticModel,
        SyntaxNode root)
    {
        return GetConfigurationOptionsToRegister(semanticModel, root, sectionNameSuffixes: null);
    }

    public static List<ConfigurationOptionsRegistration> GetConfigurationOptionsToRegister(
        SemanticModel semanticModel,
        SyntaxNode root,
        IEnumerable<string>? sectionNameSuffixes)
    {
        var configOptions = new List<ConfigurationOptionsRegistration>();
        var processedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();
        foreach (var classDeclaration in typeDeclarations)
        {
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
            if (classSymbol == null) continue;
            if (classSymbol.IsAbstract || classSymbol.TypeKind == TypeKind.Interface) continue;

            var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
            var hasConditionalServiceAttribute = classSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
            var isHostedServiceType = TypeAnalyzer.IsAssignableFromIHostedService(classSymbol);
            if (!hasLifetimeAttribute && !hasConditionalServiceAttribute && !isHostedServiceType) continue;

            var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(classSymbol, semanticModel);
            foreach (var configField in configFields)
            {
                var targetType = configField.IsOptionsPattern
                    ? configField.GetOptionsInnerType()
                    : configField.FieldType;

                if (targetType == null) continue;

                if (targetType.SpecialType != SpecialType.None) continue; // primitives/strings not option classes
                if (!(targetType.IsReferenceType && targetType.TypeKind == TypeKind.Class)) continue;
                if (!processedTypes.Add(targetType)) continue;

                var sectionName = configField.GetSectionName();
                configOptions.Add(new ConfigurationOptionsRegistration(targetType, sectionName));
            }

            var regularDependencies = DependencyAnalyzer.GetConstructorDependencies(classSymbol, semanticModel);
            foreach (var dependency in regularDependencies.AllDependencies)
                if (dependency.ServiceType is INamedTypeSymbol namedType &&
                    namedType.OriginalDefinition.ToDisplayString()
                        .StartsWith("Microsoft.Extensions.Options.IOptions"))
                {
                    var optionsInnerType = namedType.TypeArguments.FirstOrDefault();
                    if (optionsInnerType != null && optionsInnerType.SpecialType == SpecialType.None &&
                        optionsInnerType.IsReferenceType && optionsInnerType.TypeKind == TypeKind.Class &&
                        processedTypes.Add(optionsInnerType))
                    {
                        var sectionName = ConfigurationNamingUtilities.InferSectionNameFromType(
                            optionsInnerType.Name,
                            sectionNameSuffixes ?? ConfigurationNamingUtilities.GetDefaultSectionNameSuffixes());
                        configOptions.Add(new ConfigurationOptionsRegistration(optionsInnerType, sectionName));
                    }
                }
        }

        return configOptions;
    }

    private static string InferSectionNameFromType(ITypeSymbol type)
    {
        var typeName = type.Name;
        return ConfigurationNamingUtilities.InferSectionNameFromType(typeName);
    }
}
