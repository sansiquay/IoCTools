namespace IoCTools.Generator.Utilities;

internal static class DependencyLifetimeResolver
{
    internal static (string? lifetime, string? implementationName)
        GetDependencyLifetimeWithGenericSupportAndImplementationName(
            ITypeSymbol dependencyType,
            Dictionary<string, string> serviceLifetimes,
            HashSet<string> allRegisteredServices,
            Dictionary<string, List<INamedTypeSymbol>>? allImplementations,
            string implicitLifetime)
    {
        var dependencyTypeName = dependencyType.ToDisplayString();
        if (serviceLifetimes.TryGetValue(dependencyTypeName, out var lifetime)) return (lifetime, null);

        // Direct lookup: find implementation by exact type name match
        var directResult = ExtractDirectLookup(dependencyTypeName, allImplementations, implicitLifetime);
        if (directResult.lifetime != null) return directResult;

        // Generic type lookup: handle constructed and open generic types
        if (dependencyType is INamedTypeSymbol namedType && namedType.IsGenericType && !namedType.IsUnboundGenericType)
        {
            var constructedResult = ExtractConstructedGenericLookup(namedType, serviceLifetimes, allRegisteredServices);
            if (constructedResult.lifetime != null) return constructedResult;

            var genericResult = ExtractGenericImplementationsLookup(
                namedType, serviceLifetimes, allImplementations, implicitLifetime);
            if (genericResult.lifetime != null) return genericResult;
        }

        return (null, null);
    }

    /// <summary>
    /// Extracts lifetime by direct type name lookup across all implementations.
    /// </summary>
    private static (string? lifetime, string? implementationName) ExtractDirectLookup(
        string dependencyTypeName,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations,
        string implicitLifetime)
    {
        if (allImplementations == null) return (null, null);

        foreach (var kvp in allImplementations)
            foreach (var implementation in kvp.Value)
            {
                var implTypeName = implementation.ToDisplayString();
                if (implTypeName == dependencyTypeName)
                {
                    var implLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation, implicitLifetime);
                    if (implLifetime != null) return (implLifetime, implementation.Name);
                }
            }

