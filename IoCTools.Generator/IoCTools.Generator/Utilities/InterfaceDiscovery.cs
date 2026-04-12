namespace IoCTools.Generator.Utilities;

internal static class InterfaceDiscovery
{
    /// <summary>
    /// Gets all interfaces implemented by a service class, excluding System interfaces.
    /// The provider is injectable for tests; the method itself does not swallow exceptions.
    /// </summary>
    internal static List<INamedTypeSymbol> GetAllInterfacesForService(
        INamedTypeSymbol classSymbol,
        Func<INamedTypeSymbol, IEnumerable<INamedTypeSymbol>>? interfaceProvider = null)
    {
        interfaceProvider ??= static symbol => symbol.AllInterfaces;

        var uniqueInterfaces = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var interfaceSymbol in interfaceProvider(classSymbol).OfType<INamedTypeSymbol>())
            if (interfaceSymbol != null &&
                interfaceSymbol.ContainingNamespace?.ToDisplayString()?.StartsWith("System", StringComparison.Ordinal) !=
                true)
                uniqueInterfaces.Add(interfaceSymbol);

        return uniqueInterfaces.ToList();
    }
}
