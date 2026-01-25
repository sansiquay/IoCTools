namespace IoCTools.Generator.Diagnostics;

internal static partial class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidConfigurationKey = new(
        "IOC016",
        "Invalid configuration key",
        "Configuration key '{0}' is invalid: {1}",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Provide a valid configuration key. Keys cannot be empty, whitespace-only, or contain invalid characters like double colons.");

    public static readonly DiagnosticDescriptor UnsupportedConfigurationType = new(
        "IOC017",
        "Unsupported configuration type",
        "Type '{0}' cannot be bound from configuration: {1}",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Use a supported configuration type (primitives, POCOs with parameterless constructors, collections) or provide a custom converter.");

    public static readonly DiagnosticDescriptor ConfigurationOnNonPartialClass = new(
        "IOC018",
        "InjectConfiguration requires partial class",
        "Class '{0}' uses [InjectConfiguration] but is not marked as partial",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Add 'partial' modifier to the class declaration: 'public partial class {0}' to enable configuration injection constructor generation.");

    public static readonly DiagnosticDescriptor ConfigurationOnStaticField = new(
        "IOC019",
        "InjectConfiguration on static field not supported",
        "Field '{0}' in class '{1}' is marked with [InjectConfiguration] but is static",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove [InjectConfiguration] from static fields. Configuration injection only supports instance fields.");

    public static readonly DiagnosticDescriptor ConfigurationOverlap = new(
        "IOC046",
        "Overlapping configuration bindings",
        "Configuration section '{0}' is bound both as options '{1}' and as '{2}' on class '{3}'. Choose a single binding shape to avoid duplicate configuration sources.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Bind each configuration section exactly once. Avoid mixing options bindings with per-field configuration from the same section, and remove duplicate configuration slots across inheritance.");

    public static readonly DiagnosticDescriptor MixedOptionsAndPrimitiveBindings = new(
        "IOC056",
        "Use a single configuration binding style per section",
        "Configuration section '{0}' is bound to an options type '{1}' and also to primitive configuration values on '{2}'. Prefer either the options object or direct primitives—avoid mixing both.",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "Bind each configuration section in one style: either inject the options object once or inject primitives directly, but not both in the same inheritance chain.");

    public static readonly DiagnosticDescriptor ConfigurationBindingMissing = new(
        "IOC057",
        "Configuration binding not found",
        "Configuration section '{0}' for options type '{1}' is not bound in this project. Add Configure<{1}>(), AddOptions<{1}>().Bind… or implement IConfigureOptions<{1}>.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Ensure options are bound: services.Configure<{1}>(configuration.GetSection(\"{0}\")), services.AddOptions<{1}>().BindConfiguration(\"{0}\"), or add an IConfigureOptions<{1}>/IConfigureNamedOptions<{1}> implementation.");

    public static readonly DiagnosticDescriptor IConfigurationDependencyDiscouraged = new(
        "IOC079",
        "Prefer DependsOnConfiguration over IConfiguration",
        "Class '{0}' depends on IConfiguration directly. Use [DependsOnConfiguration<...>] or typed options instead of IConfiguration to keep configuration binding declarative and analyzer-friendly.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Depend on typed configuration via [DependsOnConfiguration<...>] or options classes; avoid raw IConfiguration where possible.");

    public static readonly DiagnosticDescriptor ConfigurationCircularReference = new(
        "IOC088",
        "Configuration type has circular reference",
        "Configuration type '{0}' has a circular reference through property '{1}'. This will cause infinite recursion during configuration binding.",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Fix the circular reference by: 1) Breaking the cycle by removing the self-referencing property, 2) Using a different configuration structure (nested classes without cycles), or 3) Using IOptions<> pattern with manual configuration.");
}
