namespace IoCTools.Generator.Generator.Diagnostics.Validators;

internal static class DependencySetValidator
{
    public static void Validate(SourceProductionContext context,
        Compilation compilation)
    {
        var sets = CollectDependencySets(compilation);
        if (sets.Count == 0) return;

        foreach (var set in sets)
        {
            ValidateMetadataOnly(context, set);
            ValidateRegistrationIntent(context, set);
        }

        ValidateCycles(context, sets);
    }

    private static List<INamedTypeSymbol> CollectDependencySets(Compilation compilation)
    {
        var results = new List<INamedTypeSymbol>();
        var queue = new Queue<INamespaceSymbol>();
        queue.Enqueue(compilation.Assembly.GlobalNamespace);

        while (queue.Count > 0)
        {
            var ns = queue.Dequeue();
            foreach (var nested in ns.GetNamespaceMembers()) queue.Enqueue(nested);

            foreach (var type in ns.GetTypeMembers())
            {
                if (type.IsImplicitlyDeclared) continue;
                if (DependencySetUtilities.IsDependencySet(type)) results.Add(type);
                foreach (var nestedType in type.GetTypeMembers())
                    if (!nestedType.IsImplicitlyDeclared && DependencySetUtilities.IsDependencySet(nestedType))
                        results.Add(nestedType);
            }
        }

        return results;
    }

    private static void ValidateMetadataOnly(SourceProductionContext context,
        INamedTypeSymbol setSymbol)
    {
        var invalidMembers = setSymbol.GetMembers()
            .Where(m => !m.IsImplicitlyDeclared)
            .Where(m => m switch
            {
                IMethodSymbol method when method.MethodKind is not MethodKind.Constructor
                    and not MethodKind.StaticConstructor => true,
                IPropertySymbol => true,
                IFieldSymbol => true,
                IEventSymbol => true,
                INamedTypeSymbol => true,
                _ => false
            })
            .ToList();

        if (invalidMembers.Count == 0) return;

        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.DependencySetMetadataOnly,
            setSymbol.Locations.FirstOrDefault(), setSymbol.Name));
    }

    private static void ValidateRegistrationIntent(SourceProductionContext context,
        INamedTypeSymbol setSymbol)
    {
        var attrs = setSymbol.GetAttributes();
        var forbidden = attrs.FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() is "IoCTools.Abstractions.Annotations.ScopedAttribute"
                or "IoCTools.Abstractions.Annotations.SingletonAttribute"
                or "IoCTools.Abstractions.Annotations.TransientAttribute"
                or AttributeTypeChecker.RegisterAsAllAttribute
                or "IoCTools.Abstractions.Annotations.ManualServiceAttribute"
                or "IoCTools.Abstractions.Annotations.ExternalServiceAttribute" ||
            AttributeTypeChecker.IsRegisterAsAttribute(a) ||
            a.AttributeClass?.ToDisplayString() ==
            "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");

        if (forbidden == null) return;

        var source = forbidden.AttributeClass?.Name ?? "attribute";
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.DependencySetRegistrationDetected,
            forbidden.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? setSymbol.Locations.FirstOrDefault(),
            setSymbol.Name, source));
    }

    private static void ValidateCycles(SourceProductionContext context,
        List<INamedTypeSymbol> sets)
    {
        var graph = sets.ToDictionary(s => s, GetReferencedSets, SymbolEqualityComparer.Default);
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var stack = new Stack<INamedTypeSymbol>();

        foreach (var set in sets)
            Dfs(set);

        void Dfs(INamedTypeSymbol node)
        {
            if (!visited.Add(node)) return;
            stack.Push(node);

            foreach (var next in graph[node])
            {
                if (stack.Any(s => SymbolEqualityComparer.Default.Equals(s, next)))
                {
                    var cycle = string.Join(" -> ", stack.Reverse().Select(s => s.Name).Concat(new[] { next.Name }));
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.DependencySetCycleDetected,
                        node.Locations.FirstOrDefault(), node.Name, cycle));
                    continue;
                }

                Dfs(next);
            }

            stack.Pop();
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetReferencedSets(INamedTypeSymbol setSymbol)
    {
        foreach (var attr in setSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name?.StartsWith("DependsOn") != true) continue;
            if (AttributeParser.IsDependsOnConfigurationAttribute(attr)) continue;
            if (attr.AttributeClass?.TypeArguments == null) continue;

            foreach (var arg in attr.AttributeClass.TypeArguments)
                if (arg is INamedTypeSymbol named && DependencySetUtilities.IsDependencySet(named))
                    yield return named;
        }
    }
}
