namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

using Utilities;

internal static class DependencySetSuggestionValidator
{
    public static void Suggest(SourceProductionContext context,
        ImmutableArray<ServiceClassInfo> services)
    {
        if (services.IsDefaultOrEmpty) return;

        SuggestExtractedSets(context, services);
        SuggestNearMatches(context, services);
        SuggestSharedBaseSets(context, services);
        SuggestPromoteInheritedSetsToBase(context, services);
        SuggestPromoteRegisterAsToBase(context, services);
    }

    private static void SuggestExtractedSets(SourceProductionContext context,
        ImmutableArray<ServiceClassInfo> services)
    {
        var clusters = new Dictionary<string, List<ServiceClassInfo>>();

        foreach (var service in services)
        {
            if (service.SemanticModel is null || service.ClassDeclaration is null) continue;
            var deps = DependencyAnalyzer.GetInheritanceHierarchyDependenciesForDiagnostics(service.ClassSymbol,
                service.SemanticModel);
            var depTypes = deps.AllDependencies
                .Select(GetDependencyKey)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (depTypes.Count < 3) continue;
            var key = string.Join("|", depTypes);
            if (!clusters.ContainsKey(key))
                clusters[key] = new List<ServiceClassInfo>();
            clusters[key].Add(service);
        }

        foreach (var cluster in clusters.Where(c => c.Value.Count >= 2))
        {
            var depsText = string.Join(", ", cluster.Key.Split('|'));
            var suggestedName = "SharedDependencySet";
            foreach (var svc in cluster.Value)
            {
                var location = svc.ClassDeclaration!.Identifier.GetLocation();
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.DependencySetExtractionSuggestion,
                    location,
                    depsText,
                    suggestedName));
            }
        }
    }

    private static void SuggestNearMatches(SourceProductionContext context,
        ImmutableArray<ServiceClassInfo> services)
    {
        var compilation = services.Select(s => s.SemanticModel)
            .FirstOrDefault(sm => sm != null)?.Compilation;
        if (compilation == null) return;

        var dependencySets = DiscoverDependencySets(compilation);
        if (dependencySets.Count == 0) return;

        foreach (var service in services)
        {
            if (service.SemanticModel is null || service.ClassDeclaration is null) continue;
            var deps = DependencyAnalyzer.GetInheritanceHierarchyDependenciesForDiagnostics(service.ClassSymbol,
                service.SemanticModel);
            var depTypes = new HashSet<string>(deps.AllDependencies
                .Select(GetDependencyKey)
                .Distinct());

            foreach (var set in dependencySets)
            {
                if (set.Members.Count < 2) continue;
                var overlap = set.Members.Count(m => depTypes.Contains(m));
                if (overlap >= Math.Max(1, set.Members.Count - 1) && overlap < set.Members.Count)
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.DependencySetNearMatchSuggestion,
                        service.ClassDeclaration!.Identifier.GetLocation(),
                        service.ClassSymbol.Name,
                        set.Symbol.Name,
                        overlap,
                        set.Members.Count));
            }
        }
    }

    private static void SuggestSharedBaseSets(SourceProductionContext context,
        ImmutableArray<ServiceClassInfo> services)
    {
        var groups = services
            .Select(s => new { Service = s, Base = s.ClassSymbol.BaseType as INamedTypeSymbol })
            .Where(x => x.Service.SemanticModel != null && x.Base != null &&
                        x.Base.SpecialType != SpecialType.System_Object)
            .GroupBy(x => x.Base!, SymbolEqualityComparer.Default);

        foreach (var group in groups)
        {
            var depSets = new List<HashSet<string>>();
            foreach (var svc in group)
            {
                var deps = DependencyAnalyzer.GetInheritanceHierarchyDependenciesForDiagnostics(svc.Service.ClassSymbol,
                    svc.Service.SemanticModel!);
                var depTypes = new HashSet<string>(deps.AllDependencies
                    .Select(GetDependencyKey)
                    .Distinct());
                depSets.Add(depTypes);
            }

            if (depSets.Count < 2) continue; // need at least two children to suggest moving up
            var shared = depSets.Aggregate((left,
                right) => new HashSet<string>(left.Intersect(right)));
            if (shared.Count < 1) continue; // even a single shared dependency is enough to suggest extraction

            var depsText = string.Join(", ", shared.OrderBy(x => x));

            var firstService = group.FirstOrDefault();
            var classDeclaration = firstService?.Service.ClassDeclaration;
            var location = classDeclaration?.Identifier.GetLocation();

            location ??= group.Key?.Locations.FirstOrDefault();
            if (location == null || group.Key == null) continue;

            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.DependencySetSharedBaseSuggestion,
                location,
                group.Key.Name,
                depsText));
        }
    }

    private static void SuggestPromoteInheritedSetsToBase(SourceProductionContext context,
        ImmutableArray<ServiceClassInfo> services)
    {
        var groups = services
            .Select(s => new { Service = s, Base = s.ClassSymbol.BaseType as INamedTypeSymbol })
            .Where(x => x.Service.SemanticModel != null && x.Base != null &&
                        x.Base.SpecialType != SpecialType.System_Object)
            .GroupBy(x => x.Base!, SymbolEqualityComparer.Default);

        foreach (var group in groups)
        {
            var baseType = group.Key as INamedTypeSymbol;
            if (baseType == null) continue;
            if (DependencySetUtilities.IsDependencySet(baseType)) continue;
            var baseSetTypes = GetDependencySetTypes(baseType).ToList();
            var baseAlreadyHasSets = baseSetTypes.Any();

            var setOccurrences = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
            foreach (var svc in group.Select(g => g.Service))
            {
                foreach (var set in GetDependencySetTypes(svc.ClassSymbol))
                {
                    setOccurrences[set] = setOccurrences.TryGetValue(set, out var count) ? count + 1 : 1;
                }
            }

            if (setOccurrences.Count == 0) continue;

            foreach (var kvp in setOccurrences)
            {
                if (kvp.Value < 2) continue; // need at least two derived classes using the same set
                if (baseAlreadyHasSets && baseSetTypes.Any(set => SymbolEqualityComparer.Default.Equals(set, kvp.Key)))
                    continue;

                var baseLocation = baseType.Locations.FirstOrDefault(loc => loc.IsInSource);
                if (baseLocation == null) continue;

                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.DependencySetBaseSuggestion,
                    baseLocation,
                    baseType.Name,
                    kvp.Key.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void SuggestPromoteRegisterAsToBase(SourceProductionContext context,
        ImmutableArray<ServiceClassInfo> services)
    {
        var groups = services
            .Select(s => new { Service = s, Base = s.ClassSymbol.BaseType as INamedTypeSymbol })
            .Where(x => x.Service.SemanticModel != null && x.Base != null &&
                        x.Base.SpecialType != SpecialType.System_Object)
            .GroupBy(x => x.Base!, SymbolEqualityComparer.Default);

        foreach (var group in groups)
        {
            var baseType = group.Key as INamedTypeSymbol;
            if (baseType == null) continue;

            // Skip if base already has RegisterAs
            if (GetRegisterAsInterfaces(baseType).Any()) continue;

            var derivedInterfaces = new List<HashSet<INamedTypeSymbol>>(group.Count());
            foreach (var svc in group.Select(g => g.Service))
            {
                var set = new HashSet<INamedTypeSymbol>(GetRegisterAsInterfaces(svc.ClassSymbol),
                    SymbolEqualityComparer.Default);
                if (set.Count == 0) continue;
                derivedInterfaces.Add(set);
            }

            if (derivedInterfaces.Count < 2) continue; // need repetition across at least two

            // Require all derived with RegisterAs to have identical sets
            var first = derivedInterfaces.First();
            if (derivedInterfaces.Any(set => !set.SetEquals(first))) continue;

            var baseLocation = baseType.Locations.FirstOrDefault(loc => loc.IsInSource);
            if (baseLocation == null) continue;

            var formatted = string.Join(", ", first
                .Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                .OrderBy(n => n, StringComparer.Ordinal));

            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RegisterAsBaseSuggestion,
                baseLocation,
                baseType.Name,
                formatted);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetDependencySetTypes(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsDependsOnAttribute(attribute)) continue;
            foreach (var arg in attribute.AttributeClass?.TypeArguments ?? ImmutableArray<ITypeSymbol>.Empty)
            {
                if (arg is INamedTypeSymbol named && DependencySetUtilities.IsDependencySet(named))
                    yield return named;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetRegisterAsInterfaces(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!AttributeTypeChecker.IsRegisterAsAttribute(attribute))
                continue;
            foreach (var arg in attribute.AttributeClass.TypeArguments)
            {
                if (arg is INamedTypeSymbol named && named.TypeKind == TypeKind.Interface)
                    yield return named;
            }
        }
    }

    private static bool IsDependsOnAttribute(AttributeData attribute)
        => attribute.AttributeClass?.Name?.StartsWith("DependsOn", StringComparison.Ordinal) == true;

    private static List<(INamedTypeSymbol Symbol, List<string> Members)> DiscoverDependencySets(Compilation compilation)
    {
        var results = new List<(INamedTypeSymbol Symbol, List<string> Members)>();
        var sets = new List<INamedTypeSymbol>();
        var queue = new Queue<INamespaceSymbol>();
        queue.Enqueue(compilation.Assembly.GlobalNamespace);

        while (queue.Count > 0)
        {
            var ns = queue.Dequeue();
            foreach (var nested in ns.GetNamespaceMembers()) queue.Enqueue(nested);

            foreach (var type in ns.GetTypeMembers())
                if (DependencySetUtilities.IsDependencySet(type))
                    sets.Add(type);
        }

        foreach (var set in sets)
        {
            var syntaxRef = set.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null) continue;

            var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);
            var content = DependencySetExpander.ExpandForType(set, semanticModel, null, null);
            var members = content.Dependencies.Select(d => GetDependencyKey((d.ServiceType, d.FieldName,
                DependencySource.DependsOn))).Distinct().ToList();
            results.Add((set, members));
        }

        return results;
    }

    private static string GetDependencyKey((ITypeSymbol ServiceType, string FieldName, DependencySource Source) dep)
    {
        var typeName = dep.ServiceType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var fieldName = dep.FieldName ?? string.Empty;
        return dep.Source == DependencySource.ConfigurationInjection
            ? $"config:{fieldName}:{typeName}"
            : typeName;
    }
}
