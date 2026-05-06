namespace IoCTools.Generator.Utilities;

using System;

/// <summary>
/// One implementation candidate the analyzer found for a dependency interface.
/// The diagnostic enumerates these so the message reports every impl whose lifetime
/// is shorter than the consumer's, instead of guessing one based on iteration order.
/// </summary>
internal readonly struct LifetimeImplCandidate
{
    public LifetimeImplCandidate(string implName, string lifetime, bool isImplicit)
    {
        ImplName = implName;
        Lifetime = lifetime;
        IsImplicit = isImplicit;
    }

    /// <summary>Display-friendly impl name (e.g. simple type name without namespace).</summary>
    public string ImplName { get; }

    /// <summary>Resolved lifetime string ("Singleton" / "Scoped" / "Transient").</summary>
    public string Lifetime { get; }

    /// <summary>
    /// True when the lifetime came from the implicit fallback (no [Singleton]/[Scoped]/[Transient] attribute).
    /// </summary>
    public bool IsImplicit { get; }
}

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
        var candidates = GetDependencyLifetimeCandidates(
            dependencyType, serviceLifetimes, allRegisteredServices, allImplementations, implicitLifetime);
        if (candidates.Count == 0) return (null, null);
        var first = candidates[0];
        // Backwards-compatible single-impl shape used by callers that have not migrated.
        return (first.Lifetime, string.IsNullOrEmpty(first.ImplName) ? null : first.ImplName);
    }

    /// <summary>
    /// Returns every implementation candidate the analyzer can statically associate with the dependency type,
    /// each carrying its resolved lifetime. Results are deterministically ordered by impl name (Ordinal).
    /// Callers should NOT pick "the first" — the actual DI runtime resolution depends on registration order
    /// (TryAdd vs Add, conditional factories), which is not statically predictable. Instead, callers should
    /// reason about the full set: "all impls violate" vs "some violate" vs "none violate".
    /// </summary>
    internal static IReadOnlyList<LifetimeImplCandidate> GetDependencyLifetimeCandidates(
        ITypeSymbol dependencyType,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations,
        string implicitLifetime)
    {
        var dependencyTypeName = dependencyType.ToDisplayString();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<LifetimeImplCandidate>();

        // 1) Direct lookup across allImplementations (every kvp value list, NOT first match).
        AddDirectMatches(dependencyTypeName, allImplementations, implicitLifetime, results, seen);

        // 2) Generic lookups when applicable.
        if (dependencyType is INamedTypeSymbol namedType && namedType.IsGenericType && !namedType.IsUnboundGenericType)
        {
            AddConstructedGenericMatches(namedType, serviceLifetimes, allRegisteredServices, results, seen);
            AddMatchingInterfaceMatches(namedType, serviceLifetimes, allImplementations, implicitLifetime, results, seen);
            AddAllInterfacesMatches(namedType, serviceLifetimes, allImplementations, implicitLifetime, results, seen);
            AddOpenGenericMatches(namedType, serviceLifetimes, allImplementations, implicitLifetime, results, seen);
        }

        // 3) Registered service lifetime keyed directly by interface type name. Only add this
        //    "registered against the interface" entry when no impl candidates were found, so
        //    multi-impl cases enumerate concrete impls instead of an opaque interface row.
        if (results.Count == 0 && serviceLifetimes.TryGetValue(dependencyTypeName, out var registeredLifetime))
            AddCandidate(results, seen, "", registeredLifetime, isImplicit: false);

        // Deterministic ordering: Ordinal ascending by impl name. The empty "" sorts first
        // and represents the registered-by-interface-key candidate when present.
        results.Sort(static (a, b) => string.CompareOrdinal(a.ImplName, b.ImplName));
        return results;
    }

    private static void AddCandidate(
        List<LifetimeImplCandidate> results,
        HashSet<string> seen,
        string implName,
        string? lifetime,
        bool isImplicit)
    {
        if (string.IsNullOrEmpty(lifetime)) return;
        var key = implName + "|" + lifetime;
        if (!seen.Add(key)) return;
        results.Add(new LifetimeImplCandidate(implName, lifetime!, isImplicit));
    }

    private static void AddDirectMatches(
        string dependencyTypeName,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations,
        string implicitLifetime,
        List<LifetimeImplCandidate> results,
        HashSet<string> seen)
    {
        if (allImplementations == null) return;

        // Path A: dependency type name matches the interface key directly. Every impl on the
        // value list is a candidate (this is the multi-impl case the bug was about). Only
        // include impls IoCTools actually registers — see IsRegisteredService().
        if (allImplementations.TryGetValue(dependencyTypeName, out var implsForInterface))
        {
            foreach (var implementation in implsForInterface)
            {
                if (!IsRegisteredService(implementation)) continue;
                var (implLifetime, isImplicit) =
                    LifetimeUtilities.GetServiceLifetimeFromSymbolWithSource(implementation, implicitLifetime);
                AddCandidate(results, seen,
                    TypeNameUtilities.FormatTypeNameForDiagnostic(implementation),
                    implLifetime,
                    isImplicit);
            }
        }

        // Path B: dependency type name matches an impl's display string (concrete-class injection).
        // Iterate every value list and report each impl whose name matches.
        foreach (var kvp in allImplementations)
            foreach (var implementation in kvp.Value)
            {
                if (implementation.ToDisplayString() != dependencyTypeName) continue;
                if (!IsRegisteredService(implementation)) continue;
                var (implLifetime, isImplicit) =
                    LifetimeUtilities.GetServiceLifetimeFromSymbolWithSource(implementation, implicitLifetime);
                AddCandidate(results, seen,
                    TypeNameUtilities.FormatTypeNameForDiagnostic(implementation),
                    implLifetime,
                    isImplicit);
            }
    }

    /// <summary>
    /// Returns true when IoCTools considers the type a registered service, either by an
    /// explicit lifetime / service / DependsOn / RegisterAs / Conditional / IHostedService
    /// indicator, or by being a partial class implementing an interface (the
    /// <see cref="ServiceDiscovery"/> service-inference rule).
    /// Pure POCO classes that happen to implement an interface are NOT registered services,
    /// and including them as lifetime candidates produces phantom diagnostics — that is the
    /// regression the IOC002 path was protecting against, so we replicate the same gate here.
    /// </summary>
    private static bool IsRegisteredService(INamedTypeSymbol impl)
    {
        // Explicit lifetime / inherited lifetime
        var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(impl);
        if (hasLifetimeAttribute) return true;

        // Other service-bearing attributes
        foreach (var attr in impl.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            if (name == "ConditionalServiceAttribute" ||
                name == "ExternalServiceAttribute" ||
                name == "RegisterAsAllAttribute")
                return true;
            if (AttributeTypeChecker.IsRegisterAsAttribute(attr)) return true;
            if (AttributeTypeChecker.IsDependsOnAttribute(attr)) return true;
        }

        // Inject / DependsOn fields are evidence of being a service (matches HasServiceInferenceIndicators).
        foreach (var member in impl.GetMembers())
        {
            if (member is IFieldSymbol field)
            {
                foreach (var attr in field.GetAttributes())
                {
                    var name = attr.AttributeClass?.Name;
                    if (name == "InjectAttribute" || name == "InjectConfigurationAttribute")
                        return true;
                }
            }
        }

        // Partial class implementing an interface: ServiceDiscovery treats this as a registered service.
        if (impl.Interfaces.Any())
        {
            foreach (var syntaxRef in impl.DeclaringSyntaxReferences)
            {
                if (syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDecl &&
                    typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    return true;
            }
        }

        // IHostedService / BackgroundService inheritance
        var current = impl;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (current.Interfaces.Any(i => i.ToDisplayString() == "Microsoft.Extensions.Hosting.IHostedService"))
                return true;
            if (current.ToDisplayString() == "Microsoft.Extensions.Hosting.BackgroundService")
                return true;
            current = current.BaseType;
        }

        return false;
    }

    private static void AddConstructedGenericMatches(
        INamedTypeSymbol namedType,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices,
        List<LifetimeImplCandidate> results,
        HashSet<string> seen)
    {
        var openGenericType = namedType.ConstructedFrom.ToDisplayString();
        if (serviceLifetimes.TryGetValue(openGenericType, out var openLifetime))
            AddCandidate(results, seen, "", openLifetime, isImplicit: false);

        var genericTypeName = namedType.Name;
        var typeParameterCount = namedType.TypeArguments.Length;
        foreach (var registeredService in allRegisteredServices)
        {
            if (!IsMatchingOpenGenericByNameAndArity(genericTypeName, typeParameterCount, registeredService)) continue;
            if (serviceLifetimes.TryGetValue(registeredService, out var matchingLifetime))
                AddCandidate(results, seen, "", matchingLifetime, isImplicit: false);
        }
    }

    private static void AddMatchingInterfaceMatches(
        INamedTypeSymbol namedType,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations,
        string implicitLifetime,
        List<LifetimeImplCandidate> results,
        HashSet<string> seen)
    {
        if (allImplementations == null) return;

        foreach (var kvp in allImplementations)
        {
            var interfaceKey = kvp.Key;
            if (!IsMatchingGenericInterfaceBySymbol(namedType, interfaceKey, allImplementations)) continue;

            foreach (var impl in kvp.Value)
            {
                if (!ImplementsConstructedInterface(impl, namedType)) continue;

                var implTypeName = impl.ToDisplayString();
                var implName = TypeNameUtilities.FormatTypeNameForDiagnostic(impl);

                if (serviceLifetimes.TryGetValue(implTypeName, out var registeredLifetime))
                {
                    AddCandidate(results, seen, implName, registeredLifetime, isImplicit: false);
                    continue;
                }

                if (!IsRegisteredService(impl)) continue;
                var (implLifetime, isImplicit) =
                    LifetimeUtilities.GetServiceLifetimeFromSymbolWithSource(impl, implicitLifetime);
                AddCandidate(results, seen, implName, implLifetime, isImplicit);
            }
        }
    }

    private static void AddAllInterfacesMatches(
        INamedTypeSymbol namedType,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations,
        string implicitLifetime,
        List<LifetimeImplCandidate> results,
        HashSet<string> seen)
    {
        if (allImplementations == null) return;

        foreach (var kvp in allImplementations)
            foreach (var impl in kvp.Value)
            {
                if (!ImplementsConstructedInterface(impl, namedType)) continue;

                var implTypeName = impl.ToDisplayString();
                var implName = TypeNameUtilities.FormatTypeNameForDiagnostic(impl);
                if (serviceLifetimes.TryGetValue(implTypeName, out var registeredLifetime))
                {
                    AddCandidate(results, seen, implName, registeredLifetime, isImplicit: false);
                    continue;
                }

                if (!IsRegisteredService(impl)) continue;
                var (implLifetime, isImplicit) =
                    LifetimeUtilities.GetServiceLifetimeFromSymbolWithSource(impl, implicitLifetime);
                AddCandidate(results, seen, implName, implLifetime, isImplicit);
            }
    }

    /// <summary>
    /// Returns true when <paramref name="impl"/> implements an interface compatible with
    /// <paramref name="constructedInterface"/>. This handles both:
    /// <list type="bullet">
    ///   <item>Closed-generic match — the impl directly implements the same closed generic
    ///         (e.g. <c>GetUserQueryHandler : IRequestHandler&lt;GetUserQuery, string&gt;</c>
    ///         matches a request for <c>IRequestHandler&lt;GetUserQuery, string&gt;</c>).</item>
    ///   <item>Open-generic match — the impl implements the same open generic via type
    ///         parameters (e.g. <c>ServiceFactory&lt;T&gt; : IFactory&lt;IService&lt;T&gt;&gt;</c>
    ///         matches a request for <c>IFactory&lt;IService&lt;string&gt;&gt;</c>) — recognized by
    ///         comparing <c>ConstructedFrom</c> on the open generic and confirming the impl is
    ///         itself generic (registered as open generic).</item>
    /// </list>
    /// This narrows the older name-and-arity match so that closed-generic dependencies
    /// resolve only to handlers that actually serve that closed type, but still resolves
    /// open-generic registrations correctly.
    /// </summary>
    private static bool ImplementsConstructedInterface(INamedTypeSymbol impl, INamedTypeSymbol constructedInterface)
    {
        var depOpen = constructedInterface.ConstructedFrom;
        foreach (var i in impl.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(i, constructedInterface))
                return true;

            // Open-generic registration: impl is generic and implements the same open interface.
            // Example: Repository<T> : IRepository<T> matches a request for IRepository<User>.
            if (impl.IsGenericType && i.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(i.ConstructedFrom, depOpen))
                return true;
        }
        return false;
    }

    private static void AddOpenGenericMatches(
        INamedTypeSymbol namedType,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations,
        string implicitLifetime,
        List<LifetimeImplCandidate> results,
        HashSet<string> seen)
    {
        if (allImplementations == null) return;

        var genericTypeName = namedType.Name;
        var typeParameterCount = namedType.TypeArguments.Length;

        foreach (var kvp in allImplementations)
        {
            if (!IsMatchingOpenGenericByNameAndArity(genericTypeName, typeParameterCount, kvp.Key)) continue;
            foreach (var impl in kvp.Value)
            {
                if (!ImplementsConstructedInterface(impl, namedType)) continue;

                var implTypeName = impl.ToDisplayString();
                var implName = TypeNameUtilities.FormatTypeNameForDiagnostic(impl);
                if (serviceLifetimes.TryGetValue(implTypeName, out var registeredLifetime))
                {
                    AddCandidate(results, seen, implName, registeredLifetime, isImplicit: false);
                    continue;
                }

                if (!IsRegisteredService(impl)) continue;
                var (implLifetime, isImplicit) =
                    LifetimeUtilities.GetServiceLifetimeFromSymbolWithSource(impl, implicitLifetime);
                AddCandidate(results, seen, implName, implLifetime, isImplicit);
            }
        }
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

            // Use Roslyn symbol comparison instead of string parsing for namespace-aware matching
            var openGenericSymbol = namedType.ConstructedFrom;
            var typeParameterCount = namedType.TypeArguments.Length;

            foreach (var kvp in serviceLifetimes)
            {
                // Parse the key to get its symbol for comparison
                // We need to match by open generic type with same arity
                var serviceType = kvp.Key;
                if (!serviceType.Contains('<')) continue;

                // Extract base name and arity from service type key for comparison
                var lastDotIndex = serviceType.LastIndexOf('.');
                var serviceLocalName = lastDotIndex >= 0 ? serviceType.Substring(lastDotIndex + 1) : serviceType;
                var serviceBaseName = serviceLocalName.Substring(0, serviceLocalName.IndexOf('<'));

                // Check if base names match
                if (serviceBaseName != openGenericSymbol.Name) continue;

                // Check arity by counting type parameters
                var angleStart = serviceType.IndexOf('<');
                var angleEnd = serviceType.LastIndexOf('>');
                if (angleStart >= 0 && angleEnd > angleStart)
                {
                    var typeParamSection = serviceType.Substring(angleStart + 1, angleEnd - angleStart - 1);
                    var paramCount = string.IsNullOrWhiteSpace(typeParamSection) ? 0 : typeParamSection.Split(',').Length;
                    if (paramCount == typeParameterCount) return kvp.Value;
                }
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
