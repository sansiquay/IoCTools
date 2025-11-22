namespace IoCTools.Generator.Diagnostics;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor NoImplementationFound = new(
        "IOC001",
        "No implementation found for interface",
        "Service '{0}' depends on '{1}' but no implementation of this interface exists in the project",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Fix options: 1) Create a class implementing '{1}' with lifetime attribute ([Scoped], [Singleton], or [Transient]), 2) Add [ExternalService] attribute to this class if dependency is provided externally, or 3) Register manually with services.AddScoped<{1}, Implementation>() in Program.cs.");

    public static readonly DiagnosticDescriptor ImplementationNotRegistered = new(
        "IOC002",
        "Implementation exists but not registered",
        "Service '{0}' depends on '{1}' - implementation exists but lacks lifetime attribute ([Scoped], [Singleton], or [Transient])",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Fix options: 1) Add lifetime attribute ([Scoped], [Singleton], or [Transient]) to the implementation of '{1}', 2) Add [ExternalService] attribute to this class if dependency is provided externally, or 3) Register manually with services.AddScoped<{1}, Implementation>() in Program.cs.");

    public static readonly DiagnosticDescriptor CircularDependency = new(
        "IOC003",
        "Circular dependency detected",
        "Circular dependency detected: {0}",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Break the circular dependency by: 1) Using dependency injection with interfaces, 2) Introducing a mediator pattern, or 3) Refactoring to eliminate the circular reference.");

    public static readonly DiagnosticDescriptor RegisterAsAllRequiresService = new(
        "IOC004",
        "RegisterAsAll attribute requires Service attribute",
        "Class '{0}' has [RegisterAsAll] attribute but is missing lifetime attribute",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Add lifetime attribute ([Scoped], [Singleton], or [Transient]) to the class to enable multi-interface registration.");

    public static readonly DiagnosticDescriptor SkipRegistrationWithoutRegisterAsAll = new(
        "IOC005",
        "SkipRegistration attribute has no effect without RegisterAsAll",
        "Class '{0}' has [SkipRegistration] attribute but no [RegisterAsAll] attribute",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Fix the attribute combination by: 1) Adding [RegisterAsAll] attribute to make [SkipRegistration] meaningful, or 2) Removing the unnecessary [SkipRegistration] attribute.");

    public static readonly DiagnosticDescriptor DuplicateDependsOnType = new(
        "IOC006",
        "Duplicate dependency type in DependsOn attributes",
        "Type '{0}' is declared multiple times in [DependsOn] attributes on class '{1}'",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove duplicate dependency declarations.");

    public static readonly DiagnosticDescriptor DependsOnConflictsWithInject = new(
        "IOC007",
        "DependsOn type conflicts with Inject field",
        "Type '{0}' is declared in [DependsOn] attribute but also exists as [Inject] field in class '{1}'",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove the [DependsOn] declaration or the [Inject] field to avoid duplication.");

    public static readonly DiagnosticDescriptor DuplicateTypeInSingleDependsOn = new(
        "IOC008",
        "Duplicate type in single DependsOn attribute",
        "Type '{0}' is declared multiple times in the same [DependsOn] attribute on class '{1}'",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove duplicate type declarations from the [DependsOn] attribute.");

    public static readonly DiagnosticDescriptor SkipRegistrationForNonRegisteredInterface = new(
        "IOC009",
        "SkipRegistration for interface not registered by RegisterAsAll",
        "Type '{0}' in [SkipRegistration] is not an interface that would be registered by [RegisterAsAll] on class '{1}'",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove unnecessary [SkipRegistration] declaration.");

    [Obsolete("IOC010 has been consolidated into IOC014 to eliminate duplicate diagnostics. Use IOC014 instead.")]
    public static readonly DiagnosticDescriptor BackgroundServiceLifetimeConflict = new(
        "IOC010",
        "Background service with non-Singleton lifetime (deprecated)",
        "Background service '{0}' has lifetime attribute with '{1}' lifetime. Background services should typically be Singleton. Note: This diagnostic is deprecated - use IOC014 instead.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "This diagnostic has been deprecated. Use IOC014 for background service lifetime validation.");

    public static readonly DiagnosticDescriptor BackgroundServiceNotPartial = new(
        "IOC011",
        "Background service class must be partial",
        "Background service '{0}' inherits from BackgroundService but is not marked as partial",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Add 'partial' modifier to the class declaration: 'public partial class {0}' to enable dependency injection constructor generation.");

    public static readonly DiagnosticDescriptor SingletonDependsOnScoped = new(
        "IOC012",
        "Singleton service depends on Scoped service",
        "Singleton service '{0}' depends on Scoped service '{1}'. Singleton services cannot capture shorter-lived dependencies.",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Fix the lifetime mismatch by: 1) Changing dependency '{1}' to [Singleton], 2) Changing this service to [Scoped] or [Transient], or 3) Use dependency factories/scoped service locator pattern.");

    public static readonly DiagnosticDescriptor SingletonDependsOnTransient = new(
        "IOC013",
        "Singleton service depends on Transient service",
        "Singleton service '{0}' depends on Transient service '{1}'. Consider if this transient should be Singleton or if the dependency is appropriate.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Review the design: 1) If '{1}' should be shared, change it to [Singleton], 2) If truly transient, this may cause issues as the singleton will capture only one instance - consider using IServiceProvider or factory pattern instead.");

    public static readonly DiagnosticDescriptor BackgroundServiceLifetimeValidation = new(
        "IOC014",
        "Background service with non-Singleton lifetime",
        "Background service '{0}' has {1} lifetime. Background services should typically be Singleton.",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Fix options: 1) Change to [Singleton] for optimal background service lifetime, 2) Use [BackgroundService(SuppressLifetimeWarnings = true)] to suppress this warning if the current lifetime is intentional, or 3) Consider if this should inherit from BackgroundService at all.");

    public static readonly DiagnosticDescriptor InheritanceChainLifetimeValidation = new(
        "IOC015",
        "Service lifetime mismatch in inheritance chain",
        "Service lifetime mismatch in inheritance chain: '{0}' ({1}) inherits from dependencies with {2} lifetime",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Fix the inheritance lifetime hierarchy by: 1) Making all services in the chain Singleton, 2) Changing consuming service to Scoped/Transient, or 3) Breaking the inheritance chain to avoid lifetime conflicts.");

    // Configuration Injection Validation Diagnostics (IOC016-IOC019)
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

    // Conditional Service Diagnostics (IOC020-IOC022)
    public static readonly DiagnosticDescriptor ConditionalServiceConflictingConditions = new(
        "IOC020",
        "Conditional service has conflicting conditions",
        "Conditional service '{0}' has conflicting conditions: {1}",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Ensure that Environment and NotEnvironment conditions do not overlap, and Equals and NotEquals conditions are not contradictory.");

    public static readonly DiagnosticDescriptor ConditionalServiceMissingServiceAttribute = new(
        "IOC021",
        "ConditionalService attribute requires Service attribute",
        "Class '{0}' has [ConditionalService] attribute but lifetime attribute is required",
        "IoCTools",
        DiagnosticSeverity.Error, // Changed to Error to match test expectations
        true,
        "Add lifetime attribute ([Scoped], [Singleton], or [Transient]) to the class to enable conditional service registration.");

    public static readonly DiagnosticDescriptor ConditionalServiceEmptyConditions = new(
        "IOC022",
        "ConditionalService attribute has no conditions",
        "Class '{0}' has [ConditionalService] attribute but at least one condition is required",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Specify at least one Environment, NotEnvironment, or ConfigValue condition for conditional registration.");

    public static readonly DiagnosticDescriptor ConditionalServiceConfigValueWithoutComparison = new(
        "IOC023",
        "ConfigValue specified without Equals or NotEquals",
        "Class '{0}' has ConfigValue '{1}' specified but Equals or NotEquals condition is required",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "When ConfigValue is specified, provide at least one Equals or NotEquals condition for comparison.");

    public static readonly DiagnosticDescriptor ConditionalServiceComparisonWithoutConfigValue = new(
        "IOC024",
        "Equals or NotEquals specified without ConfigValue",
        "Class '{0}' has Equals or NotEquals condition but ConfigValue is required",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "When using Equals or NotEquals, specify the ConfigValue property to define which configuration key to check.");

    public static readonly DiagnosticDescriptor ConditionalServiceEmptyConfigKey = new(
        "IOC025",
        "ConfigValue is empty or whitespace",
        "Class '{0}' has an empty or whitespace-only ConfigValue",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Provide a valid configuration key path for ConfigValue.");

    public static readonly DiagnosticDescriptor ConditionalServiceMultipleAttributes = new(
        "IOC026",
        "Multiple ConditionalService attributes on same class",
        "Class '{0}' has multiple [ConditionalService] attributes which may lead to unexpected behavior",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Consider combining conditions into a single [ConditionalService] attribute or use separate classes for different conditions.");

    public static readonly DiagnosticDescriptor DuplicateServiceRegistration = new(
        "IOC027",
        "Potential duplicate service registration",
        "Service '{0}' may be registered multiple times due to inheritance or attribute combinations",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "Review service registration patterns to ensure no unintended duplicates. The generator automatically deduplicates identical registrations.");

    // RegisterAs Attribute Diagnostics (IOC028-IOC031)
    public static readonly DiagnosticDescriptor RegisterAsRequiresService = new(
        "IOC028",
        "RegisterAs attribute requires service indicators",
        "Class '{0}' has [RegisterAs] attribute but lacks service indicators like [Lifetime], [Inject] fields, or other registration attributes",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Add [Lifetime], [Inject] fields, or other service indicators to enable selective interface registration.");

    public static readonly DiagnosticDescriptor RegisterAsInterfaceNotImplemented = new(
        "IOC029",
        "RegisterAs specifies unimplemented interface",
        "Class '{0}' has [RegisterAs] attribute specifying interface '{1}' but does not implement this interface",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Ensure that all interfaces specified in [RegisterAs] are actually implemented by the class.");

    public static readonly DiagnosticDescriptor RegisterAsDuplicateInterface = new(
        "IOC030",
        "RegisterAs contains duplicate interface",
        "Class '{0}' has [RegisterAs] attribute with duplicate interface '{1}'",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove duplicate interface specifications from the [RegisterAs] attribute.");

    public static readonly DiagnosticDescriptor RegisterAsNonInterfaceType = new(
        "IOC031",
        "RegisterAs specifies non-interface type",
        "Class '{0}' has [RegisterAs] attribute specifying non-interface type '{1}'",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "RegisterAs can only specify interface types. Use concrete class types for direct registration.");

    public static readonly DiagnosticDescriptor RedundantRegisterAsAttribute = new(
        "IOC032",
        "RegisterAs attribute is redundant",
        "Class '{0}' already registers interfaces {1} by default. Remove redundant [RegisterAs] attribute or reduce the interface list.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "RegisterAs should only be used when selectively registering a subset of interfaces.");

    public static readonly DiagnosticDescriptor RedundantScopedLifetimeAttribute = new(
        "IOC033",
        "Scoped lifetime attribute is redundant",
        "Class '{0}' is already implicitly registered as Scoped via {1}. Remove redundant [Scoped] attribute or change the lifetime to a non-default value.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Scoped is the default lifetime for implicit services; only specify it when clarifying intent or when no other service indicators exist.");

    public static readonly DiagnosticDescriptor RedundantRegisterAsWithRegisterAsAll = new(
        "IOC034",
        "RegisterAsAll already registers every interface",
        "Class '{0}' uses both [RegisterAsAll] and [RegisterAs]; selective RegisterAs attributes have no effect when RegisterAsAll is present",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove redundant [RegisterAs] attributes or drop [RegisterAsAll] if selective registration is required.");

    public static readonly DiagnosticDescriptor InjectFieldPreferDependsOn = new(
        "IOC035",
        "Inject field can be simplified to DependsOn",
        "Field '{0}' in class '{1}' uses [Inject] but matches the default DependsOn naming for dependency '{2}'. Prefer [DependsOn<{2}>] unless you require a custom field name or mutability.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Replace the [Inject] field with a [DependsOn] attribute so the generator can produce constructor parameters and backing fields automatically. Keep [Inject] only when a custom field name or non-readonly behavior is required.");

    public static readonly DiagnosticDescriptor MultipleLifetimeAttributes = new(
        "IOC036",
        "Multiple lifetime attributes declared",
        "Class '{0}' applies multiple lifetime attributes ({1}). Choose a single lifetime to avoid conflicting registrations.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove redundant lifetime attributes so only one of [Scoped], [Singleton], or [Transient] remains on the class.");

    public static readonly DiagnosticDescriptor ManualRegistrationDuplicatesIoCTools = new(
        "IOC050",
        "Manual registration duplicates IoCTools registration",
        "Service '{0}' is registered manually with lifetime '{1}' but IoCTools already registers it with the same lifetime. Remove the manual registration and rely on IoCTools attributes ({2}).",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Avoid duplicate manual registrations when IoCTools already emits the same service/implementation.");

    public static readonly DiagnosticDescriptor ManualRegistrationLifetimeMismatch = new(
        "IOC051",
        "Manual registration lifetime differs from IoCTools",
        "Service '{0}' is registered manually with lifetime '{1}' but IoCTools registers it with lifetime '{2}'. Align lifetimes or remove the manual registration.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Keep manual registrations aligned with IoCTools-generated lifetimes to avoid duplicate or conflicting registrations.");

    public static readonly DiagnosticDescriptor InheritedLifetimeRedundant = new(
        "IOC048",
        "Lifetime attribute duplicates inherited lifetime",
        "Class '{0}' declares lifetime '{1}' but already inherits the same lifetime from base class '{2}'. Remove the redundant lifetime attribute or change it to a different lifetime if intended.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Avoid repeating the same lifetime attribute on derived classes when the base class already establishes the lifetime.");

    public static readonly DiagnosticDescriptor SkipRegistrationOverridesOtherAttributes = new(
        "IOC037",
        "SkipRegistration overrides other registration attributes",
        "Class '{0}' uses [SkipRegistration] along with {1}, but SkipRegistration prevents those attributes from taking effect",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove redundant registration attributes or drop [SkipRegistration] so the class can register as intended.");

    public static readonly DiagnosticDescriptor SkipRegistrationIneffectiveInDirectMode = new(
        "IOC038",
        "SkipRegistration for interfaces has no effect in RegisterAsAll(DirectOnly)",
        "Class '{0}' declares [SkipRegistration] for interfaces, but RegisterAsAll is set to DirectOnly so no interfaces would register anyway",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Change RegisterAsAll to RegistrationMode.All/Exclusionary or remove the ineffective [SkipRegistration] declaration.");

    public static readonly DiagnosticDescriptor UnusedDependency = new(
        "IOC039",
        "Dependency declared but never used",
        "Dependency field '{0}' of type '{1}' declared via {2} on class '{3}' is never referenced. Remove the declaration or use the dependency.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Keep only the dependencies that are actually consumed by the class. Remove unused [Inject]/[DependsOn] declarations or reference the generated field in your implementation.");

    public static readonly DiagnosticDescriptor RedundantDependencyDeclarations = new(
        "IOC040",
        "Redundant dependency declarations",
        "Dependency type '{0}' is declared multiple times via {1} on class '{2}'. Remove the duplicate declarations so the generator only binds it once.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Declare each dependency a single time. Prefer [DependsOn] when no custom field is required and drop extra [Inject]/[DependsOn]/configuration declarations (including inherited ones) to avoid confusing constructor graphs.");

    public static readonly DiagnosticDescriptor ManualConstructorConflict = new(
        "IOC041",
        "Manual constructor conflicts with IoCTools dependencies",
        "Class '{0}' declares IoCTools dependencies but also defines manual constructor '{1}'. Remove the manual constructor or drop IoCTools dependency annotations; they cannot be combined.",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Let IoCTools generate the constructor for [Inject]/[InjectConfiguration]/[DependsOn] dependencies. If you need a hand-written constructor, remove the IoCTools dependency declarations or move your logic into a partial method.");

    public static readonly DiagnosticDescriptor UnnecessaryExternalDependency = new(
        "IOC042",
        "External dependency flag is not required",
        "Dependency '{0}' on class '{1}' is marked External, but an implementation is already available in this solution or referenced projects. Remove the External flag to let IoCTools manage it normally.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Use External only when the implementation is provided outside the solution (e.g., runtime-only or manually registered). If an implementation exists in a referenced project or is a supported framework service (ILogger, IConfiguration, IOptions, etc.), skip External.");

    public static readonly DiagnosticDescriptor OptionsDependencyNotSupported = new(
        "IOC043",
        "IOptions<T> should use DependsOnConfiguration",
        "Dependency '{0}' on class '{1}' uses IOptions-based types. Use [DependsOnConfiguration<...>] instead so IoCTools can bind configuration and manage options lifetimes.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Inject configuration via [DependsOnConfiguration<...>] and access the options payload directly. Avoid taking IOptions/IOptionsSnapshot/IOptionsMonitor as dependencies in IoCTools-managed constructors.");

    public static readonly DiagnosticDescriptor NonServiceDependencyType = new(
        "IOC044",
        "Dependency type is not a service",
        "Dependency '{0}' on class '{1}' is a primitive/value type or string. Use [DependsOnConfiguration<...>] for configuration values or depend on an interface/class service instead.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Reserve [DependsOn]/[Inject] for services (interfaces/classes). For configuration values, switch to [DependsOnConfiguration<...>] or [InjectConfiguration].");

    public static readonly DiagnosticDescriptor UnsupportedCollectionDependency = new(
        "IOC045",
        "Collection dependency type is not supported",
        "Dependency '{0}' on class '{1}' uses collection type '{2}'. Use IReadOnlyCollection<T> for sets of resolved services.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Use IReadOnlyCollection<T> when consuming multiple service implementations. Avoid arrays, IEnumerable<T>, IReadOnlyList<T>, List<T>, HashSet<T>, Dictionary<,>, or custom collections; wrap the allowed collection yourself if you need different semantics.");

    public static readonly DiagnosticDescriptor ConfigurationOverlap = new(
        "IOC046",
        "Overlapping configuration bindings",
        "Configuration section '{0}' is bound both as options '{1}' and as '{2}' on class '{3}'. Choose a single binding shape to avoid duplicate configuration sources.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Bind each configuration section exactly once. Avoid mixing options bindings with per-field configuration from the same section, and remove duplicate configuration slots across inheritance.");

    public static readonly DiagnosticDescriptor PreferParamsStyleAttributeArguments = new(
        "IOC047",
        "Use params-style attribute arguments",
        "Attribute '{0}' on '{1}' uses the '{2}' named argument; pass these values via the params argument instead (e.g., memberNames: value or configurationKeys: value)",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "Prefer params-style constructor arguments for [DependsOn] member names and [DependsOnConfiguration] keys so analyzers and generators can align argument order and defaults consistently.");

    public static readonly DiagnosticDescriptor NullableDependencyNotAllowed = new(
        "IOC048",
        "Dependencies must be non-nullable",
        "Dependency '{0}' on '{1}' is declared nullable. Provide a concrete/non-null dependency or register a no-op implementation instead of using nullable dependencies.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Dependencies are expected to be required. Prefer non-nullable types and use composition (feature flags, no-op implementations, Lazy<Func>) rather than nullable dependency slots.");

    public static readonly DiagnosticDescriptor DependencySetMetadataOnly = new(
        "IOC049",
        "Dependency sets must be metadata-only",
        "Type '{0}' implements IDependencySet and cannot declare members like methods, properties, fields, events, or nested types. Move those members elsewhere so the set stays metadata-only.",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "IDependencySet types are declaration-only containers for dependencies. Keep them free of executable members or state.");

    public static readonly DiagnosticDescriptor DependencySetCycleDetected = new(
        "IOC050",
        "Dependency set cycle detected",
        "Dependency set '{0}' forms a cycle: {1}. Remove one of the set references to break the cycle.",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Dependency sets must not reference each other in cycles (e.g., SetA -> SetB -> SetA). Flattening stops when a cycle exists, so fix the graph to proceed.");

    public static readonly DiagnosticDescriptor DependencySetNameCollision = new(
        "IOC051",
        "Dependency set expansion collided with an existing dependency",
        "Type '{0}' is pulled from dependency sets with conflicting member names ('{1}' vs '{2}') on '{3}'. Align the member names or remove the duplicate dependency.",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "When dependency sets flatten into a consumer, duplicate dependency types must share the same generated field/parameter name. Rename the slots or deduplicate the dependency in the sets.");

    public static readonly DiagnosticDescriptor DependencySetRegistrationDetected = new(
        "IOC052",
        "Dependency sets must not be registered as services",
        "Type '{0}' implements IDependencySet but is marked for registration via '{1}'. Remove lifetime/registration attributes; sets are metadata-only and are never registered.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "IDependencySet is a metadata-only construct. Do not combine it with lifetimes, RegisterAs/All, ConditionalService, ManualService, or ExternalService attributes.");

    public static readonly DiagnosticDescriptor DependencySetExtractionSuggestion = new(
        "IOC053",
        "Repeated dependency cluster could be a dependency set",
        "Dependencies {0} repeat across multiple services. Extract them into an IDependencySet and reference it with [DependsOn<{1}>].",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "When three or more dependencies appear together on several services, consider introducing an IDependencySet to reduce duplication and tighten diagnostics.");

    public static readonly DiagnosticDescriptor DependencySetNearMatchSuggestion = new(
        "IOC054",
        "Service nearly matches an existing dependency set",
        "Service '{0}' already has most of dependency set '{1}' ({2}/{3} members). Consider using the set plus the few additional dependencies to reduce noise.",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "Adopt existing dependency sets when a service already matches most of their members; add or remove the small delta locally.");

    public static readonly DiagnosticDescriptor DependencySetSharedBaseSuggestion = new(
        "IOC055",
        "Shared dependency cluster on related services",
        "Services derived from '{0}' share common dependencies {1}. Move them into a base-oriented IDependencySet (or the base class) for reuse.",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "When multiple derived services share the same dependencies, centralize them in a base set to cut duplication and align lifetimes.");

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

    public static readonly DiagnosticDescriptor SharedBaseMissingLifetimeSuggestion = new(
        "IOC058",
        "Apply lifetime attribute to shared base class",
        "Services deriving from '{0}' lack lifetime attributes. Add [{1}] to the base class to register all derived services in one place.",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "When many services share a base type, prefer a single lifetime attribute on the base to avoid duplicating [Scoped]/[Singleton]/[Transient] on every derived class.");

    public static readonly DiagnosticDescriptor RedundantSingletonLifetimeAttribute = new(
        "IOC059",
        "Singleton lifetime attribute is redundant",
        "Class '{0}' inherits [Singleton] from '{1}'. Remove redundant [Singleton] on the derived class.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "When a base class is already marked [Singleton], derived classes do not need to repeat the attribute unless changing lifetime.");

    public static readonly DiagnosticDescriptor RedundantTransientLifetimeAttribute = new(
        "IOC060",
        "Transient lifetime attribute is redundant",
        "Class '{0}' inherits [Transient] from '{1}'. Remove redundant [Transient] on the derived class.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "When a base class is already marked [Transient], derived classes do not need to repeat the attribute unless changing lifetime.");

    public static readonly DiagnosticDescriptor RedundantDependencySetInInheritance = new(
        "IOC061",
        "Dependency set already applied in base class",
        "Class '{0}' already inherits dependency set '{1}' from '{2}'. Remove redundant [DependsOn<{1}>] on the derived class.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Place dependency sets once on the base type when all descendants need them; avoid duplicating the same [DependsOn<Set>] on derived classes.");

    public static readonly DiagnosticDescriptor DependencySetBaseSuggestion = new(
        "IOC062",
        "Move shared dependency set to base class",
        "Services derived from '{0}' all reference dependency set '{1}'. Move [DependsOn<{1}>] to the base to reduce duplication.",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "When several derived services reference the same dependency set, place the [DependsOn<Set>] on their shared base class instead of repeating it.");

    public static readonly DiagnosticDescriptor RedundantRegisterAsInheritance = new(
        "IOC063",
        "RegisterAs attribute is redundant on derived class",
        "Class '{0}' inherits RegisterAs interfaces {1} from '{2}'. Remove redundant [RegisterAs] on the derived class.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Put [RegisterAs] on the base class once when all derived types share the same interface list; derived classes need it only when narrowing or extending the list.");

    public static readonly DiagnosticDescriptor RegisterAsBaseSuggestion = new(
        "IOC064",
        "Move shared RegisterAs to base class",
        "Services derived from '{0}' all specify [RegisterAs({1})]. Move the attribute to the base class to reduce duplication.",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "When multiple derived classes repeat the same RegisterAs interfaces, place the attribute on their shared base type instead.");

    public static readonly DiagnosticDescriptor RedundantRegisterAsAllInheritance = new(
        "IOC065",
        "RegisterAsAll attribute is redundant on derived class",
        "Class '{0}' inherits [RegisterAsAll] intent from '{1}'. Remove redundant [RegisterAsAll] on the derived class.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Only one [RegisterAsAll] is needed in an inheritance chain; place it on the base when all descendants should register all implemented interfaces.");

    public static readonly DiagnosticDescriptor RedundantConditionalServiceInheritance = new(
        "IOC067",
        "ConditionalService attribute is redundant on derived class",
        "Class '{0}' repeats [ConditionalService] with the same condition as '{1}'. Remove the redundant attribute or change the condition.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Use [ConditionalService] on the base when derived types share the same predicate; only override when the condition differs.");

    public static readonly DiagnosticDescriptor ConstructorCouldUseDependsOn = new(
        "IOC068",
        "Constructor parameters could be expressed with [DependsOn] and lifetime attribute",
        "Class '{0}' has a manual constructor with injectable parameters but no IoCTools attributes. Consider adding [Scoped]/[Singleton]/[Transient] and [DependsOn<{1}>].",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "Classes with DI-like constructors can opt into IoCTools by adding a lifetime attribute and [DependsOn<T>] to enable generator support and diagnostics.");

    public static readonly DiagnosticDescriptor RegisterAsMissingLifetime = new(
        "IOC069",
        "RegisterAs requires a lifetime attribute",
        "Class '{0}' uses [RegisterAs] but has no lifetime attribute. Add [Scoped], [Singleton], or [Transient].",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Registration attributes still need a lifetime indicator; add a lifetime attribute once on the class.");

    public static readonly DiagnosticDescriptor DependsOnMissingLifetime = new(
        "IOC070",
        "DependsOn/Inject used without lifetime",
        "Class '{0}' declares dependencies but has no lifetime attribute. Add [Scoped], [Singleton], or [Transient].",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "When a class has [DependsOn] or [Inject], add a lifetime so it will be registered and validated.");

    public static readonly DiagnosticDescriptor ConditionalMissingLifetime = new(
        "IOC071",
        "ConditionalService missing lifetime",
        "Class '{0}' uses [ConditionalService] but has no lifetime attribute. Add [Scoped], [Singleton], or [Transient].",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Conditional services still require an explicit lifetime; add one to enable registration.");

    public static readonly DiagnosticDescriptor HostedServiceMissingLifetime = new(
        "IOC072",
        "Hosted service should be Singleton",
        "Class '{0}' implements IHostedService/BackgroundService but has no lifetime attribute. Add [Singleton].",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Hosted services are typically singletons; add [Singleton] to enable generator registration and diagnostics.");

    public static readonly DiagnosticDescriptor ManualRegistrationCouldUseAttributes = new(
        "IOC073",
        "Manual registration could use IoCTools attributes",
        "'{0}' is registered manually as {1} but the implementation '{2}' lacks IoCTools lifetime attributes. Consider adding [Scoped]/[Singleton]/[Transient] (and [RegisterAs]) instead of manual registration.",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "Prefer IoCTools attributes over manual registrations to unlock diagnostics and generated registration.");

    public static readonly DiagnosticDescriptor MissingRegisterAsAllForMultiInterface = new(
        "IOC074",
        "Multi-interface class could use RegisterAsAll",
        "Class '{0}' implements multiple interfaces but only has a lifetime attribute. Consider adding [RegisterAsAll] to register all interfaces automatically.",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "When a class implements multiple interfaces, [RegisterAsAll] makes intent explicit and prevents partial registrations.");
}
