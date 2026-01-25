namespace IoCTools.Generator.Generator.Diagnostics.Validators;

internal static class BaseLifetimeConsistencyValidator
{
    internal static void Validate(SourceProductionContext context,
        Compilation compilation,
        Dictionary<string, string> serviceLifetimes,
        string implicitLifetime)
    {
        var map = new Dictionary<INamedTypeSymbol, List<(INamedTypeSymbol Derived, string Lifetime)>>(SymbolEqualityComparer.Default);

        var allTypes = new List<INamedTypeSymbol>();
        DiagnosticScan.ScanNamespaceForTypes(compilation.Assembly.GlobalNamespace, allTypes);

        foreach (var type in allTypes)
        {
            if (!IsIoCToolsAware(type)) continue;

            var display = type.ToDisplayString();
            var lifetime = ServiceDiscovery.GetServiceLifetimeFromAttributes(type, implicitLifetime);

            var baseType = type.BaseType;
            while (baseType is { SpecialType: not SpecialType.System_Object })
            {
                if (!map.TryGetValue(baseType, out var list))
                {
                    list = new List<(INamedTypeSymbol, string)>();
                    map[baseType] = list;
                }

                list.Add((type, lifetime));
                baseType = baseType.BaseType;
            }
        }

        foreach (var kvp in map)
        {
            var baseType = kvp.Key;
            var derived = kvp.Value;
            if (!derived.Any()) continue;

            var distinctLifetimes = derived
                .Select(d => d.Lifetime)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (distinctLifetimes.Count <= 1) continue;

            var location = baseType.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation();
            if (location == null) continue;

            var lifetimesText = string.Join(", ", distinctLifetimes.OrderBy(x => x));
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.BaseClassLifetimeMismatch,
                location,
                baseType.Name,
                lifetimesText);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsIoCToolsAware(INamedTypeSymbol type)
    {
        var directLifetime = ServiceDiscovery.GetDirectLifetimeAttributes(type).HasAny;
        if (directLifetime) return true;

        // Use fully-qualified namespace checks to avoid matching user-defined attributes with similar names
        var hasDependsOnAttribute = type.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString().StartsWith("IoCTools.Abstractions.Annotations.DependsOnAttribute`") == true);
        var hasRegisterAsAllAttribute = type.GetAttributes().Any(attr =>
            AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute));
        var hasRegisterAsAttribute = type.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString().StartsWith("IoCTools.Abstractions.Annotations.RegisterAsAttribute`") == true);
        var hasConditionalServiceAttribute = type.GetAttributes().Any(attr =>
            AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.ConditionalServiceAttribute));
        var hasInjectFields = type.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                             "IoCTools.Abstractions.Annotations.InjectAttribute"));
        var hasInjectConfigurationFields = type.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                             "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute"));

        return hasDependsOnAttribute || hasRegisterAsAllAttribute || hasRegisterAsAttribute ||
               hasConditionalServiceAttribute || hasInjectFields || hasInjectConfigurationFields;
    }
}
