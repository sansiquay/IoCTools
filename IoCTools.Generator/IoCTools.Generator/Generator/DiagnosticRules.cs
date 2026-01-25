namespace IoCTools.Generator.Generator;

using System;
using System.Collections.Immutable;
using System.Linq;

using Diagnostics.Validators;

using Microsoft.CodeAnalysis;

internal static class DiagnosticRules
{
    // Attribute combinations (IOC02x family; current focus on ConditionalService rules)
    public static void ValidateAttributeCombinations(SourceProductionContext context,
        IEnumerable<INamedTypeSymbol> servicesWithAttributes)
    {
        ConditionalServiceValidator
            .ValidateAttributeCombinations(context, servicesWithAttributes);
    }

    // Conditional services handled by ConditionalServiceValidator

    public static void ValidateCircularDependenciesComplete(SourceProductionContext context,
        List<INamedTypeSymbol> servicesWithAttributes,
        HashSet<string> allRegisteredServices,
        DiagnosticConfiguration diagnosticConfig)
    {
        CircularDependencyValidator
            .ValidateCircularDependenciesComplete(context, servicesWithAttributes, allRegisteredServices,
                diagnosticConfig);
    }

    // IOC016-IOC019: Validate configuration injection
    public static void ValidateConfigurationInjection(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        ConfigurationInjectionValidator
            .ValidateConfigurationInjection(context, classDeclaration, classSymbol);
    }

    public static void ValidateRedundantServiceConfigurations(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        RedundantConfigurationValidator
            .Validate(context, classDeclaration, classSymbol);
    }

