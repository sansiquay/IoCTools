using System.Collections.Generic;
using System.Text;

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

            var candidates = DependencyLifetimeResolver.GetDependencyLifetimeCandidates(
                dependency.ServiceType, serviceLifetimes, allRegisteredServices, allImplementations, implicitLifetime);
            if (candidates.Count == 0) continue;

            ReportLifetimeViolationsForCandidates(
                context,
                classDeclaration,
                classSymbol,
                serviceLifetime,
                dependencyTypeName,
                candidates,
                diagnosticConfig.LifetimeValidationSeverity,
                allRegisteredServices);
        }
    }

    /// <summary>
    /// Inspects every impl candidate. If all violate, fires the canonical IOC012/013/087.
    /// If only some violate, fires IOC110 (ambiguous). If none violate, no diagnostic.
    /// All emitted messages enumerate every impl and its lifetime so consumers do not
    /// have to guess which one DI will resolve at runtime.
    /// </summary>
    private static void ReportLifetimeViolationsForCandidates(
        SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string consumerLifetime,
        string dependencyTypeName,
        IReadOnlyList<LifetimeImplCandidate> candidates,
        DiagnosticSeverity lifetimeValidationSeverity,
        HashSet<string> allRegisteredServices)
    {
        var violations = new List<(LifetimeImplCandidate Candidate, LifetimeViolationType Violation)>();
        var nonViolating = new List<LifetimeImplCandidate>();

        foreach (var candidate in candidates)
        {
            var v = LifetimeCompatibilityChecker.GetViolationType(consumerLifetime, candidate.Lifetime);
            if (v == LifetimeViolationType.Compatible)
                nonViolating.Add(candidate);
            else
                violations.Add((candidate, v));
        }

        if (violations.Count == 0)
            return;

        var allViolate = nonViolating.Count == 0;
        var displayInterface = TypeNameUtilities.ExtractSimpleTypeNameFromFullName(dependencyTypeName);
        var location = classDeclaration.GetLocation();
        var serviceName = classSymbol.Name;

        if (!allViolate)
        {
            // Mixed — ambiguous. Fire IOC110 with every impl + its lifetime so the consumer
            // sees both the violating and the safe candidates.
            var implList = FormatImplListWithFallback(candidates, dependencyTypeName, allRegisteredServices);
            var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                DiagnosticDescriptors.AmbiguousLifetimeMultipleImpls, lifetimeValidationSeverity);
            var diagnostic = Diagnostic.Create(
                descriptor,
                location,
                consumerLifetime,
                serviceName,
                displayInterface,
                implList);
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // All-violating. Pivot the descriptor on the first violation kind. Because all violate,
        // every candidate produces the same descriptor (Singleton-vs-Scoped is uniform across all
        // candidates when each candidate is Scoped, etc.), so picking the first is correct.
        var primaryViolation = violations[0].Violation;
        // If candidates carry mixed *kinds* of violations (e.g. one Scoped and one Transient under a
        // Singleton consumer), prefer the more severe one (Scoped > Transient).
        foreach (var (_, v) in violations)
        {
            if (v == LifetimeViolationType.SingletonDependsOnScoped ||
                v == LifetimeViolationType.TransientDependsOnScoped)
            {
                primaryViolation = v;
                break;
            }
        }

        var allImplList = FormatImplListWithFallback(candidates, dependencyTypeName, allRegisteredServices);
        switch (primaryViolation)
        {
            case LifetimeViolationType.SingletonDependsOnScoped:
                {
                    var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                        DiagnosticDescriptors.SingletonDependsOnScoped, lifetimeValidationSeverity);
                    var diagnostic = Diagnostic.Create(descriptor, location, serviceName, displayInterface, allImplList);
                    context.ReportDiagnostic(diagnostic);
                    break;
                }
            case LifetimeViolationType.SingletonDependsOnTransient:
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.SingletonDependsOnTransient,
                        location, serviceName, displayInterface, allImplList);
                    context.ReportDiagnostic(diagnostic);
                    break;
                }
            case LifetimeViolationType.TransientDependsOnScoped:
                {
                    var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                        DiagnosticDescriptors.TransientDependsOnScoped, lifetimeValidationSeverity);
                    var diagnostic = Diagnostic.Create(descriptor, location, serviceName, displayInterface, allImplList);
                    context.ReportDiagnostic(diagnostic);
                    break;
                }
        }
    }

    private static string FormatImplListWithFallback(
        IReadOnlyList<LifetimeImplCandidate> candidates,
        string dependencyTypeName,
        HashSet<string> allRegisteredServices)
    {
        var sb = new StringBuilder();
        var displayInterface = TypeNameUtilities.ExtractSimpleTypeNameFromFullName(dependencyTypeName);
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            var displayName = c.ImplName;
            if (string.IsNullOrEmpty(displayName))
            {
                // Registered against the interface key directly — fall back to a synthesized name
                // so the message never reads "  • (Lifetime)".
                displayName = DependencyLifetimeResolver.FindImplementationNameForInterface(
                                  dependencyTypeName, allRegisteredServices)
                              ?? $"<registered {displayInterface}>";
            }

            sb.Append("  • ");
            sb.Append(displayName);
            sb.Append(" (");
            sb.Append(c.Lifetime);
            if (c.IsImplicit)
                sb.Append(" — implicit, no [Singleton]/[Scoped]/[Transient]");
            sb.Append(')');
            if (i < candidates.Count - 1)
                sb.Append('\n');
        }
        return sb.ToString();
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
                classDeclaration, classSymbol, dependencyTypeName, lifetimeValidationSeverity, allRegisteredServices);
        }

        if (innerType.Contains('<') && innerType.Contains('>'))
        {
            var baseGeneric = TypeHelpers.ExtractBaseGenericInterface(innerType);
            if (baseGeneric != null && allImplementations.TryGetValue(baseGeneric, out var generics))
            {
                foundImplementations = true;
                ValidateImplementationSet(generics, processed, serviceLifetime, implicitLifetime, context,
                    classDeclaration, classSymbol, dependencyTypeName, lifetimeValidationSeverity, allRegisteredServices);
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
                        classDeclaration, classSymbol, dependencyTypeName, lifetimeValidationSeverity, allRegisteredServices);
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
        DiagnosticSeverity lifetimeValidationSeverity,
        HashSet<string> allRegisteredServices)
    {
        foreach (var implementation in implementations)
        {
            if (!processed.Add(implementation.ToDisplayString())) continue;

            ValidateImplementationLifetime(implementation, serviceLifetime, implicitLifetime, context,
                classDeclaration, classSymbol, dependencyTypeName, lifetimeValidationSeverity, allRegisteredServices);
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
        DiagnosticSeverity lifetimeValidationSeverity,
        HashSet<string> allRegisteredServices)
    {
        var (implLifetime, isImplicit) =
            LifetimeUtilities.GetServiceLifetimeFromSymbolWithSource(implementation, implicitLifetime);
        if (implLifetime == null) return;

        var violationType = LifetimeCompatibilityChecker.GetViolationType(serviceLifetime, implLifetime);
        if (violationType == LifetimeViolationType.Compatible) return;

        // IEnumerable<T> resolves all impls, so each impl is reportable on its own. Render
        // a single-row list keeping the new message format consistent with the multi-impl path.
        var implName = TypeNameUtilities.FormatTypeNameForDiagnostic(implementation);
        var candidates = new List<LifetimeImplCandidate>
        {
            new(implName, implLifetime, isImplicit)
        };
        var displayInterface = TypeNameUtilities.ExtractSimpleTypeNameFromFullName(dependencyTypeName);
        var implList = FormatImplListWithFallback(candidates, dependencyTypeName, allRegisteredServices);
        var location = classDeclaration.GetLocation();
        var serviceName = classSymbol.Name;

        switch (violationType)
        {
            case LifetimeViolationType.SingletonDependsOnScoped:
                {
                    var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                        DiagnosticDescriptors.SingletonDependsOnScoped, lifetimeValidationSeverity);
                    var diagnostic = Diagnostic.Create(descriptor, location, serviceName, displayInterface, implList);
                    context.ReportDiagnostic(diagnostic);
                    break;
                }
            case LifetimeViolationType.SingletonDependsOnTransient:
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                        location, serviceName, displayInterface, implList);
                    context.ReportDiagnostic(diagnostic);
                    break;
                }
            case LifetimeViolationType.TransientDependsOnScoped:
                {
                    var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                        DiagnosticDescriptors.TransientDependsOnScoped, lifetimeValidationSeverity);
                    var diagnostic = Diagnostic.Create(descriptor, location, serviceName, displayInterface, implList);
                    context.ReportDiagnostic(diagnostic);
                    break;
                }
        }
    }
}
