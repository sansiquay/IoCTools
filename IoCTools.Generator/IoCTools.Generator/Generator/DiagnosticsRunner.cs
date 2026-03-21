namespace IoCTools.Generator.Generator;

using System;
using System.Collections.Immutable;

using Diagnostics.Validators;

using IoCTools.Generator.Utilities;

using Microsoft.CodeAnalysis.Diagnostics;

internal static class DiagnosticsRunner
{
    public static void EmitWithReferencedTypes(
        SourceProductionContext context,
        ((ImmutableArray<ServiceClassInfo> Services, ImmutableArray<INamedTypeSymbol> ReferencedTypes, Compilation
            Compilation) Input,
            AnalyzerConfigOptionsProvider ConfigOptions,
            GeneratorStyleOptions StyleOptions) payload) =>
        ValidateAllServiceDiagnosticsWithReferencedTypes(context, payload);

    internal static void ValidateAllServiceDiagnostics(SourceProductionContext context,
        ((ImmutableArray<ServiceClassInfo> Services, Compilation Compilation) Input, AnalyzerConfigOptionsProvider
            ConfigOptions) input)
    {
        try
        {
            var ((services, compilation), configOptions) = input;
            var styleOptions = GeneratorStyleOptions.From(configOptions, compilation);
            var implicitLifetime = styleOptions.DefaultImplicitLifetime;
            var diagnosticConfig = DiagnosticUtilities.GetDiagnosticConfiguration(configOptions);

            var servicesFiltered = services
                .Where(s => !TypeSkipEvaluator.ShouldSkipRegistration(s.ClassSymbol, compilation, styleOptions))
                .ToImmutableArray();
            var autoConfigOptions = CollectConfigurationOptionTypes(compilation, servicesFiltered, diagnosticConfig);

            DependencySetValidator.Validate(context, compilation);

            if (!servicesFiltered.Any())
            {
                // IOC075: Base lifetime consistency
                if (diagnosticConfig.DiagnosticsEnabled)
                {
                    var lifetimes = CollectLifetimesFromCompilation(compilation, implicitLifetime);
                    BaseLifetimeConsistencyValidator.Validate(context, compilation, lifetimes, implicitLifetime);
                    // IOC086: Manual registration suggestions
                    ManualRegistrationValidator.ValidateAllTrees(context, compilation, lifetimes, autoConfigOptions);
                }
                return;
            }

            DependencySetSuggestionValidator.Suggest(context, services);

            var allRegisteredServices = new HashSet<string>();
            var allImplementations = new Dictionary<string, List<INamedTypeSymbol>>();
            var serviceLifetimes = new Dictionary<string, string>();
            var processedClasses = new HashSet<string>(StringComparer.Ordinal);

            foreach (var serviceInfo in servicesFiltered)
            {
                var classKey = serviceInfo.ClassSymbol.ToDisplayString();
                if (!processedClasses.Add(classKey)) continue;
                if (serviceInfo.ClassDeclaration?.SyntaxTree != null && serviceInfo.SemanticModel != null)
                    DiagnosticScan.CollectServiceSymbolsOnce(serviceInfo.ClassDeclaration.SyntaxTree.GetRoot(),
                        serviceInfo.SemanticModel,
                        new List<INamedTypeSymbol>(), allRegisteredServices, allImplementations, serviceLifetimes,
                        new HashSet<string>(), implicitLifetime);
            }

            // IOC075: Base lifetime consistency
            if (diagnosticConfig.DiagnosticsEnabled)
                BaseLifetimeConsistencyValidator.Validate(context, compilation, serviceLifetimes, implicitLifetime);

            // IOC050/IOC051: manual registrations overlapping IoCTools
            if (diagnosticConfig.DiagnosticsEnabled)
                ManualRegistrationValidator.ValidateAllTrees(context, compilation, serviceLifetimes, autoConfigOptions);

            var validatedClasses = new HashSet<string>(StringComparer.Ordinal);
            foreach (var serviceInfo in services)
            {
                var classKey = serviceInfo.ClassSymbol.ToDisplayString();
                if (!validatedClasses.Add(classKey)) continue;
                if (serviceInfo.SemanticModel == null) continue;

                var hierarchyDependencies = DependencyAnalyzer.GetInheritanceHierarchyDependenciesForDiagnostics(
                    serviceInfo.ClassSymbol,
                    serviceInfo.SemanticModel,
                    context,
                    serviceInfo.ClassDeclaration,
                    allRegisteredServices,
                    allImplementations);

                if (serviceInfo.ClassDeclaration != null)
                    ValidateDependenciesComplete(context, serviceInfo.ClassDeclaration, hierarchyDependencies,
                        allRegisteredServices, allImplementations, serviceLifetimes, diagnosticConfig,
                        serviceInfo.SemanticModel, serviceInfo.ClassSymbol, implicitLifetime);
            }

            // IOC050/IOC051: manual registrations overlapping IoCTools (runs after lifetimes map is built)
            if (diagnosticConfig.DiagnosticsEnabled)
                ManualRegistrationValidator.ValidateAllTrees(context, compilation, serviceLifetimes, autoConfigOptions);

            var allServiceSymbols = services.Select(s => s.ClassSymbol).ToList();
            DiagnosticRules.ValidateCircularDependenciesComplete(context, allServiceSymbols, allRegisteredServices,
                diagnosticConfig);
            // IOC020, IOC022, IOC023, IOC024, IOC026: ConditionalServiceValidator
            if (diagnosticConfig.DiagnosticsEnabled)
                DiagnosticRules.ValidateAttributeCombinations(context, services.Select(s => s.ClassSymbol));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Report diagnostic with exception details for debugging
            GeneratorDiagnostics.Report(context, "IOC996",
                "Diagnostic validation pipeline error",
                $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

            // Re-throw to prevent downstream corruption from continuing with invalid state
            throw;
        }
        catch (OutOfMemoryException)
        {
            // Do not attempt to report diagnostics during OOM - just let process fail
            throw;
        }
        catch (StackOverflowException)
        {
            // Do not attempt to report diagnostics during stack overflow - just let process fail
            throw;
        }
    }

    internal static void ValidateDependenciesComplete(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        Dictionary<string, string> serviceLifetimes,
        DiagnosticConfiguration diagnosticConfig,
        SemanticModel semanticModel,
        INamedTypeSymbol classSymbol,
        string implicitLifetime)
    {
        if (!diagnosticConfig.DiagnosticsEnabled) return;

        var hasExternalServiceAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");
        if (hasExternalServiceAttribute) return;

        // IOC080: Classes with code-generating attributes must be partial
        DiagnosticRules.ValidateMissingPartialKeyword(context, classDeclaration, classSymbol);

        // Nullable dependencies are not allowed; encourage explicit non-null dependencies or no-op implementations.
        DiagnosticRules.ValidateNullableDependencies(context, classDeclaration, classSymbol, hierarchyDependencies);

        // Info-level suggestion: prefer params-style arguments over named MemberNames/ConfigurationKeys.
        DiagnosticRules.ValidateParamsStyleAttributes(context, classSymbol);

        try
        {
            DiagnosticRules.ValidateRedundantMemberNames(context, classSymbol);
        }
        catch
        {
            // Do not let a naming helper failure suppress other diagnostics.
        }

        // Manual user-defined constructors combined with IoCTools-managed dependencies are invalid states.
        // Report once and skip other dependency diagnostics to avoid misleading messages (e.g., unused dependencies).
        if (DiagnosticRules.ValidateManualConstructorMixing(context, classDeclaration, classSymbol)) return;

        // IOC042: External flag unnecessary when implementations exist
        DiagnosticRules.ValidateUnnecessaryExternalDependencies(context, classSymbol, allRegisteredServices,
            allImplementations);

        // IOC043: discourage IOptions-based dependencies; prefer DependsOnConfiguration
        DiagnosticRules.ValidateOptionsDependencies(context, classSymbol, hierarchyDependencies);

        // IOC044: Non-service dependency types (primitives/structs/string/arrays thereof)
        DiagnosticRules.ValidateNonServiceDependencies(context, classSymbol, hierarchyDependencies);

        // IOC045: Unsupported collection shapes
        DiagnosticRules.ValidateCollectionDependencies(context, classDeclaration, classSymbol, hierarchyDependencies);

        // IOC079: Discourage raw IConfiguration dependencies
        DiagnosticRules.ValidateIConfigurationUsage(context, classDeclaration, classSymbol, hierarchyDependencies);

        // IOC040/IOC046: configuration/dependency redundancy and overlaps
        DiagnosticRules.ValidateConfigurationRedundancy(context, classDeclaration, classSymbol, semanticModel);

        // IOC012/IOC013
        DiagnosticRules.ValidateLifetimeDependencies(context, classDeclaration, hierarchyDependencies,
            serviceLifetimes,
            allRegisteredServices, allImplementations, diagnosticConfig, classSymbol, implicitLifetime);

        // IOC040/IOC006/IOC008/IOC009
        DiagnosticRules.ValidateDependencyRedundancy(context, classDeclaration, classSymbol, hierarchyDependencies);
        DiagnosticRules.ValidateDuplicateDependsOn(context, classDeclaration, classSymbol);
        DiagnosticRules.ValidateDuplicatesWithinSingleDependsOn(context, classDeclaration, classSymbol);
        DiagnosticRules.ValidateUnnecessarySkipRegistration(context, classDeclaration, classSymbol);
        DiagnosticRules.ValidateInjectFieldPreferences(context, classDeclaration, classSymbol);
        DiagnosticRules.ValidateUnusedDependencies(context, classDeclaration, classSymbol, semanticModel,
            hierarchyDependencies);
        DiagnosticRules.ValidateManualDependencyFieldShadows(context, classDeclaration, classSymbol, semanticModel,
            hierarchyDependencies);
        DiagnosticRules.ValidateRedundantDependencyWrappers(context, classDeclaration, classSymbol, semanticModel,
            hierarchyDependencies);

        // IOC016–IOC019
        DiagnosticRules.ValidateConfigurationInjection(context, classDeclaration, classSymbol);

        // IOC032–IOC034
        DiagnosticRules.ValidateRedundantServiceConfigurations(context, classDeclaration, classSymbol);

        // IOC011
        DiagnosticRules.ValidateHostedServiceRequirements(context, classDeclaration, classSymbol);

        // IOC015
        var serviceLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(classSymbol, implicitLifetime);
        if (LifetimeCompatibilityChecker.ShouldValidateInheritanceChain(serviceLifetime))
            DiagnosticRules.ValidateInheritanceChainLifetimesForSourceProduction(context, classDeclaration, classSymbol,
                serviceLifetimes, allImplementations, implicitLifetime, diagnosticConfig);

        // IOC001/IOC002
        DiagnosticRules.ValidateMissingDependencies(context, classDeclaration, hierarchyDependencies,
            allRegisteredServices,
            allImplementations, serviceLifetimes, diagnosticConfig, classSymbol, implicitLifetime);
    }

    private static void ValidateAllServiceDiagnosticsWithReferencedTypes(SourceProductionContext context,
        ((ImmutableArray<ServiceClassInfo> Services, ImmutableArray<INamedTypeSymbol> ReferencedTypes, Compilation
            Compilation) Input,
            AnalyzerConfigOptionsProvider ConfigOptions,
            GeneratorStyleOptions StyleOptions) input)
    {
        try
        {
            var ((services, referencedTypes, compilation), configOptions, styleOptions) = input;
            var diagnosticConfig = DiagnosticUtilities.GetDiagnosticConfiguration(configOptions);
            var servicesFiltered = services
                .Where(s => !TypeSkipEvaluator.ShouldSkipRegistration(s.ClassSymbol, compilation, styleOptions))
                .ToImmutableArray();
            var autoConfigOptions = CollectConfigurationOptionTypes(compilation, servicesFiltered, diagnosticConfig);

            if (!servicesFiltered.Any())
            {
                var implicitLifetimeLocal = styleOptions.DefaultImplicitLifetime;
                // IOC075: Base lifetime consistency
                if (diagnosticConfig.DiagnosticsEnabled)
                {
                    var lifetimes = CollectLifetimesFromCompilation(compilation, implicitLifetimeLocal);
                    BaseLifetimeConsistencyValidator.Validate(context, compilation, lifetimes, implicitLifetimeLocal);
                    // IOC086: Manual registration suggestions
                    ManualRegistrationValidator.ValidateAllTrees(context, compilation, lifetimes, autoConfigOptions);
                }

                // IOC068: Suggest opt-in opportunities on types with DI-like constructors
                if (diagnosticConfig.DiagnosticsEnabled)
                {
                    var localCompilationTypes = new List<INamedTypeSymbol>();
                    DiagnosticScan.ScanNamespaceForTypes(compilation.Assembly.GlobalNamespace, localCompilationTypes);
                    foreach (var currentType in localCompilationTypes)
                    {
                        if (DependencySetUtilities.IsDependencySet(currentType)) continue;
                        if (currentType.TypeKind != TypeKind.Class) continue;
                        var syntaxRef = currentType.DeclaringSyntaxReferences.FirstOrDefault();
                        if (syntaxRef?.GetSyntax() is not TypeDeclarationSyntax typeDecl) continue;
                        var semanticModel = compilation.GetSemanticModel(typeDecl.SyntaxTree);
                        MissedOpportunityValidator.Validate(context, typeDecl, currentType, semanticModel, implicitLifetimeLocal, diagnosticConfig);
                    }
                }

                return;
            }

            DependencySetValidator.Validate(context, compilation);
            DependencySetSuggestionValidator.Suggest(context, servicesFiltered);
            DiagnosticRules.ValidateConfigurationBindings(context, compilation, servicesFiltered);

            var allRegisteredServices = new HashSet<string>();
            var allImplementations = new Dictionary<string, List<INamedTypeSymbol>>();
            var serviceLifetimes = new Dictionary<string, string>();
            var processedClasses = new HashSet<string>(StringComparer.Ordinal);
            var implicitLifetime = styleOptions.DefaultImplicitLifetime;

            // Collect from current project
            foreach (var serviceInfo in servicesFiltered)
            {
                var classKey = serviceInfo.ClassSymbol.ToDisplayString();
                if (!processedClasses.Add(classKey)) continue;
                if (serviceInfo.ClassDeclaration != null && serviceInfo.SemanticModel != null)
                    DiagnosticScan.CollectServiceSymbolsOnce(
                        serviceInfo.ClassDeclaration.SyntaxTree.GetRoot(), serviceInfo.SemanticModel,
                        new List<INamedTypeSymbol>(), allRegisteredServices, allImplementations, serviceLifetimes,
                        new HashSet<string>(), implicitLifetime);
            }

            // Scan current compilation types
            var currentCompilationTypes = new List<INamedTypeSymbol>();
            DiagnosticScan.ScanNamespaceForTypes(compilation.Assembly.GlobalNamespace, currentCompilationTypes);
            foreach (var currentType in currentCompilationTypes)
            {
                if (DependencySetUtilities.IsDependencySet(currentType)) continue;
                var typeName = currentType.ToDisplayString();
                if (!currentType.IsAbstract && currentType.TypeKind == TypeKind.Class)
                {
                    var hasConditionalServiceAttribute = currentType.GetAttributes().Any(attr =>
                        attr.AttributeClass?.ToDisplayString() ==
                        "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
                    var hasInjectFields = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(currentType);
                    var hasDependsOnAttribute = currentType.GetAttributes()
                        .Any(AttributeTypeChecker.IsDependsOnAttribute);
                    var hasRegisterAsAllAttribute = currentType.GetAttributes()
                        .Any(attr => AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute));
                    var hasRegisterAsAttribute = currentType.GetAttributes()
                        .Any(attr => AttributeTypeChecker.IsRegisterAsAttribute(attr));
                    var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(currentType);
                    var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(currentType);
                    var hasExplicitServiceIntent = hasConditionalServiceAttribute || hasRegisterAsAllAttribute ||
                                                   hasRegisterAsAttribute || hasLifetimeAttribute || isHostedService ||
                                                   hasInjectFields || hasDependsOnAttribute;
                    if (hasExplicitServiceIntent)
                    {
                        allRegisteredServices.Add(typeName);
                        var lifetime = ServiceDiscovery.GetServiceLifetimeFromAttributes(currentType, implicitLifetime);
                        serviceLifetimes[typeName] = lifetime;
                        foreach (var interfaceSymbol in currentType.Interfaces)
                            serviceLifetimes[interfaceSymbol.ToDisplayString()] = lifetime;
                    }

                    if (!allImplementations.ContainsKey(typeName))
                        allImplementations[typeName] = new List<INamedTypeSymbol>();
                    allImplementations[typeName].Add(currentType);
                }

                var syntaxRef = currentType.DeclaringSyntaxReferences.FirstOrDefault();
                // IOC068: Suggest DependsOn for types with DI-like constructors
                if (syntaxRef?.GetSyntax() is TypeDeclarationSyntax typeDecl && diagnosticConfig.DiagnosticsEnabled)
                {
                    var semanticModel = compilation.GetSemanticModel(typeDecl.SyntaxTree);
                    MissedOpportunityValidator.Validate(context, typeDecl, currentType, semanticModel, implicitLifetime, diagnosticConfig);
                }

                foreach (var interfaceType in currentType.Interfaces)
                {
                    var interfaceName = interfaceType.ToDisplayString();
                    if (!allImplementations.ContainsKey(interfaceName))
                        allImplementations[interfaceName] = new List<INamedTypeSymbol>();
                    allImplementations[interfaceName].Add(currentType);
                }

            }

            // Add referenced assembly types to implementations and registrations
            foreach (var referencedType in referencedTypes)
            {
                if (DependencySetUtilities.IsDependencySet(referencedType)) continue;
                var typeName = referencedType.ToDisplayString();
                if (!allImplementations.ContainsKey(typeName))
                    allImplementations[typeName] = new List<INamedTypeSymbol>();
                allImplementations[typeName].Add(referencedType);
                foreach (var interfaceType in referencedType.Interfaces)
                {
                    var interfaceName = interfaceType.ToDisplayString();
                    if (!allImplementations.ContainsKey(interfaceName))
                        allImplementations[interfaceName] = new List<INamedTypeSymbol>();
                    allImplementations[interfaceName].Add(referencedType);
                }

                var hasServiceRelatedAttribute = referencedType.GetAttributes().Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute" ||
                    AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.ScopedAttribute) ||
                    AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.SingletonAttribute) ||
                    AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.TransientAttribute) ||
                    AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute) ||
                    AttributeTypeChecker.IsRegisterAsAttribute(attr));
                var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(referencedType);
                if (hasServiceRelatedAttribute || isHostedService ||
                    (!referencedType.IsAbstract && referencedType.TypeKind == TypeKind.Class))
                {
                    allRegisteredServices.Add(typeName);
                    var lifetime = ServiceDiscovery.GetServiceLifetimeFromAttributes(referencedType, implicitLifetime)!;
                    serviceLifetimes[typeName] = lifetime;
                    foreach (var interfaceSymbol in referencedType.Interfaces)
                    {
                        var ifaceName = interfaceSymbol.ToDisplayString();
                        serviceLifetimes[ifaceName] = lifetime;
                    }
                }
            }

            // Suggest centralizing lifetimes when multiple derived implementations share a base without lifetimes
            if (diagnosticConfig.DiagnosticsEnabled)
                DiagnosticRules.SuggestSharedBaseLifetimes(context, allImplementations, allRegisteredServices,
                    implicitLifetime);

            foreach (var serviceInfo in services)
            {
                var classKey = serviceInfo.ClassSymbol.ToDisplayString();
                if (!processedClasses.Contains(classKey)) continue;

                if (serviceInfo.ClassDeclaration != null && serviceInfo.SemanticModel != null)
                {
                    var hierarchyDependencies = DependencyAnalyzer.GetInheritanceHierarchyDependenciesForDiagnostics(
                        serviceInfo.ClassSymbol,
                        serviceInfo.SemanticModel,
                        context,
                        serviceInfo.ClassDeclaration,
                        allRegisteredServices,
                        allImplementations);

                    ValidateDependenciesComplete(context, serviceInfo.ClassDeclaration, hierarchyDependencies,
                        allRegisteredServices, allImplementations, serviceLifetimes, diagnosticConfig,
                        serviceInfo.SemanticModel, serviceInfo.ClassSymbol, implicitLifetime);

                    // IOC068: Suggest DependsOn for types with DI-like constructors
                    if (diagnosticConfig.DiagnosticsEnabled)
                        MissedOpportunityValidator.Validate(context, serviceInfo.ClassDeclaration, serviceInfo.ClassSymbol,
                            serviceInfo.SemanticModel, implicitLifetime, diagnosticConfig);
                }
            }

            var allServiceSymbols = servicesFiltered.Select(s => s.ClassSymbol).ToList();
            DiagnosticRules.ValidateCircularDependenciesComplete(context, allServiceSymbols, allRegisteredServices,
                diagnosticConfig);
            // IOC020, IOC022, IOC023, IOC024, IOC026: ConditionalServiceValidator
            if (diagnosticConfig.DiagnosticsEnabled)
                DiagnosticRules.ValidateAttributeCombinations(context, servicesFiltered.Select(s => s.ClassSymbol));

            // IOC075: Base lifetime consistency
            if (diagnosticConfig.DiagnosticsEnabled)
                BaseLifetimeConsistencyValidator.Validate(context, compilation, serviceLifetimes, implicitLifetime);

            // IOC050/IOC051 + options duplication (cross-assembly scenario)
            if (diagnosticConfig.DiagnosticsEnabled)
                ManualRegistrationValidator.ValidateAllTrees(context, compilation, serviceLifetimes, autoConfigOptions);
        }
        catch (Exception ex)
        {
            GeneratorDiagnostics.Report(context, "IOC997", "Cross-assembly diagnostic validation error", ex.Message);
        }
    }

    private static Dictionary<string, string> CollectLifetimesFromCompilation(Compilation compilation,
        string implicitLifetime)
    {
        var lifetimes = new Dictionary<string, string>(StringComparer.Ordinal);
        var queue = new Queue<INamespaceSymbol>();
        queue.Enqueue(compilation.Assembly.GlobalNamespace);

        while (queue.Count > 0)
        {
            var ns = queue.Dequeue();
            foreach (var nestedNs in ns.GetNamespaceMembers()) queue.Enqueue(nestedNs);

            foreach (var type in ns.GetTypeMembers())
            {
                if (type.IsImplicitlyDeclared || type.IsStatic) continue;

                var (hasLifetime, _, _, _) = ServiceDiscovery.GetDirectLifetimeAttributes(type);
                if (!hasLifetime) continue;

                var lifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(type, implicitLifetime)!;
                var typeName = type.ToDisplayString();
                lifetimes[typeName] = lifetime;
                foreach (var iface in type.Interfaces)
                    lifetimes[iface.ToDisplayString()] = lifetime;
            }
        }

        return lifetimes;
    }

    private static HashSet<string> CollectConfigurationOptionTypes(
        Compilation compilation,
        ImmutableArray<ServiceClassInfo> servicesFiltered,
        Models.DiagnosticConfiguration diagnosticConfig)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var sectionNameSuffixes = diagnosticConfig.SectionNameSuffixes;

        // Pass 1: per-service (as before)
        foreach (var service in servicesFiltered)
        {
            if (service.SemanticModel == null) continue;
            var root = service.ClassDeclaration?.SyntaxTree.GetRoot();
            if (root == null) continue;
            var options = ConfigurationOptionsScanner.GetConfigurationOptionsToRegister(service.SemanticModel, root, sectionNameSuffixes);
            foreach (var opt in options)
                set.Add(Normalize(opt.OptionsType.ToDisplayString()));
        }

        // Pass 2: whole-compilation scan so cross-project/manual bindings surface
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            var options = ConfigurationOptionsScanner.GetConfigurationOptionsToRegister(semanticModel, root, sectionNameSuffixes);
            foreach (var opt in options)
                set.Add(Normalize(opt.OptionsType.ToDisplayString()));
        }

        return set;

        static string Normalize(string name) => name.StartsWith("global::", StringComparison.Ordinal)
            ? name.Substring("global::".Length)
            : name;
    }
}
