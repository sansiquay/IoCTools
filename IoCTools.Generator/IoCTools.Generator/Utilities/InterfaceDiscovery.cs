namespace IoCTools.Generator.Utilities;

internal static class InterfaceDiscovery
{
    /// <summary>
    /// Gets all interfaces implemented by a service class, excluding System interfaces.
    /// Returns empty list on error to maintain generator resilience - interface discovery
    /// failures should not prevent compilation, but may result in missing registrations.
    /// </summary>
    internal static List<INamedTypeSymbol> GetAllInterfacesForService(INamedTypeSymbol classSymbol)
    {
        try
        {
            var allInterfaces = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            CollectAllInterfacesRecursive(classSymbol, allInterfaces);

            return allInterfaces
                .Where(i => i?.ContainingNamespace?.ToDisplayString()?.StartsWith("System", StringComparison.Ordinal) !=
                            true)
                .ToList();
        }
        catch (InvalidOperationException)
        {
            // Can occur during collection modification or invalid LINQ operations
            return new List<INamedTypeSymbol>();
        }
        catch (NullReferenceException)
        {
            // Can occur if typeSymbol or its interfaces are in an invalid state
            return new List<INamedTypeSymbol>();
        }
    }

    private static void CollectAllInterfacesRecursive(INamedTypeSymbol typeSymbol,
        HashSet<INamedTypeSymbol> allInterfaces)
    {
        if (typeSymbol == null) return;

        foreach (var interfaceSymbol in typeSymbol.Interfaces)
            if (interfaceSymbol != null && allInterfaces.Add(interfaceSymbol))
                CollectAllInterfacesRecursive(interfaceSymbol, allInterfaces);

        var baseType = typeSymbol.BaseType;
        if (baseType != null && baseType.SpecialType != SpecialType.System_Object)
            CollectAllInterfacesRecursive(baseType, allInterfaces);

        foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
            if (interfaceSymbol != null)
                allInterfaces.Add(interfaceSymbol);
    }
}
