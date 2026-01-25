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

        if (allImplementations != null)
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

        if (dependencyType is INamedTypeSymbol namedType && namedType.IsGenericType && !namedType.IsUnboundGenericType)
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

            if (allImplementations != null)
            {
                // Check if dependency type matches any registered interface by comparing constructed from
                foreach (var kvp in allImplementations)
                {
                    var interfaceKey = kvp.Key;
                    var implementations = kvp.Value;
                    if (IsMatchingGenericInterfaceBySymbol(namedType, interfaceKey, allImplementations))
                        foreach (var impl in implementations)
                        {
                            var implTypeName = impl.ToDisplayString();
                            if (serviceLifetimes.TryGetValue(implTypeName, out var implLifetime))
                                return (implLifetime, TypeHelpers.FormatTypeNameForDiagnostic(impl));
                            var symbolLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(impl, implicitLifetime);
                            if (symbolLifetime != null)
                                return (symbolLifetime, TypeHelpers.FormatTypeNameForDiagnostic(impl));
                        }
                }

                // Check all interfaces of implementations for a match
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
                                    return (implLifetime, TypeHelpers.FormatTypeNameForDiagnostic(impl));
                                var symbolLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(impl, implicitLifetime);
                                if (symbolLifetime != null)
                                    return (symbolLifetime, TypeHelpers.FormatTypeNameForDiagnostic(impl));
                            }
                        }

                    // Check if implementations match the open generic
                    var interfaceName = kvp.Key;
                    var interfaceImplementations = kvp.Value;
                    if (IsMatchingOpenGenericByNameAndArity(genericTypeName, typeParameterCount, interfaceName))
                        foreach (var impl in interfaceImplementations)
                        {
                            var implTypeName = impl.ToDisplayString();
                            if (serviceLifetimes.TryGetValue(implTypeName, out var implLifetime))
                                return (implLifetime, TypeHelpers.FormatTypeNameForDiagnostic(impl));
                            var symbolLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(impl, implicitLifetime);
                            if (symbolLifetime != null)
                                return (symbolLifetime, TypeHelpers.FormatTypeNameForDiagnostic(impl));
                        }
                }
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
        var interfaceBaseName = TypeHelpers.ExtractSimpleTypeNameFromFullName(interfaceTypeName);
        foreach (var registeredService in allRegisteredServices)
        {
            var serviceBaseName = TypeHelpers.ExtractSimpleTypeNameFromFullName(registeredService);
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
