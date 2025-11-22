namespace IoCTools.Generator.Generator.Intent;

internal static class ServiceIntentEvaluator
{
    internal static bool HasExplicitServiceIntent(
        INamedTypeSymbol classSymbol,
        bool hasInjectFields,
        bool hasInjectConfigurationFields,
        bool hasDependsOnAttribute,
        bool hasConditionalServiceAttribute,
        bool hasRegisterAsAll,
        bool hasRegisterAs,
        bool hasLifetimeAttribute,
        bool isHostedService,
        bool isPartialWithInterfaces)
    {
        if (DependencySetUtilities.IsDependencySet(classSymbol)) return false;

        // Matches the logic used by RegistrationSelector (kept centralized for clarity)
        var intent = hasLifetimeAttribute ||
                     hasConditionalServiceAttribute ||
                     hasRegisterAsAll ||
                     hasRegisterAs ||
                     isHostedService ||
                     hasDependsOnAttribute ||
                     (hasInjectFields && !hasInjectConfigurationFields);

        if (!intent && isPartialWithInterfaces && !hasLifetimeAttribute && !hasInjectConfigurationFields &&
            !hasDependsOnAttribute)
            intent = true;

        return intent;
    }
}
