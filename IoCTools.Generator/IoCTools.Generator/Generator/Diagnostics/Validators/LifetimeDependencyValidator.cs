using System.Collections.Generic;

using IoCTools.Generator.Diagnostics;
using IoCTools.Generator.Utilities;

namespace IoCTools.Generator.Generator.Diagnostics.Validators;

internal static class LifetimeDependencyValidator
{
    internal static void ValidateInheritanceChainLifetimesForSourceProduction(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        string implicitLifetime,
        DiagnosticConfiguration diagnosticConfig)
    {
        var serviceLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(classSymbol, implicitLifetime);
        if (serviceLifetime == null) return;

        // We only care about Singleton and Transient checks here (Scoped can depend on anything)
        if (!LifetimeCompatibilityChecker.ShouldValidateInheritanceChain(serviceLifetime)) return;

        var currentType = classSymbol.BaseType;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var baseServiceLifetime =
                DependencyLifetimeResolver.GetDependencyLifetimeForSourceProduction(currentType, serviceLifetimes,
                    allImplementations, implicitLifetime);

            if (baseServiceLifetime != null)
            {
                var violationType = LifetimeCompatibilityChecker.GetViolationType(serviceLifetime, baseServiceLifetime);
                if (violationType != LifetimeViolationType.Compatible)
                {
                    // For inheritance, we stick to IOC015 but with correct message
                    var pathParts = new List<string>();
                    var pathType = classSymbol;
                    while (pathType != null && pathType.SpecialType != SpecialType.System_Object)
                    {
                        pathParts.Add(pathType.Name);
                        if (SymbolEqualityComparer.Default.Equals(pathType, currentType)) break;
                        pathType = pathType.BaseType;
                    }
                    var inheritancePath = string.Join(" -> ", pathParts);

                    var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                        DiagnosticDescriptors.InheritanceChainLifetimeValidation, diagnosticConfig.LifetimeValidationSeverity);
                    var diagnostic = Diagnostic.Create(descriptor,
                        classDeclaration.GetLocation(), classSymbol.Name, serviceLifetime, baseServiceLifetime, inheritancePath);
                    context.ReportDiagnostic(diagnostic);
                    return;
                }
            }

