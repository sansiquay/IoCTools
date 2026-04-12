namespace IoCTools.Generator.Diagnostics;

internal static partial class DiagnosticDescriptors
{
    [Obsolete("IOC010 has been consolidated into IOC014 to eliminate duplicate diagnostics. Use IOC014 instead.")]
    public static readonly DiagnosticDescriptor BackgroundServiceLifetimeConflict = new(
        "IOC010",
        "Background service with non-Singleton lifetime (deprecated)",
        "Background service '{0}' has lifetime attribute with '{1}' lifetime. Background services should typically be Singleton. Note: This diagnostic is deprecated - use IOC014 instead.",
        "IoCTools.Structural",
        DiagnosticSeverity.Warning,
        true,
        "This diagnostic has been deprecated. Use IOC014 for background service lifetime validation.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc010");

    public static readonly DiagnosticDescriptor BackgroundServiceNotPartial = new(
        "IOC011",
        "Background service class must be partial",
        "Background service '{0}' inherits from BackgroundService but is not marked as partial",
        "IoCTools.Structural",
        DiagnosticSeverity.Error,
        true,
        "Add 'partial' modifier to the class declaration: 'public partial class {0}' to enable dependency injection constructor generation.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc011");

    public static readonly DiagnosticDescriptor ConditionalServiceConflictingConditions = new(
        "IOC020",
        "Conditional service has conflicting conditions",
        "Conditional service '{0}' has conflicting conditions: {1}",
        "IoCTools.Structural",
        DiagnosticSeverity.Warning,
        true,
        "Ensure that Environment and NotEnvironment conditions do not overlap, and Equals and NotEquals conditions are not contradictory.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc020");

    public static readonly DiagnosticDescriptor ConditionalServiceMissingServiceAttribute = new(
        "IOC021",
        "ConditionalService attribute requires Service attribute",
        "Class '{0}' has [ConditionalService] attribute but lifetime attribute is required",
        "IoCTools.Structural",
        DiagnosticSeverity.Error,
        true,
        "Add lifetime attribute ([Scoped], [Singleton], or [Transient]) to the class to enable conditional service registration.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc021");

    public static readonly DiagnosticDescriptor ConditionalServiceEmptyConditions = new(
        "IOC022",
        "ConditionalService attribute has no conditions",
        "Class '{0}' has [ConditionalService] attribute but at least one condition is required",
        "IoCTools.Structural",
        DiagnosticSeverity.Warning,
        true,
        "Specify at least one Environment, NotEnvironment, or ConfigValue condition for conditional registration.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc022");

    public static readonly DiagnosticDescriptor ConditionalServiceConfigValueWithoutComparison = new(
        "IOC023",
        "ConfigValue specified without Equals or NotEquals",
        "Class '{0}' has ConfigValue '{1}' specified but Equals or NotEquals condition is required",
        "IoCTools.Structural",
        DiagnosticSeverity.Warning,
        true,
        "When ConfigValue is specified, provide at least one Equals or NotEquals condition for comparison.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc023");

    public static readonly DiagnosticDescriptor ConditionalServiceComparisonWithoutConfigValue = new(
        "IOC024",
        "Equals or NotEquals specified without ConfigValue",
        "Class '{0}' has Equals or NotEquals condition but ConfigValue is required",
        "IoCTools.Structural",
        DiagnosticSeverity.Warning,
        true,
        "When using Equals or NotEquals, specify the ConfigValue property to define which configuration key to check.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc024");

    public static readonly DiagnosticDescriptor ConditionalServiceEmptyConfigKey = new(
        "IOC025",
        "ConfigValue is empty or whitespace",
        "Class '{0}' has an empty or whitespace-only ConfigValue",
        "IoCTools.Structural",
        DiagnosticSeverity.Warning,
        true,
        "Provide a valid configuration key path for ConfigValue.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc025");

    public static readonly DiagnosticDescriptor ConditionalServiceMultipleAttributes = new(
        "IOC026",
        "Multiple ConditionalService attributes on same class",
        "Class '{0}' has multiple [ConditionalService] attributes which may lead to unexpected behavior",
        "IoCTools.Structural",
        DiagnosticSeverity.Warning,
        true,
        "Consider combining conditions into a single [ConditionalService] attribute or use separate classes for different conditions.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc026");

    public static readonly DiagnosticDescriptor SharedBaseMissingLifetimeSuggestion = new(
        "IOC058",
        "Apply lifetime attribute to shared base class",
        "Services deriving from '{0}' lack lifetime attributes. Add [{1}] to the base class to register all derived services in one place.",
        "IoCTools.Structural",
        DiagnosticSeverity.Info,
        true,
        "When many services share a base type, prefer a single lifetime attribute on the base to avoid duplicating [Scoped]/[Singleton]/[Transient] on every derived class.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc058");

    public static readonly DiagnosticDescriptor RedundantConditionalServiceInheritance = new(
        "IOC067",
        "ConditionalService attribute is redundant on derived class",
        "Class '{0}' repeats [ConditionalService] with the same condition as '{1}'. Remove the redundant attribute or change the condition.",
        "IoCTools.Structural",
        DiagnosticSeverity.Warning,
        true,
        "Use [ConditionalService] on the base when derived types share the same predicate; only override when the condition differs.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc067");

    public static readonly DiagnosticDescriptor ConstructorCouldUseDependsOn = new(
        "IOC068",
        "Constructor parameters could be expressed with [DependsOn] and lifetime attribute",
        "Class '{0}' has a manual constructor with injectable parameters but no IoCTools attributes. Consider adding [Scoped]/[Singleton]/[Transient] and [DependsOn<{1}>].",
        "IoCTools.Structural",
        DiagnosticSeverity.Info,
        true,
        "Classes with DI-like constructors can opt into IoCTools by adding a lifetime attribute and [DependsOn<T>] to enable generator support and diagnostics.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc068");

    public static readonly DiagnosticDescriptor ServiceAnalysisFailure = new(
        "IOC093",
        "IoCTools could not analyze service shape",
        "IoCTools could not fully analyze '{0}' because '{1}' failed. Generation was skipped for the affected output to avoid incomplete registrations or constructors.",
        "IoCTools.Structural",
        DiagnosticSeverity.Error,
        true,
        "This indicates generator analysis failed for a specific type. Fix the underlying syntax/model issue or report a bug if the code is valid.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc093");

    public static readonly DiagnosticDescriptor ServiceClassMustBePartial = new(
        "IOC080",
        "Service class must be partial",
        "Class '{0}' uses IoCTools attributes ({1}) that require code generation, but is not marked as partial",
        "IoCTools.Structural",
        DiagnosticSeverity.Error,
        true,
        "Add 'partial' modifier to the class declaration: 'public partial class {0}' to enable IoCTools code generation for constructors and fields.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc080");
}