        return (null, null);
    }

    /// <summary>
    /// Extracts lifetime for constructed generic types using open generic lookup.
    /// </summary>
    private static (string? lifetime, string? implementationName) ExtractConstructedGenericLookup(
        INamedTypeSymbol namedType,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices)
    {
        // Use Roslyn API to get the open generic type definition
        var openGenericType = namedType.ConstructedFrom.ToDisplayString();
        if (serviceLifetimes.TryGetValue(openGenericType, out var openLifetime))
            return (openLifetime, null);

        var genericTypeName = namedType.Name;
        var typeParameterCount = namedType.TypeArguments.Length;

        // Find matching open generic registrations by name and arity
        foreach (var registeredService in allRegisteredServices)
        {
            if (IsMatchingOpenGenericByNameAndArity(genericTypeName, typeParameterCount, registeredService))
                if (serviceLifetimes.TryGetValue(registeredService, out var matchingLifetime))
                    return (matchingLifetime, null);
        }

        return (null, null);
    }

    /// <summary>
    /// Extracts lifetime from generic implementations by matching interfaces and open generics.
    /// </summary>
    private static (string? lifetime, string? implementationName) ExtractGenericImplementationsLookup(
        INamedTypeSymbol namedType,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations,
        string implicitLifetime)
    {
        if (allImplementations == null) return (null, null);

        // Check if dependency type matches any registered interface by comparing constructed from
        var interfaceResult = ExtractMatchingInterfaceLookup(namedType, serviceLifetimes, allImplementations, implicitLifetime);
        if (interfaceResult.lifetime != null) return interfaceResult;

        // Check all interfaces of implementations for a match
        var allInterfacesResult = ExtractAllInterfacesLookup(namedType, serviceLifetimes, allImplementations, implicitLifetime);
        if (allInterfacesResult.lifetime != null) return allInterfacesResult;

        // Check if implementations match the open generic
        var openGenericResult = ExtractOpenGenericLookup(namedType, serviceLifetimes, allImplementations, implicitLifetime);
        if (openGenericResult.lifetime != null) return openGenericResult;

        return (null, null);
    }

    /// <summary>
    /// Extracts lifetime by matching dependency type to registered interfaces.
    /// </summary>
    private static (string? lifetime, string? implementationName) ExtractMatchingInterfaceLookup(
        INamedTypeSymbol namedType,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        string implicitLifetime)
    {
        foreach (var kvp in allImplementations)
        {
            var interfaceKey = kvp.Key;
            var implementations = kvp.Value;
            if (IsMatchingGenericInterfaceBySymbol(namedType, interfaceKey, allImplementations))
                foreach (var impl in implementations)
                {
                    var implTypeName = impl.ToDisplayString();
                    if (serviceLifetimes.TryGetValue(implTypeName, out var implLifetime))
                        return (implLifetime, TypeNameUtilities.FormatTypeNameForDiagnostic(impl));
                    var symbolLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(impl, implicitLifetime);
                    if (symbolLifetime != null)
                        return (symbolLifetime, TypeNameUtilities.FormatTypeNameForDiagnostic(impl));
                }
        }

        return (null, null);
    }

    /// <summary>
    /// Extracts lifetime by checking all implemented interfaces of each implementation.
    /// </summary>
    private static (string? lifetime, string? implementationName) ExtractAllInterfacesLookup(
        INamedTypeSymbol namedType,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        string implicitLifetime)
    {
        foreach (var kvp in allImplementations)
        {
            var implementations = kvp.Value;
            foreach (var impl in implementations)
                foreach (var implementedInterface in impl.AllInterfaces)
                {
                    if (IsMatchingGenericInterfaceBySymbol(namedType, implementedInterface.ToDisplayString(), null))
                    {
                        var implTypeName = impl.ToDisplayString();
                        if (serviceLifetimes.TryGetValue(implTypeName, out var implLifetime))
                            return (implLifetime, TypeNameUtilities.FormatTypeNameForDiagnostic(impl));
                        var symbolLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(impl, implicitLifetime);
                        if (symbolLifetime != null)
                            return (symbolLifetime, TypeNameUtilities.FormatTypeNameForDiagnostic(impl));
                    }
                }
        }

        return (null, null);
    }

    /// <summary>
    /// Extracts lifetime by matching implementations to open generic definitions.
    /// </summary>
    private static (string? lifetime, string? implementationName) ExtractOpenGenericLookup(
        INamedTypeSymbol namedType,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        string implicitLifetime)
    {
        var genericTypeName = namedType.Name;
        var typeParameterCount = namedType.TypeArguments.Length;

        foreach (var kvp in allImplementations)
        {
            var interfaceName = kvp.Key;
            var interfaceImplementations = kvp.Value;
            if (IsMatchingOpenGenericByNameAndArity(genericTypeName, typeParameterCount, interfaceName))
                foreach (var impl in interfaceImplementations)
                {
                    var implTypeName = impl.ToDisplayString();
                    if (serviceLifetimes.TryGetValue(implTypeName, out var implLifetime))
                        return (implLifetime, TypeNameUtilities.FormatTypeNameForDiagnostic(impl));
                    var symbolLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(impl, implicitLifetime);
                    if (symbolLifetime != null)
                        return (symbolLifetime, TypeNameUtilities.FormatTypeNameForDiagnostic(impl));
                }
        }

        return (null, null);
    }

    private static bool IsMatchingOpenGenericByNameAndArity(string genericTypeName, int typeParameterCount, string registeredService)
    {
        // Check if registered service starts with the generic type name followed by '<'
        if (!registeredService.StartsWith(genericTypeName + "<", StringComparison.Ordinal) &&
            !registeredService.Contains("." + genericTypeName + "<"))
            return false;

        // Extract the part after the last '.' to handle namespaced types
        var lastDotIndex = registeredService.LastIndexOf('.');
        var localName = lastDotIndex >= 0 ? registeredService.Substring(lastDotIndex + 1) : registeredService;

        if (!localName.StartsWith(genericTypeName + "<"))
            return false;

        // Count type parameters by counting commas + 1
        var angleStart = registeredService.IndexOf('<');
        var angleEnd = registeredService.LastIndexOf('>');
        if (angleStart < 0 || angleEnd < 0 || angleEnd < angleStart)
            return false;

        var typeParamSection = registeredService.Substring(angleStart + 1, angleEnd - angleStart - 1);
        var paramCount = string.IsNullOrWhiteSpace(typeParamSection) ? 0 : typeParamSection.Split(',').Length;

        return paramCount == typeParameterCount;
    }

    private static bool IsMatchingGenericInterfaceBySymbol(
        INamedTypeSymbol dependencyType,
        string interfaceKey,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations)
    {
        // Check if interfaceKey is also a generic type
        if (!interfaceKey.Contains('<') || !interfaceKey.Contains('>'))
            return false;

        // Extract base name and count parameters from interfaceKey
        var angleStart = interfaceKey.IndexOf('<');
        var interfaceBaseName = interfaceKey.Substring(0, angleStart);
        var angleEnd = interfaceKey.LastIndexOf('>');
        var typeParamSection = interfaceKey.Substring(angleStart + 1, angleEnd - angleStart - 1);
        var interfaceParamCount = string.IsNullOrWhiteSpace(typeParamSection) ? 0 : typeParamSection.Split(',').Length;

        // Compare with dependency type
        var dependencyBaseName = dependencyType.ConstructedFrom.Name;
        var dependencyParamCount = dependencyType.TypeArguments.Length;

        return dependencyBaseName == interfaceBaseName && dependencyParamCount == interfaceParamCount;
    }

    internal static string? GetDependencyLifetimeForSourceProduction(ITypeSymbol dependencyType,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        string implicitLifetime)
    {
        var dependencyTypeName = dependencyType.ToDisplayString();
        if (serviceLifetimes.TryGetValue(dependencyTypeName, out var lifetime)) return lifetime;
        if (allImplementations.TryGetValue(dependencyTypeName, out var implementations) && implementations.Any())
        {
            var implementation = implementations.First();
            var implementationLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation,
                implicitLifetime);
            if (implementationLifetime != null) return implementationLifetime;
        }

        if (dependencyType is INamedTypeSymbol namedType && namedType.IsGenericType && !namedType.IsUnboundGenericType)
        {
            var openGenericType = namedType.ConstructedFrom.ToDisplayString();
            if (serviceLifetimes.TryGetValue(openGenericType, out var openGenericLifetime)) return openGenericLifetime;
            var namespaceName = namedType.ContainingNamespace?.ToDisplayString();
            var genericTypeName = namedType.Name;
            var typeParameterCount = namedType.TypeArguments.Length;
            foreach (var kvp in serviceLifetimes)
            {
                var serviceType = kvp.Key;
                var serviceLifetime = kvp.Value;
                if (serviceType.Contains(genericTypeName + "<") &&
                    (namespaceName == null || serviceType.StartsWith(namespaceName + ".")))
                {
                    var angleStart = serviceType.IndexOf('<');
                    var angleEnd = serviceType.LastIndexOf('>');
                    if (angleStart >= 0 && angleEnd > angleStart)
                    {
                        var typeParamSection = serviceType.Substring(angleStart + 1, angleEnd - angleStart - 1);
                        var paramCount = typeParamSection.Split(',').Length;
                        if (paramCount == typeParameterCount) return serviceLifetime;
                    }
                }
            }

            foreach (var kvp in serviceLifetimes)
            {
                var serviceType = kvp.Key;
                var serviceLifetime = kvp.Value;
                if (serviceType.StartsWith(genericTypeName + "<") || serviceType.Contains("." + genericTypeName + "<"))
                    return serviceLifetime;
            }
        }

        return null;
    }

    internal static string? FindImplementationNameForInterface(string interfaceTypeName,
        HashSet<string> allRegisteredServices)
    {
        var interfaceBaseName = TypeNameUtilities.ExtractSimpleTypeNameFromFullName(interfaceTypeName);
        foreach (var registeredService in allRegisteredServices)
        {
            var serviceBaseName = TypeNameUtilities.ExtractSimpleTypeNameFromFullName(registeredService);
            if (interfaceBaseName.StartsWith("I") && interfaceBaseName.Length > 1 &&
                serviceBaseName.EndsWith("Service") && serviceBaseName.Contains(interfaceBaseName.Substring(1)))
                return serviceBaseName;
            if (interfaceBaseName.StartsWith("I") && serviceBaseName.EndsWith("Service"))
            {
                var interfaceRoot = interfaceBaseName.Substring(1);
                if (serviceBaseName.Contains(interfaceRoot)) return serviceBaseName;
            }
        }

        return null;
    }
}
