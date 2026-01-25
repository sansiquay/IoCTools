namespace IoCTools.Generator.Analysis;

/// <summary>
///     Context object holding all state for dependency set expansion.
///     Reduces parameter count in recursive methods and improves maintainability.
/// </summary>
internal sealed class ExpansionContext
{
    public ExpansionContext(
        Compilation compilation,
        SemanticModel consumerSemanticModel,
        HashSet<string>? allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations,
        SourceProductionContext? context,
        TypeDeclarationSyntax? consumerDeclaration,
        INamedTypeSymbol consumerSymbol)
    {
        Compilation = compilation;
        ConsumerSemanticModel = consumerSemanticModel;
        AllRegisteredServices = allRegisteredServices;
        AllImplementations = allImplementations;
        Context = context;
        ConsumerDeclaration = consumerDeclaration;
        ConsumerSymbol = consumerSymbol;
        SeenTypes = new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default);
        Dependencies = new List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal, INamedTypeSymbol? OriginSet)>();
        Configurations = new List<(ConfigurationInjectionInfo Config, INamedTypeSymbol? OriginSet)>();
        RecursionStack = new Stack<INamedTypeSymbol>();
    }

    public Compilation Compilation { get; }
    public SemanticModel ConsumerSemanticModel { get; }
    public HashSet<string>? AllRegisteredServices { get; }
    public Dictionary<string, List<INamedTypeSymbol>>? AllImplementations { get; }
    public SourceProductionContext? Context { get; }
    public TypeDeclarationSyntax? ConsumerDeclaration { get; }
    public INamedTypeSymbol ConsumerSymbol { get; }

    // Mutable state during expansion
    public Dictionary<ITypeSymbol, string> SeenTypes { get; }
    public List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal, INamedTypeSymbol? OriginSet)> Dependencies { get; }
    public List<(ConfigurationInjectionInfo Config, INamedTypeSymbol? OriginSet)> Configurations { get; }
    public Stack<INamedTypeSymbol> RecursionStack { get; }
}

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
        Dependencies
    { get; }

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
        var expansionContext = new ExpansionContext(
            semanticModel.Compilation,
            semanticModel,
            allRegisteredServices,
            allImplementations,
            context,
            consumerDeclaration,
            typeSymbol);

        var rawDepends = DependsOnFieldAnalyzer.GetRawDependsOnFieldsForTypeWithExternalFlag(typeSymbol,
            allRegisteredServices, allImplementations);

        foreach (var dep in rawDepends)
        {
            if (dep.ServiceType is INamedTypeSymbol depNamed && DependencySetUtilities.IsDependencySet(depNamed))
            {
                ExpandSet(expansionContext, depNamed);
                continue;
            }

            AddDependency(expansionContext, dep.ServiceType, dep.FieldName, dep.IsExternal, null);
        }

        return new DependencySetExpansionResult(expansionContext.Dependencies, expansionContext.Configurations);
    }

    private static void ExpandSet(ExpansionContext context, INamedTypeSymbol setSymbol)
    {
        if (context.RecursionStack.Any(s => SymbolEqualityComparer.Default.Equals(s, setSymbol)))
        {
            var cycle = string.Join(" -> ", context.RecursionStack.Select(s => s.Name).Concat(new[] { setSymbol.Name }));
            ReportDiagnostic(context.Context, DiagnosticDescriptors.DependencySetCycleDetected,
                context.ConsumerDeclaration?.GetLocation() ?? setSymbol.Locations.FirstOrDefault(), setSymbol.Name, cycle);
            return;
        }

        context.RecursionStack.Push(setSymbol);

        var setContent = GetSetContent(context, setSymbol);

        foreach (var dep in setContent.Dependencies)
        {
            if (dep.ServiceType is INamedTypeSymbol nestedSet && DependencySetUtilities.IsDependencySet(nestedSet))
            {
                ExpandSet(context, nestedSet);
                continue;
            }

            AddDependency(context, dep.ServiceType, dep.FieldName, dep.IsExternal, setSymbol);
        }

        foreach (var config in setContent.Configuration)
            context.Configurations.Add((config, setSymbol));

        context.RecursionStack.Pop();
    }

    private static void AddDependency(
        ExpansionContext context,
        ITypeSymbol serviceType,
        string fieldName,
        bool isExternal,
        INamedTypeSymbol? originSet)
    {
        if (context.SeenTypes.TryGetValue(serviceType, out var existingName))
        {
            if (!string.Equals(existingName, fieldName, StringComparison.Ordinal))
                ReportDiagnostic(context.Context, DiagnosticDescriptors.DependencySetNameCollision,
                    context.ConsumerDeclaration?.GetLocation() ?? context.ConsumerSymbol.Locations.FirstOrDefault(),
                    TypeNameUtilities.FormatTypeNameForDiagnostic(serviceType), existingName, fieldName,
                    context.ConsumerSymbol.Name);

            return;
        }

        context.SeenTypes[serviceType] = fieldName;
        context.Dependencies.Add((serviceType, fieldName, isExternal, originSet));
    }

    private static (List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)> Dependencies,
        List<ConfigurationInjectionInfo> Configuration) GetSetContent(
            ExpansionContext context,
            INamedTypeSymbol setSymbol)
    {
        var setSyntax = setSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;
        var semanticModel = setSyntax != null
            ? context.Compilation.GetSemanticModel(setSyntax)
            : context.ConsumerSemanticModel;

        var deps = DependsOnFieldAnalyzer.GetRawDependsOnFieldsForTypeWithExternalFlag(setSymbol,
            context.AllRegisteredServices, context.AllImplementations);
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