    // IOC011: Validate HostedService requirements (partial class requirement)
    public static void ValidateHostedServiceRequirements(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(classSymbol);
        if (!isHostedService) return;

        var isPartial = classDeclaration.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PartialKeyword));

        var hasInjectFields = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"));
        var hasDependsOnAttributes = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);

        if ((hasInjectFields || hasDependsOnAttributes) && !isPartial)
        {
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.BackgroundServiceNotPartial,
                classDeclaration.GetLocation(), classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    // IOC001/IOC002: Validate missing/unregistered dependencies
    public static void ValidateMissingDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        Dictionary<string, string> serviceLifetimes,
        DiagnosticConfiguration diagnosticConfig,
        INamedTypeSymbol classSymbol,
        string implicitLifetime)
    {
        foreach (var dependency in hierarchyDependencies.AllDependenciesWithExternalFlag)
        {
            if (dependency.IsExternal) continue;

            if (dependency.Source == DependencySource.ConfigurationInjection &&
                dependency.FieldName != "_configuration")
                continue;

            var dependencyType = dependency.ServiceType.ToDisplayString();

            // Heuristic: clean-architecture abstractions often live alongside the application layer
            // while their implementations live in an infrastructure assembly that is not referenced
            // by the current project. If the interface has no implementations in this compilation
            // and sits in an *Abstractions* / *Contracts* / *Interfaces* namespace, assume it is
            // provided by another assembly to avoid noisy IOC001/IOC002 false positives.
            if (dependency.ServiceType.TypeKind == TypeKind.Interface &&
                !allImplementations.ContainsKey(dependencyType) &&
                !allRegisteredServices.Contains(dependencyType))
            {
                var ns = dependency.ServiceType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                if (ns.IndexOf("Abstractions", StringComparison.Ordinal) >= 0 ||
                    ns.IndexOf("Contracts", StringComparison.Ordinal) >= 0 ||
                    ns.IndexOf("Interfaces", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }
            }

            // If the dependency interface lives in a different assembly than the consuming class, assume
            // it may be provided by another project/host; avoid false positives in clean-architecture splits.
            // Cross-assembly dependencies: assume provided elsewhere (clean architecture splits); skip IOC001/002 here
            if (!SymbolEqualityComparer.Default.Equals(dependency.ServiceType.ContainingAssembly,
                    classSymbol.ContainingAssembly))
                continue;

            if (TypeHelpers.IsFrameworkTypeAdapted(dependencyType) ||
                FrameworkTypes.KnownTypes.Contains(dependencyType) ||
                allRegisteredServices.Contains(dependencyType))
                continue;

            var serviceTypeName = dependency.ServiceType.ToDisplayString();

            // Check if the type matches any configured ignored patterns
            // This allows configuration of cross-assembly interfaces that are provided
            // by external assemblies without triggering IOC001/IOC002 diagnostics
            if (diagnosticConfig.CompiledIgnoredPatterns.Any(pattern => pattern.IsMatch(serviceTypeName)))
                continue;

            // Special handling for IEnumerable<T> dependencies
            var enumerableTypeInfo = TypeHelpers.ExtractIEnumerableFromWrappedType(dependencyType);
            if (enumerableTypeInfo != null)
            {
                var innerType = enumerableTypeInfo.InnerType;
                var hasInnerTypeImplementations = allImplementations.ContainsKey(innerType) ||
                                                  allRegisteredServices.Contains(innerType) ||
                                                  serviceLifetimes.ContainsKey(innerType);
                if (!hasInnerTypeImplementations)
                {
                    var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                        DiagnosticDescriptors.NoImplementationFound, diagnosticConfig.NoImplementationSeverity);
                    var diagnostic = Diagnostic.Create(descriptor,
                        classDeclaration.GetLocation(), classDeclaration.Identifier.ValueText, dependencyType);
                    context.ReportDiagnostic(diagnostic);
                }

                continue;
            }

            var (dependencyLifetime, _) =
                DependencyLifetimeResolver.GetDependencyLifetimeWithGenericSupportAndImplementationName(
                    dependency.ServiceType, serviceLifetimes, allRegisteredServices, allImplementations, implicitLifetime);

            var implementationExists = dependencyLifetime != null || allImplementations.ContainsKey(dependencyType);
            if (implementationExists)
            {
                if (dependencyLifetime != null) continue; // registered

                if (allImplementations.ContainsKey(dependencyType))
                {
                    var implementations = allImplementations[dependencyType];
                    var hasRegisteredImplementation = implementations.Any(impl =>
                        allRegisteredServices.Contains(impl.ToDisplayString()));

                    if (!hasRegisteredImplementation)
                    {
                        var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                            DiagnosticDescriptors.ImplementationNotRegistered,
                            diagnosticConfig.ManualImplementationSeverity);
                        var diagnostic = Diagnostic.Create(descriptor,
                            classDeclaration.GetLocation(), classDeclaration.Identifier.ValueText, dependencyType);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
            else
            {
                var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                    DiagnosticDescriptors.NoImplementationFound, diagnosticConfig.NoImplementationSeverity);
                var diagnostic = Diagnostic.Create(descriptor,
                    classDeclaration.GetLocation(), classDeclaration.Identifier.ValueText, dependencyType);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    public static void SuggestSharedBaseLifetimes(SourceProductionContext context,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        HashSet<string> allRegisteredServices,
        string implicitLifetime)
    {
        // We only care about concrete implementations that currently are not registered because they lack any lifetime.
        var candidates = allImplementations.Values
            .SelectMany(x => x)
            .OfType<INamedTypeSymbol>()
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .Where(symbol => symbol.TypeKind == TypeKind.Class)
            .Where(symbol => symbol.BaseType != null && symbol.BaseType.SpecialType != SpecialType.System_Object)
            .Where(symbol => !ServiceDiscovery.GetLifetimeAttributes(symbol).HasAny)
            .Where(symbol => !allRegisteredServices.Contains(symbol.ToDisplayString()))
            .Where(symbol => symbol.Interfaces.Any());

        var groups = candidates.GroupBy<INamedTypeSymbol, INamedTypeSymbol>(
            symbol => symbol.BaseType!,
            SymbolEqualityComparer.Default);

        foreach (var group in groups)
        {
            // Require at least two derived services to avoid noisy suggestions.
            var derivedNeedingLifetime = group.ToList();
            if (derivedNeedingLifetime.Count < 2) continue;

            var baseType = group.Key;
            if (ServiceDiscovery.GetLifetimeAttributes(baseType).HasAny) continue; // already has a lifetime

            var baseLocation = baseType.Locations.FirstOrDefault(loc => loc.IsInSource);
            if (baseLocation == null) continue; // can't offer a fix if the base isn't in source

            var suggestedLifetime = string.IsNullOrWhiteSpace(implicitLifetime) ? "Scoped" : implicitLifetime;

            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SharedBaseMissingLifetimeSuggestion,
                baseLocation,
                baseType.Name,
                suggestedLifetime);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static List<IFieldSymbol> GetConfigurationFieldsFromHierarchy(INamedTypeSymbol classSymbol)
    {
        var result = new List<IFieldSymbol>();
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers().OfType<IFieldSymbol>())
            {
                var hasInjectConfigurationAttribute = member.GetAttributes()
                    .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                                 "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute");
                if (hasInjectConfigurationAttribute) result.Add(member);
            }

            currentType = currentType.BaseType;
        }

        return result;
    }

    private static bool IsSupportedConfigurationType(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Object) return true;
        if (type.SpecialType == SpecialType.System_String) return true;

        if (type.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T &&
            type is INamedTypeSymbol nullableType)
            return IsSupportedConfigurationType(nullableType.TypeArguments.First());

        if (type is IArrayTypeSymbol arrayType) return IsSupportedConfigurationType(arrayType.ElementType);

        if (type is INamedTypeSymbol namedType)
        {
            var typeName = namedType.ToDisplayString();
            if (namedType.TypeKind == TypeKind.Enum) return true;

            var supportedBuiltinTypes = new[]
            {
                "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", "System.Guid", "System.Uri",
                "System.Decimal"
            };
            if (supportedBuiltinTypes.Contains(typeName)) return true;

            if (typeName.StartsWith("Microsoft.Extensions.Options.IOptions") ||
                typeName.StartsWith("Microsoft.Extensions.Options.IOptionsSnapshot") ||
                typeName.StartsWith("Microsoft.Extensions.Options.IOptionsMonitor"))
                return true;

            if (typeName.StartsWith("System.Collections.Generic.List<") ||
                typeName.StartsWith("System.Collections.Generic.IList<") ||
                typeName.StartsWith("System.Collections.Generic.ICollection<") ||
                typeName.StartsWith("System.Collections.Generic.IEnumerable<") ||
                typeName.StartsWith("System.Collections.Generic.Dictionary<") ||
                typeName.StartsWith("System.Collections.Generic.IDictionary<"))
                if (namedType.TypeArguments.Length > 0)
                    return namedType.TypeArguments.All(arg => IsSupportedConfigurationType(arg));

            if (namedType.TypeKind == TypeKind.Interface) return false;
            if (namedType.IsAbstract) return false;
            return namedType.Constructors.Any(c => c.Parameters.Length == 0);
        }

        return false;
    }

    private static string GetUnsupportedTypeReason(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            if (type is INamedTypeSymbol interfaceType)
            {
                var typeName = interfaceType.ToDisplayString();
                if (typeName.StartsWith("Microsoft.Extensions.Options."))
                    return "requires parameterless constructor";
            }

            return "Interfaces cannot be bound from configuration";
        }

        if (type.IsAbstract) return "Abstract types cannot be bound from configuration";
        if (type is IArrayTypeSymbol) return "Array element type is not supported for configuration binding";
        if (type is INamedTypeSymbol namedType2)
        {
            var typeName2 = namedType2.ToDisplayString();
            if (typeName2.StartsWith("System.Collections.Generic."))
                return "Collection element type is not supported for configuration binding";
        }

        return "requires parameterless constructor";
    }

    private static ConfigurationKeyValidationResult ValidateConfigurationKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return new ConfigurationKeyValidationResult { IsValid = false, ErrorMessage = "empty or whitespace-only" };
        if (string.IsNullOrWhiteSpace(key))
            return new ConfigurationKeyValidationResult { IsValid = false, ErrorMessage = "empty or whitespace-only" };
        if (key!.Contains("::"))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "contains double colons (::)"
            };
        if (key!.StartsWith(":") || key.EndsWith(":"))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "cannot start or end with a colon (:)"
            };
        if (key.Any(c => c == '\0' || c == '\r' || c == '\n' || c == '\t'))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "contains invalid characters (null, carriage return, newline, or tab)"
            };
        return new ConfigurationKeyValidationResult { IsValid = true, ErrorMessage = string.Empty };
    }

    public static void ValidateDependencyRedundancy(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        DependencyUsageValidator
            .ValidateRedundantDependencies(context, classDeclaration, classSymbol, hierarchyDependencies);
    }

    public static void ValidateUnusedDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel? semanticModel,
        InheritanceHierarchyDependencies hierarchyDependencies) =>
        DependencyUsageValidator.ValidateUnusedDependencies(context, classDeclaration, classSymbol, semanticModel,
            hierarchyDependencies);

    public static void ValidateRedundantDependencyWrappers(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel? semanticModel,
        InheritanceHierarchyDependencies hierarchyDependencies) =>
        DependencyUsageValidator.ValidateRedundantDependencyWrappers(context, classDeclaration, classSymbol,
            semanticModel, hierarchyDependencies);

    public static void ValidateManualDependencyFieldShadows(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel? semanticModel,
        InheritanceHierarchyDependencies hierarchyDependencies) =>
        DependencyUsageValidator.ValidateManualDependencyFieldShadows(context, classDeclaration, classSymbol,
            semanticModel, hierarchyDependencies);

    public static void ValidateDuplicateDependsOn(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        DependsOnValidator
            .ValidateDuplicateDependsOn(context, classDeclaration, classSymbol);
    }

    public static void ValidateUnnecessaryExternalDependencies(SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations)
    {
        UnnecessaryExternalDependencyValidator.Validate(context, classSymbol, allRegisteredServices,
            allImplementations);
    }

    public static void ValidateOptionsDependencies(SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies) =>
        OptionsDependencyValidator.Validate(context, classSymbol, hierarchyDependencies);

    public static void ValidateNonServiceDependencies(SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies) =>
        NonServiceDependencyValidator.Validate(context, classSymbol, hierarchyDependencies);

    public static void ValidateCollectionDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies) =>
        CollectionDependencyValidator.Validate(context, classDeclaration, classSymbol, hierarchyDependencies);

    public static void ValidateIConfigurationUsage(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies) =>
        IConfigurationUsageValidator.Validate(context, classDeclaration, classSymbol, hierarchyDependencies);

    public static void ValidateConfigurationRedundancy(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel) =>
        ConfigurationRedundancyValidator.Validate(context, classDeclaration, classSymbol, semanticModel);

    public static void ValidateConfigurationBindings(SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<ServiceClassInfo> services) =>
        ConfigurationBindingPresenceValidator.Validate(context, compilation, services);

    public static void ValidateNullableDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        var nullableDeps = hierarchyDependencies.AllDependencies
            .Where(d => IsNullableDependencyType(d.ServiceType))
            .ToList();

        if (nullableDeps.Count == 0) return;

        var classAttributes = classSymbol.GetAttributes();
        var fieldLookup = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .ToDictionary(f => f.Name, f => f, StringComparer.Ordinal);

        foreach (var dependency in nullableDeps)
        {
            var dependencyType = dependency.ServiceType;

            var location = FindDependencyLocation(classDeclaration, classAttributes, fieldLookup, dependencyType,
                dependency.FieldName, dependency.Source) ?? classDeclaration.Identifier.GetLocation();

            var diag = Diagnostic.Create(DiagnosticDescriptors.NullableDependencyNotAllowed,
                location,
                dependencyType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                classSymbol.Name);
            context.ReportDiagnostic(diag);
        }
    }

    private static bool IsNullableDependencyType(ITypeSymbol type) =>
        type.NullableAnnotation == NullableAnnotation.Annotated ||
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

    private static Location? FindDependencyLocation(TypeDeclarationSyntax classDeclaration,
        ImmutableArray<AttributeData> classAttributes,
        IReadOnlyDictionary<string, IFieldSymbol> fieldLookup,
        ITypeSymbol dependencyType,
        string fieldName,
        DependencySource source)
    {
        switch (source)
        {
            case DependencySource.DependsOn:
                foreach (var attr in classAttributes)
                {
                    if (attr.AttributeClass == null) continue;
                    if (!attr.AttributeClass.Name.StartsWith("DependsOnAttribute", StringComparison.Ordinal))
                        continue;

                    if (attr.AttributeClass.TypeArguments.Any(t =>
                            SymbolEqualityComparer.Default.Equals(
                                t.WithNullableAnnotation(NullableAnnotation.None),
                                dependencyType.WithNullableAnnotation(NullableAnnotation.None))))
                        return attr.ApplicationSyntaxReference?.GetSyntax().GetLocation();
                }

                break;

            case DependencySource.Inject:
            case DependencySource.ConfigurationInjection:
                if (fieldLookup.TryGetValue(fieldName, out var field) &&
                    field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is SyntaxNode node)
                    return node.GetLocation();

                break;
        }

        return classDeclaration.Identifier.GetLocation();
    }

    public static void ValidateParamsStyleAttributes(SourceProductionContext context,
        INamedTypeSymbol classSymbol)
    {
        foreach (var attribute in classSymbol.GetAttributes())
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
            if (location == null || attribute.AttributeClass == null) continue;

            if (!AttributeParser.IsDependsOnConfigurationAttribute(attribute)) continue;

            if (attribute.NamedArguments.Any(arg => arg.Key == "ConfigurationKeys"))
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.PreferParamsStyleAttributeArguments,
                    location,
                    attribute.AttributeClass.Name, classSymbol.Name, "ConfigurationKeys");
                context.ReportDiagnostic(diagnostic);
            }
            else if (attribute.NamedArguments.Any(arg => arg.Key == "MemberNames"))
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.PreferParamsStyleAttributeArguments,
                    location,
                    attribute.AttributeClass.Name, classSymbol.Name, "MemberNames");
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    public static void ValidateRedundantMemberNames(SourceProductionContext context,
        INamedTypeSymbol classSymbol) =>
        MemberNameRedundancyValidator.Validate(context, classSymbol);

    public static bool ValidateManualConstructorMixing(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol) =>
        ManualConstructorMixingValidator.ReportIfMixed(context, classDeclaration, classSymbol);

    public static void ValidateDuplicatesWithinSingleDependsOn(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        DependsOnValidator
            .ValidateDuplicatesWithinSingleDependsOn(context, classDeclaration, classSymbol);
    }

    // IOC009: Validates unnecessary SkipRegistration attributes for interfaces not registered by RegisterAsAll
    public static void ValidateUnnecessarySkipRegistration(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        DependsOnValidator
            .ValidateUnnecessarySkipRegistration(context, classDeclaration, classSymbol);
    }

    public static void ValidateInjectFieldPreferences(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol) =>
        InjectUsageValidator.ValidatePreferDependsOn(context, classDeclaration, classSymbol);

    // IOC080: Validate that classes with code-generating attributes are marked as partial
    public static void ValidateMissingPartialKeyword(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol) =>
        MissingPartialKeywordValidator.Validate(context, classDeclaration, classSymbol);

    // IOC015: Validate inheritance chain lifetime violations (SourceProductionContext)
    public static void ValidateInheritanceChainLifetimesForSourceProduction(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        string implicitLifetime,
        DiagnosticConfiguration diagnosticConfig)
    {
        LifetimeDependencyValidator
            .ValidateInheritanceChainLifetimesForSourceProduction(context, classDeclaration, classSymbol,
                serviceLifetimes, allImplementations, implicitLifetime, diagnosticConfig);
    }

    // IOC012/IOC013: Validate service lifetime dependencies
    public static void ValidateLifetimeDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig,
        INamedTypeSymbol classSymbol,
        string implicitLifetime)
    {
        LifetimeDependencyValidator
            .ValidateLifetimeDependencies(context, classDeclaration, hierarchyDependencies, serviceLifetimes,
                allRegisteredServices, allImplementations, diagnosticConfig, classSymbol, implicitLifetime);
    }

    internal static void ValidateIEnumerableLifetimes(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        string innerType,
        string dependencyTypeName,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        string implicitLifetime)
    {
        var foundImplementations = false;
        var processedImplementations = new HashSet<string>();
        if (allImplementations.TryGetValue(innerType, out var directImplementations))
        {
            foundImplementations = true;
            foreach (var implementation in directImplementations)
            {
                if (!processedImplementations.Add(implementation.ToDisplayString())) continue;
                var implementationLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation,
                    implicitLifetime);
                if (implementationLifetime == null) continue;
                if (serviceLifetime == "Singleton" && implementationLifetime == "Scoped")
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                        classDeclaration.GetLocation(), classSymbol.Name,
                        $"{dependencyTypeName} -> {implementation.Name}");
                    context.ReportDiagnostic(diagnostic);
                }
                else if (serviceLifetime == "Singleton" && implementationLifetime == "Transient")
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                        classDeclaration.GetLocation(), classSymbol.Name,
                        $"{dependencyTypeName} -> {implementation.Name}");
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        if (innerType.Contains('<') && innerType.Contains('>'))
        {
            var baseGenericType = TypeHelpers.ExtractBaseGenericInterface(innerType);
            if (baseGenericType != null &&
                allImplementations.TryGetValue(baseGenericType, out var genericImplementations))
            {
                foundImplementations = true;
                foreach (var implementation in genericImplementations)
                {
                    if (!processedImplementations.Add(implementation.ToDisplayString())) continue;
                    var implementationLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation,
                        implicitLifetime);
                    if (implementationLifetime == null) continue;
                    if (serviceLifetime == "Singleton" && implementationLifetime == "Scoped")
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                            classDeclaration.GetLocation(), classSymbol.Name,
                            $"{dependencyTypeName} -> {implementation.Name}");
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (serviceLifetime == "Singleton" && implementationLifetime == "Transient")
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                            classDeclaration.GetLocation(), classSymbol.Name,
                            $"{dependencyTypeName} -> {implementation.Name}");
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        if (!foundImplementations)
            foreach (var kvp in allImplementations)
            {
                var implementations = kvp.Value;
                foreach (var implementation in implementations)
                {
                    if (!processedImplementations.Add(implementation.ToDisplayString())) continue;
                    var implementedInterfaces = implementation.AllInterfaces.Select(i => i.ToDisplayString());
                    if (!implementedInterfaces.Contains(innerType)) continue;
                    var implementationLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation,
                        implicitLifetime);
                    if (implementationLifetime == null) continue;
                    if (serviceLifetime == "Singleton" && implementationLifetime == "Scoped")
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                            classDeclaration.GetLocation(), classSymbol.Name,
                            $"{dependencyTypeName} -> {implementation.Name}");
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (serviceLifetime == "Singleton" && implementationLifetime == "Transient")
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                            classDeclaration.GetLocation(), classSymbol.Name,
                            $"{dependencyTypeName} -> {implementation.Name}");
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
    }

    private static List<ITypeSymbol> GetDependsOnTypeSymbolsFromInheritanceChain(INamedTypeSymbol classSymbol)
    {
        var types = new List<ITypeSymbol>();
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var attr in currentType.GetAttributes())
                if (attr.AttributeClass?.Name == "DependsOnAttribute" &&
                    attr.AttributeClass?.TypeArguments != null)
                    foreach (var typeArg in attr.AttributeClass.TypeArguments)
                        types.Add(typeArg);
            currentType = currentType.BaseType;
        }

        return types;
    }

    // Helpers (scoped to diagnostics rules)
    private struct ConfigurationKeyValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }
}
