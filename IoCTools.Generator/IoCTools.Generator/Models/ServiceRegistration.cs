namespace IoCTools.Generator.Models;

internal class ServiceRegistration
{
    private static readonly HashSet<string> ValidLifetimes = new(StringComparer.Ordinal)
    {
        "Scoped", "Singleton", "Transient", "BackgroundService"
    };

    public INamedTypeSymbol ClassSymbol { get; }
    public INamedTypeSymbol InterfaceSymbol { get; }
    public string Lifetime { get; }
    public bool UseSharedInstance { get; }
    public bool HasConfigurationInjection { get; }

    public ServiceRegistration(
        INamedTypeSymbol classSymbol,
        INamedTypeSymbol interfaceSymbol,
        string lifetime,
        bool useSharedInstance = false,
        bool hasConfigurationInjection = false)
    {
        ClassSymbol = classSymbol ?? throw new ArgumentNullException(nameof(classSymbol));
        InterfaceSymbol = interfaceSymbol ?? throw new ArgumentNullException(nameof(interfaceSymbol));

        if (string.IsNullOrWhiteSpace(lifetime))
            throw new ArgumentException("Lifetime cannot be null or whitespace.", nameof(lifetime));

        if (!ValidLifetimes.Contains(lifetime))
            throw new ArgumentException($"Invalid lifetime: {lifetime}. Valid values are: {string.Join(", ", ValidLifetimes)}", nameof(lifetime));

        Lifetime = lifetime;
        UseSharedInstance = useSharedInstance;
        HasConfigurationInjection = hasConfigurationInjection;
    }
}
