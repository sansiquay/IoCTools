namespace IoCTools.Generator.Utilities;

internal static class LifetimeUtilities
{
    internal static string? GetServiceLifetimeFromSymbol(INamedTypeSymbol classSymbol,
        string implicitLifetime)
    {
        var (lifetime, _) = GetServiceLifetimeFromSymbolWithSource(classSymbol, implicitLifetime);
        return lifetime;
    }

    /// <summary>
    /// Returns the resolved service lifetime for a symbol along with a flag indicating whether
    /// the lifetime came from an explicit attribute ([Singleton]/[Scoped]/[Transient]) or from
    /// the implicit fallback (no lifetime attribute).
    /// IsImplicit is <c>true</c> when the lifetime equals <paramref name="implicitLifetime"/>
    /// because the symbol carried no lifetime attribute (and no [ConditionalService] lifetime).
    /// </summary>
    internal static (string? Lifetime, bool IsImplicit) GetServiceLifetimeFromSymbolWithSource(
        INamedTypeSymbol classSymbol,
        string implicitLifetime)
    {
        var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
        var conditionalAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
        if (hasLifetimeAttribute)
            return (ServiceDiscovery.GetServiceLifetimeFromAttributes(classSymbol, implicitLifetime), false);
        if (conditionalAttribute?.ConstructorArguments.Length > 1)
        {
            var lifetimeValue = conditionalAttribute.ConstructorArguments[1].Value;
            if (lifetimeValue != null)
            {
                var lifetimeInt = (int)lifetimeValue;
                return lifetimeInt switch
                {
                    0 => (implicitLifetime, true),
                    1 => ("Transient", false),
                    2 => ("Singleton", false),
                    _ => (implicitLifetime, true)
                };
            }
        }

        return (implicitLifetime, true);
    }
}