            var dependsOnAttributes = currentType.GetAttributes()
                .Where(attr => attr.AttributeClass?.Name == "DependsOnAttribute").ToList();
            foreach (var attr in dependsOnAttributes)
                if (attr.AttributeClass?.IsGenericType == true && attr.AttributeClass.TypeArguments.Length > 0)
                    foreach (var typeArg in attr.AttributeClass.TypeArguments)
                    {
                        var depLifetime =
                            DependencyLifetimeResolver.GetDependencyLifetimeForSourceProduction(typeArg,
                                serviceLifetimes, allImplementations, implicitLifetime);

                        if (depLifetime != null)
                        {
                            var violationType = LifetimeCompatibilityChecker.GetViolationType(serviceLifetime, depLifetime);
                            if (violationType != LifetimeViolationType.Compatible)
                            {
                                var pathParts2 = new List<string>();
                                var pathType2 = classSymbol;
                                while (pathType2 != null && pathType2.SpecialType != SpecialType.System_Object)
                                {
                                    pathParts2.Add(pathType2.Name);
                                    if (SymbolEqualityComparer.Default.Equals(pathType2, currentType)) break;
                                    pathType2 = pathType2.BaseType;
                                }
                                pathParts2.Add(typeArg.Name);
                                var inheritancePath2 = string.Join(" -> ", pathParts2);

                                var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                                    DiagnosticDescriptors.InheritanceChainLifetimeValidation, diagnosticConfig.LifetimeValidationSeverity);
                                var diagnostic = Diagnostic.Create(descriptor,
                                    classDeclaration.GetLocation(), classSymbol.Name, serviceLifetime, depLifetime, inheritancePath2);
                                context.ReportDiagnostic(diagnostic);
                                return;
                            }
                        }
                    }

            currentType = currentType.BaseType;
        }
    }

    internal static void ValidateLifetimeDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig,
        INamedTypeSymbol classSymbol,
        string implicitLifetime)
    {
        var serviceLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(classSymbol, implicitLifetime);
        if (serviceLifetime == null) return;

        foreach (var dependency in hierarchyDependencies.AllDependenciesWithExternalFlag)
        {
            if (dependency.IsExternal) continue;
            if (dependency.Source == DependencySource.ConfigurationInjection) continue;

            var dependencyTypeName = dependency.ServiceType.ToDisplayString();
            var enumerableTypeInfo = CollectionUtilities.ExtractIEnumerableFromWrappedType(dependencyTypeName);
            if (enumerableTypeInfo != null)
            {
                ValidateIEnumerableLifetimes(context, classDeclaration, classSymbol, serviceLifetime,
                    enumerableTypeInfo.InnerType, enumerableTypeInfo.FullEnumerableType, serviceLifetimes,
                    allRegisteredServices, allImplementations, implicitLifetime, diagnosticConfig.LifetimeValidationSeverity);
                continue;
            }

            // Handle array types (e.g., IProcessor<string>[])
            if (dependency.ServiceType is IArrayTypeSymbol arrayType)
            {
                var elementTypeName = arrayType.ElementType.ToDisplayString();
                ValidateIEnumerableLifetimes(context, classDeclaration, classSymbol, serviceLifetime,
                    elementTypeName, dependencyTypeName, serviceLifetimes,
                    allRegisteredServices, allImplementations, implicitLifetime, diagnosticConfig.LifetimeValidationSeverity);
                continue;
            }

            var (dependencyLifetime, implementationName) =
                DependencyLifetimeResolver.GetDependencyLifetimeWithGenericSupportAndImplementationName(
                    dependency.ServiceType, serviceLifetimes, allRegisteredServices, allImplementations, implicitLifetime);
            if (dependencyLifetime == null) continue;

            var violationType = LifetimeCompatibilityChecker.GetViolationType(serviceLifetime, dependencyLifetime);
            ReportLifetimeViolationIfNeeded(context, classDeclaration, classSymbol, violationType, dependencyTypeName,
                implementationName, diagnosticConfig.LifetimeValidationSeverity, allRegisteredServices);
        }
    }

    private static void ReportLifetimeViolationIfNeeded(
        SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        LifetimeViolationType violationType,
        string dependencyTypeName,
        string? implementationName,
        DiagnosticSeverity lifetimeValidationSeverity,
        HashSet<string> allRegisteredServices)
    {
        if (violationType == LifetimeViolationType.Compatible)
            return;

        var location = classDeclaration.GetLocation();
        var serviceName = classSymbol.Name;

        switch (violationType)
        {
            case LifetimeViolationType.SingletonDependsOnScoped:
                var scopedDescriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                    DiagnosticDescriptors.SingletonDependsOnScoped, lifetimeValidationSeverity);
                var scopedDiagnostic = Diagnostic.Create(scopedDescriptor, location, serviceName, dependencyTypeName);
                context.ReportDiagnostic(scopedDiagnostic);
                break;
            case LifetimeViolationType.SingletonDependsOnTransient:
                var displayName = implementationName ??
                                  DependencyLifetimeResolver.FindImplementationNameForInterface(dependencyTypeName,
                                      allRegisteredServices) ??
                                  TypeNameUtilities.ExtractSimpleTypeNameFromFullName(dependencyTypeName);
                var transientDiagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                    location, serviceName, displayName);
                context.ReportDiagnostic(transientDiagnostic);
                break;
            case LifetimeViolationType.TransientDependsOnScoped:
                var transientScopedDescriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                    DiagnosticDescriptors.TransientDependsOnScoped, lifetimeValidationSeverity);
                var transientScopedDiagnostic = Diagnostic.Create(transientScopedDescriptor, location, serviceName, dependencyTypeName);
                context.ReportDiagnostic(transientScopedDiagnostic);
                break;
        }
    }

    private static void ReportLifetimeViolationDiagnostics(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        LifetimeViolationType violationType,
        string dependencyTypeName,
        DiagnosticSeverity lifetimeValidationSeverity)
    {
        var location = classDeclaration.GetLocation();
        var serviceName = classSymbol.Name;

        switch (violationType)
        {
            case LifetimeViolationType.SingletonDependsOnScoped:
                var scopedDescriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                    DiagnosticDescriptors.SingletonDependsOnScoped, lifetimeValidationSeverity);
                var scopedDiagnostic = Diagnostic.Create(scopedDescriptor,
                    location, serviceName, dependencyTypeName);
                context.ReportDiagnostic(scopedDiagnostic);
                break;
            case LifetimeViolationType.SingletonDependsOnTransient:
                // IOC013 keeps its default Warning severity (not configurable)
                var transientDiagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                    location, serviceName, dependencyTypeName);
                context.ReportDiagnostic(transientDiagnostic);
                break;
            case LifetimeViolationType.TransientDependsOnScoped:
                var transientScopedDescriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                    DiagnosticDescriptors.TransientDependsOnScoped, lifetimeValidationSeverity);
                var transientScopedDiagnostic = Diagnostic.Create(transientScopedDescriptor,
                    location, serviceName, dependencyTypeName);
                context.ReportDiagnostic(transientScopedDiagnostic);
                break;
        }
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
        string implicitLifetime,
        DiagnosticSeverity lifetimeValidationSeverity)
    {
        var foundImplementations = false;
        var processed = new HashSet<string>();

        if (allImplementations.TryGetValue(innerType, out var direct))
        {
            foundImplementations = true;
            ValidateImplementationSet(direct, processed, serviceLifetime, implicitLifetime, context,
                classDeclaration, classSymbol, dependencyTypeName, lifetimeValidationSeverity);
        }

        if (innerType.Contains('<') && innerType.Contains('>'))
        {
            var baseGeneric = TypeHelpers.ExtractBaseGenericInterface(innerType);
            if (baseGeneric != null && allImplementations.TryGetValue(baseGeneric, out var generics))
            {
                foundImplementations = true;
                ValidateImplementationSet(generics, processed, serviceLifetime, implicitLifetime, context,
                    classDeclaration, classSymbol, dependencyTypeName, lifetimeValidationSeverity);
            }
        }

        if (!foundImplementations)
        {
            foreach (var kvp in allImplementations)
                foreach (var implementation in kvp.Value)
                {
                    if (!processed.Add(implementation.ToDisplayString())) continue;
                    var interfaces = implementation.AllInterfaces.Select(i => i.ToDisplayString());
                    if (!interfaces.Contains(innerType)) continue;

                    ValidateImplementationLifetime(implementation, serviceLifetime, implicitLifetime, context,
                        classDeclaration, classSymbol, dependencyTypeName, lifetimeValidationSeverity);
                }
        }
    }

    private static void ValidateImplementationSet(
        List<INamedTypeSymbol> implementations,
        HashSet<string> processed,
        string serviceLifetime,
        string implicitLifetime,
        SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string dependencyTypeName,
        DiagnosticSeverity lifetimeValidationSeverity)
    {
        foreach (var implementation in implementations)
        {
            if (!processed.Add(implementation.ToDisplayString())) continue;

            ValidateImplementationLifetime(implementation, serviceLifetime, implicitLifetime, context,
                classDeclaration, classSymbol, dependencyTypeName, lifetimeValidationSeverity);
        }
    }

    private static void ValidateImplementationLifetime(
        INamedTypeSymbol implementation,
        string serviceLifetime,
        string implicitLifetime,
        SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string dependencyTypeName,
        DiagnosticSeverity lifetimeValidationSeverity)
    {
        var implLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation, implicitLifetime);
        if (implLifetime == null) return;

        var violationType = LifetimeCompatibilityChecker.GetViolationType(serviceLifetime, implLifetime);
        if (violationType != LifetimeViolationType.Compatible)
        {
            var displayDependencyName = $"{dependencyTypeName} -> {implementation.Name}";
            ReportLifetimeViolationDiagnostics(context, classDeclaration, classSymbol, violationType,
                displayDependencyName, lifetimeValidationSeverity);
        }
    }
}
