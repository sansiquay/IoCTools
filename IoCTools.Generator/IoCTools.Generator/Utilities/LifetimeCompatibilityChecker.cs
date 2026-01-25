namespace IoCTools.Generator.Utilities;

/// <summary>
///     Service lifetime types for dependency injection.
/// </summary>
public enum ServiceLifetime
{
    /// <summary>Single instance per application lifetime</summary>
    Singleton,

    /// <summary>Single instance per scope</summary>
    Scoped,

    /// <summary>New instance each time requested</summary>
    Transient
}

/// <summary>
///     Result of lifetime compatibility checking.
/// </summary>
public enum LifetimeViolationType
{
    /// <summary>Lifetimes are compatible</summary>
    Compatible,

    /// <summary>Singleton service depends on Scoped service (Error)</summary>
    SingletonDependsOnScoped,

    /// <summary>Singleton service depends on Transient service (Warning)</summary>
    SingletonDependsOnTransient
}

/// <summary>
///     Centralized utility for checking lifetime compatibility between services.
///     Consolidates scattered string comparisons and magic strings.
/// </summary>
internal static class LifetimeCompatibilityChecker
{
    /// <summary>
    ///     Gets the violation type for a consumer/dependency lifetime pair.
    /// </summary>
    /// <param name="consumerLifetime">Lifetime of the consuming service (e.g., "Singleton")</param>
    /// <param name="dependencyLifetime">Lifetime of the dependency service (e.g., "Scoped")</param>
    /// <returns>The violation type, or Compatible if lifetimes are valid</returns>
    public static LifetimeViolationType GetViolationType(string? consumerLifetime, string? dependencyLifetime)
    {
        // Null or invalid inputs are treated as compatible (defensive)
        if (string.IsNullOrEmpty(consumerLifetime) || string.IsNullOrEmpty(dependencyLifetime))
            return LifetimeViolationType.Compatible;

        // Only Singleton has restrictions
        if (consumerLifetime != "Singleton")
            return LifetimeViolationType.Compatible;

        // Singleton cannot depend on Scoped (error)
        if (dependencyLifetime == "Scoped")
            return LifetimeViolationType.SingletonDependsOnScoped;

        // Singleton depending on Transient is a warning
        if (dependencyLifetime == "Transient")
            return LifetimeViolationType.SingletonDependsOnTransient;

        return LifetimeViolationType.Compatible;
    }

    /// <summary>
    ///     Parses a string lifetime to the ServiceLifetime enum.
    /// </summary>
    /// <param name="lifetime">String lifetime (e.g., "Singleton")</param>
    /// <returns>ServiceLifetime enum value, or null if invalid</returns>
    public static ServiceLifetime? ParseServiceLifetime(string? lifetime)
    {
        return lifetime switch
        {
            "Singleton" => ServiceLifetime.Singleton,
            "Scoped" => ServiceLifetime.Scoped,
            "Transient" => ServiceLifetime.Transient,
            _ => null
        };
    }
}
