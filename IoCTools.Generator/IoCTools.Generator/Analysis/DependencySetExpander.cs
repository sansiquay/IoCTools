namespace IoCTools.Generator.Analysis;

internal sealed class DependencySetExpansionResult
{
    public DependencySetExpansionResult(
        List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal, INamedTypeSymbol? OriginSet)> dependencies,
        List<(ConfigurationInjectionInfo Config, INamedTypeSymbol? OriginSet)> configurations)
    {
        Dependencies = dependencies;
        Configurations = configurations;
    }

    public List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal, INamedTypeSymbol? OriginSet)>
        Dependencies { get; }

    public List<(ConfigurationInjectionInfo Config, INamedTypeSymbol? OriginSet)> Configurations { get; }
}

/// <summary>
///     Expands dependency sets referenced via [DependsOn<Set>] into concrete dependency/configuration entries.
/// </summary>
internal static class DependencySetExpander
{
    public static DependencySetExpansionResult ExpandForType(
        INamedTypeSymbol typeSymbol,
        SemanticModel semanticModel,
        HashSet<string>? allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations,
        SourceProductionContext? context = null,
        TypeDeclarationSyntax? consumerDeclaration = null)
    {
        var compilation = semanticModel.Compilation;
        var seenTypes = new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default);
        var dependencies =
            new List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal, INamedTypeSymbol? OriginSet)>();
        var configurations = new List<(ConfigurationInjectionInfo Config, INamedTypeSymbol? OriginSet)>();

        var rawDepends = DependsOnFieldAnalyzer.GetRawDependsOnFieldsForTypeWithExternalFlag(typeSymbol,
            allRegisteredServices, allImplementations);

        foreach (var dep in rawDepends)
        {
            if (dep.ServiceType is INamedTypeSymbol depNamed && DependencySetUtilities.IsDependencySet(depNamed))
            {
                ExpandSet(depNamed, compilation, semanticModel, allRegisteredServices, allImplementations, seenTypes,
                    dependencies, configurations, context, consumerDeclaration, new Stack<INamedTypeSymbol>(),
                    typeSymbol);
                continue;
            }

            AddDependency(dep.ServiceType, dep.FieldName, dep.IsExternal, null, seenTypes, dependencies, context,
                consumerDeclaration, typeSymbol);
        }

        return new DependencySetExpansionResult(dependencies, configurations);
    }

    private static void ExpandSet(
        INamedTypeSymbol setSymbol,
        Compilation compilation,
        SemanticModel consumerSemanticModel,
        HashSet<string>? allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations,
        Dictionary<ITypeSymbol, string> seenTypes,
        List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal, INamedTypeSymbol? OriginSet)> dependencies,
        List<(ConfigurationInjectionInfo Config, INamedTypeSymbol? OriginSet)> configurations,
        SourceProductionContext? context,
        TypeDeclarationSyntax? consumerDeclaration,
        Stack<INamedTypeSymbol> recursionStack,
        INamedTypeSymbol consumerSymbol)
    {
        if (recursionStack.Any(s => SymbolEqualityComparer.Default.Equals(s, setSymbol)))
        {
            var cycle = string.Join(" -> ", recursionStack.Select(s => s.Name).Concat(new[] { setSymbol.Name }));
            ReportDiagnostic(context, DiagnosticDescriptors.DependencySetCycleDetected,
                consumerDeclaration?.GetLocation() ?? setSymbol.Locations.FirstOrDefault(), setSymbol.Name, cycle);
            return;
        }

        recursionStack.Push(setSymbol);

        var setContent = GetSetContent(setSymbol, compilation, consumerSemanticModel, allRegisteredServices,
            allImplementations);

        foreach (var dep in setContent.Dependencies)
        {
            if (dep.ServiceType is INamedTypeSymbol nestedSet && DependencySetUtilities.IsDependencySet(nestedSet))
            {
                ExpandSet(nestedSet, compilation, consumerSemanticModel, allRegisteredServices, allImplementations,
                    seenTypes, dependencies, configurations, context, consumerDeclaration, recursionStack,
                    consumerSymbol);
                continue;
            }

            AddDependency(dep.ServiceType, dep.FieldName, dep.IsExternal, setSymbol, seenTypes, dependencies,
                context, consumerDeclaration, consumerSymbol);
        }

        foreach (var config in setContent.Configuration)
            configurations.Add((config, setSymbol));

        recursionStack.Pop();
    }

    private static void AddDependency(
        ITypeSymbol serviceType,
        string fieldName,
        bool isExternal,
        INamedTypeSymbol? originSet,
        Dictionary<ITypeSymbol, string> seenTypes,
        List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal, INamedTypeSymbol? OriginSet)> dependencies,
        SourceProductionContext? context,
        TypeDeclarationSyntax? consumerDeclaration,
        INamedTypeSymbol consumerSymbol)
    {
        if (seenTypes.TryGetValue(serviceType, out var existingName))
        {
            if (!string.Equals(existingName, fieldName, StringComparison.Ordinal))
                ReportDiagnostic(context, DiagnosticDescriptors.DependencySetNameCollision,
                    consumerDeclaration?.GetLocation() ?? consumerSymbol.Locations.FirstOrDefault(),
                    TypeHelpers.FormatTypeNameForDiagnostic(serviceType), existingName, fieldName,
                    consumerSymbol.Name);

            return;
        }

        seenTypes[serviceType] = fieldName;
        dependencies.Add((serviceType, fieldName, isExternal, originSet));
    }

    private static (List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)> Dependencies,
        List<ConfigurationInjectionInfo> Configuration) GetSetContent(
            INamedTypeSymbol setSymbol,
            Compilation compilation,
            SemanticModel consumerSemanticModel,
            HashSet<string>? allRegisteredServices,
            Dictionary<string, List<INamedTypeSymbol>>? allImplementations)
    {
        var setSyntax = setSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;
        var semanticModel = setSyntax != null
            ? compilation.GetSemanticModel(setSyntax)
            : consumerSemanticModel;

        var deps = DependsOnFieldAnalyzer.GetRawDependsOnFieldsForTypeWithExternalFlag(setSymbol, allRegisteredServices,
            allImplementations);
        var configs = ConfigurationFieldAnalyzer.GetConfigurationInjectedFieldsForType(setSymbol, semanticModel);

        return (deps, configs);
    }

    private static void ReportDiagnostic(SourceProductionContext? context,
        DiagnosticDescriptor descriptor,
        Location? location,
        params object?[] messageArgs)
    {
        if (location == null) return;
        context?.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArgs));
    }
}
