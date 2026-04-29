namespace IoCTools.Generator.Diagnostics;

internal static partial class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidConfigurationKey = new(
        "IOC016",
        "Invalid configuration key",
        "Configuration key '{0}' is invalid: {1}",
        "IoCTools.Configuration",
        DiagnosticSeverity.Error,
        true,
        "Provide a valid configuration key. Keys cannot be empty, whitespace-only, or contain invalid characters like double colons. Example valid keys: 'ConnectionStrings:Default', 'App:Settings:Feature'.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc016");

    public static readonly DiagnosticDescriptor UnsupportedConfigurationType = new(
        "IOC017",
        "Unsupported configuration type",
        "Type '{0}' cannot be bound from configuration: {1}",
        "IoCTools.Configuration",
        DiagnosticSeverity.Warning,
        true,
        "Use a supported configuration type: primitives (string, int, bool, double), POCOs with parameterless constructors, or collections (List<T>, Dictionary<string, T>). Prefer [DependsOnConfiguration<MyOptions>(\"MySection\")] for new code; InjectConfiguration remains compatibility-only.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc017");

    public static readonly DiagnosticDescriptor ConfigurationOnNonPartialClass = new(
        "IOC018",
        "InjectConfiguration requires partial class",
        "Class '{0}' uses [InjectConfiguration] but is not marked as partial",
        "IoCTools.Configuration",
        DiagnosticSeverity.Error,
        true,
        "Add 'partial' modifier to the class declaration to preserve compatibility, then migrate new code to [DependsOnConfiguration] or [DependsOnOptions]. [InjectConfiguration] remains compatibility-only in 1.5.0.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc018");

    public static readonly DiagnosticDescriptor ConfigurationOnStaticField = new(
        "IOC019",
        "InjectConfiguration on static field not supported",
        "Field '{0}' in class '{1}' is marked with [InjectConfiguration] but is static",
        "IoCTools.Configuration",
        DiagnosticSeverity.Warning,
        true,
        "Remove [InjectConfiguration] from static fields. Configuration injection only supports instance members, and new code should use [DependsOnConfiguration] or [DependsOnOptions] instead of InjectConfiguration.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc019");

    public static readonly DiagnosticDescriptor ConfigurationOverlap = new(
        "IOC046",
        "Overlapping configuration bindings",
        "Configuration section '{0}' is bound both as options '{1}' and as '{2}' on class '{3}'. Choose a single binding shape to avoid duplicate configuration sources.",
        "IoCTools.Configuration",
        DiagnosticSeverity.Warning,
        true,
        "Bind each configuration section exactly once. Avoid mixing options bindings with per-field configuration from the same section, and remove duplicate configuration slots across inheritance.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc046");

    public static readonly DiagnosticDescriptor MixedOptionsAndPrimitiveBindings = new(
        "IOC056",
        "Use a single configuration binding style per section",
        "Configuration section '{0}' is bound to an options type '{1}' and also to primitive configuration values on '{2}'. Prefer either the options object or direct primitives—avoid mixing both.",
        "IoCTools.Configuration",
        DiagnosticSeverity.Info,
        true,
        "Bind each configuration section in one style: either inject the options object once or inject primitives directly, but not both in the same inheritance chain.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc056");

    public static readonly DiagnosticDescriptor ConfigurationBindingMissing = new(
        "IOC057",
        "Configuration binding not found",
        "Configuration section '{0}' for options type '{1}' is not bound in this project. Add Configure<{1}>(), AddOptions<{1}>().Bind… or implement IConfigureOptions<{1}>.",
        "IoCTools.Configuration",
        DiagnosticSeverity.Warning,
        true,
        "Ensure options are bound: services.Configure<{1}>(configuration.GetSection(\"{0}\")), services.AddOptions<{1}>().BindConfiguration(\"{0}\"), or add an IConfigureOptions<{1}>/IConfigureNamedOptions<{1}> implementation.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc057");

    public static readonly DiagnosticDescriptor IConfigurationDependencyDiscouraged = new(
        "IOC079",
        "Prefer DependsOnConfiguration over IConfiguration",
        "Class '{0}' depends on IConfiguration directly. Use [DependsOnConfiguration<...>] or typed options instead of IConfiguration to keep configuration binding declarative and analyzer-friendly.",
        "IoCTools.Configuration",
        DiagnosticSeverity.Warning,
        true,
        "Depend on typed configuration via [DependsOnConfiguration<...>] or options classes; avoid raw IConfiguration where possible.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc079");

    public static readonly DiagnosticDescriptor ConfigurationCircularReference = new(
        "IOC088",
        "Configuration type has circular reference",
        "Configuration type '{0}' has a circular reference through property '{1}'. This will cause infinite recursion during configuration binding.",
        "IoCTools.Configuration",
        DiagnosticSeverity.Error,
        true,
        "Fix the circular reference by: 1) Breaking the cycle by removing the self-referencing property, 2) Using a different configuration structure (nested classes without cycles), or 3) Using IOptions<> pattern with manual configuration.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc088");

    public static readonly DiagnosticDescriptor SupportsReloadingOnPrimitiveType = new(
        "IOC089",
        "SupportsReloading is only supported for Options pattern types",
        "Field '{0}' has SupportsReloading=true but uses primitive type binding. SupportsReloading only works with Options pattern (IOptionsSnapshot<T> for complex types).",
        "IoCTools.Configuration",
        DiagnosticSeverity.Warning,
        true,
        "Remove SupportsReloading=true from primitive configuration fields. For reloadable configuration, use IOptionsSnapshot<T> with a complex options type instead.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc089");
}
