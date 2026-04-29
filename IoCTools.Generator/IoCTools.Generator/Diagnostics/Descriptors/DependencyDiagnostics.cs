namespace IoCTools.Generator.Diagnostics;

internal static partial class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor NoImplementationFound = new(
        "IOC001",
        "No implementation found for interface",
        "Service '{0}' depends on '{1}' but no implementation of this interface exists in the project",
        "IoCTools.Dependency",
        DiagnosticSeverity.Error,
        true,
        "Fix options: 1) Create a class implementing '{1}' with lifetime attribute ([Scoped], [Singleton], or [Transient]), 2) Add [ExternalService] attribute to this class if dependency is provided externally, or 3) Register manually with services.AddScoped<{1}, Implementation>() in Program.cs.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc001");

    public static readonly DiagnosticDescriptor ImplementationNotRegistered = new(
        "IOC002",
        "Implementation exists but not registered",
        "Service '{0}' depends on '{1}' - implementation exists but lacks lifetime attribute ([Scoped], [Singleton], or [Transient])",
        "IoCTools.Dependency",
        DiagnosticSeverity.Error,
        true,
        "Fix options: 1) Add lifetime attribute ([Scoped], [Singleton], or [Transient]) to the implementation of '{1}', 2) Add [ExternalService] attribute to this class if dependency is provided externally, or 3) Register manually with services.AddScoped<{1}, Implementation>() in Program.cs.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc002");

    public static readonly DiagnosticDescriptor CircularDependency = new(
        "IOC003",
        "Circular dependency detected",
        "Circular dependency detected: {0}",
        "IoCTools.Dependency",
        DiagnosticSeverity.Error,
        true,
        "Break the circular dependency by: 1) Using dependency injection with interfaces, 2) Introducing a mediator pattern, or 3) Refactoring to eliminate the circular reference.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc003");

    public static readonly DiagnosticDescriptor DuplicateDependsOnType = new(
        "IOC006",
        "Duplicate dependency type in DependsOn attributes",
        "Type '{0}' is declared multiple times in [DependsOn] attributes on class '{1}'",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Remove duplicate dependency declarations.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc006");

    public static readonly DiagnosticDescriptor DependsOnConflictsWithInject = new(
        "IOC007",
        "DependsOn type conflicts with Inject field",
        "Type '{0}' is declared in [DependsOn] attribute but also exists as [Inject] field in class '{1}'",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Remove the [DependsOn] declaration or the [Inject] field to avoid duplication.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc007");

    public static readonly DiagnosticDescriptor DuplicateTypeInSingleDependsOn = new(
        "IOC008",
        "Duplicate type in single DependsOn attribute",
        "Type '{0}' is declared multiple times in the same [DependsOn] attribute on class '{1}'",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Remove duplicate type declarations from the [DependsOn] attribute.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc008");

    public static readonly DiagnosticDescriptor SkipRegistrationForNonRegisteredInterface = new(
        "IOC009",
        "SkipRegistration for interface not registered by RegisterAsAll",
        "Type '{0}' in [SkipRegistration] is not an interface that would be registered by [RegisterAsAll] on class '{1}'",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Remove unnecessary [SkipRegistration] declaration.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc009");

    public static readonly DiagnosticDescriptor UnusedDependency = new(
        "IOC039",
        "Dependency declared but never used",
        "Dependency field '{0}' of type '{1}' declared via {2} on class '{3}' is never referenced. Remove the declaration or use the dependency.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Keep only the dependencies that are actually consumed by the class. Remove unused [Inject]/[DependsOn] declarations or reference the generated field in your implementation.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc039");

    public static readonly DiagnosticDescriptor RedundantDependencyDeclarations = new(
        "IOC040",
        "Redundant dependency declarations",
        "Dependency type '{0}' is declared multiple times via {1} on class '{2}'. Remove the duplicate declarations so the generator only binds it once.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Declare each dependency a single time. Prefer [DependsOn] when no custom field is required and drop extra [Inject]/[DependsOn]/configuration declarations (including inherited ones) to avoid confusing constructor graphs.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc040");

    public static readonly DiagnosticDescriptor ManualConstructorConflict = new(
        "IOC041",
        "Manual constructor conflicts with IoCTools dependencies",
        "Class '{0}' declares IoCTools dependencies but also defines manual constructor '{1}'. Remove the manual constructor or drop IoCTools dependency annotations; they cannot be combined.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Error,
        true,
        "Let IoCTools generate the constructor for [Inject]/[InjectConfiguration]/[DependsOn] dependencies. If you need a hand-written constructor, remove the IoCTools dependency declarations or move your logic into a partial method.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc041");

    public static readonly DiagnosticDescriptor UnnecessaryExternalDependency = new(
        "IOC042",
        "External dependency flag is not required",
        "Dependency '{0}' on class '{1}' is marked External, but an implementation is already available in this solution or referenced projects. Remove the External flag to let IoCTools manage it normally.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Use External only when the implementation is provided outside the solution (e.g., runtime-only or manually registered). If an implementation exists in a referenced project or is a supported framework service (ILogger, IConfiguration, IOptions, etc.), skip External.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc042");

    public static readonly DiagnosticDescriptor OptionsDependencyNotSupported = new(
        "IOC043",
        "IOptions<T> should use DependsOnConfiguration",
        "Dependency '{0}' on class '{1}' uses IOptions-based types. Use [DependsOnConfiguration<...>] instead so IoCTools can bind configuration and manage options lifetimes.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Inject configuration via [DependsOnConfiguration<...>] and access the options payload directly. Avoid taking IOptions/IOptionsSnapshot/IOptionsMonitor as dependencies in IoCTools-managed constructors.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc043");

    public static readonly DiagnosticDescriptor NonServiceDependencyType = new(
        "IOC044",
        "Dependency type is not a service",
        "Dependency '{0}' on class '{1}' is a primitive/value type or string. Use [DependsOnConfiguration<...>] for configuration values or depend on an interface/class service instead.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Reserve [DependsOn]/[Inject] for services (interfaces/classes). For configuration values, switch to [DependsOnConfiguration<...>] or [DependsOnOptions]. Do not introduce new InjectConfiguration usage.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc044");

    public static readonly DiagnosticDescriptor UnsupportedCollectionDependency = new(
        "IOC045",
        "Collection dependency type is not supported",
        "Dependency '{0}' on class '{1}' uses collection type '{2}'. Use IReadOnlyCollection<T> for sets of resolved services.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Use IReadOnlyCollection<T> when consuming multiple service implementations. Avoid arrays, IEnumerable<T>, IReadOnlyList<T>, List<T>, HashSet<T>, Dictionary<,>, or custom collections; wrap the allowed collection yourself if you need different semantics.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc045");

    public static readonly DiagnosticDescriptor NullableDependencyNotAllowed = new(
        "IOC048",
        "Dependencies must be non-nullable",
        "Dependency '{0}' on '{1}' is declared nullable. Provide a concrete/non-null dependency or register a no-op implementation instead of using nullable dependencies.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Dependencies are expected to be required. Prefer non-nullable types and use composition (feature flags, no-op implementations, Lazy<Func>) rather than nullable dependency slots.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc048");

    public static readonly DiagnosticDescriptor DependencySetMetadataOnly = new(
        "IOC049",
        "Dependency sets must be metadata-only",
        "Type '{0}' implements IDependencySet and cannot declare members like methods, properties, fields, events, or nested types. Move those members elsewhere so the set stays metadata-only.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Error,
        true,
        "IDependencySet types are declaration-only containers for dependencies. Keep them free of executable members or state.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc049");

    public static readonly DiagnosticDescriptor DependencySetCycleDetected = new(
        "IOC050",
        "Dependency set cycle detected",
        "Dependency set '{0}' forms a cycle: {1}. Remove one of the set references to break the cycle.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Error,
        true,
        "Dependency sets must not reference each other in cycles (e.g., SetA -> SetB -> SetA). Flattening stops when a cycle exists, so fix the graph to proceed.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc050");

    public static readonly DiagnosticDescriptor DependencySetNameCollision = new(
        "IOC051",
        "Dependency set expansion collided with an existing dependency",
        "Type '{0}' is pulled from dependency sets with conflicting member names ('{1}' vs '{2}') on '{3}'. Align the member names or remove the duplicate dependency.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Error,
        true,
        "When dependency sets flatten into a consumer, duplicate dependency types must share the same generated field/parameter name. Rename the slots or deduplicate the dependency in the sets.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc051");

    public static readonly DiagnosticDescriptor DependencySetRegistrationDetected = new(
        "IOC052",
        "Dependency sets must not be registered as services",
        "Type '{0}' implements IDependencySet but is marked for registration via '{1}'. Remove lifetime/registration attributes; sets are metadata-only and are never registered.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "IDependencySet is a metadata-only construct. Do not combine it with lifetimes, RegisterAs/All, ConditionalService, ManualService, or ExternalService attributes.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc052");

    public static readonly DiagnosticDescriptor DependencySetExtractionSuggestion = new(
        "IOC053",
        "Repeated dependency cluster could be a dependency set",
        "Dependencies {0} repeat across multiple services. Extract them into an IDependencySet and reference it with [DependsOn<{1}>].",
        "IoCTools.Dependency",
        DiagnosticSeverity.Info,
        true,
        "When three or more dependencies appear together on several services, consider introducing an IDependencySet to reduce duplication and tighten diagnostics.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc053");

    public static readonly DiagnosticDescriptor DependencySetNearMatchSuggestion = new(
        "IOC054",
        "Service nearly matches an existing dependency set",
        "Service '{0}' already has most of dependency set '{1}' ({2}/{3} members). Consider using the set plus the few additional dependencies to reduce noise.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Info,
        true,
        "Adopt existing dependency sets when a service already matches most of their members; add or remove the small delta locally.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc054");

    public static readonly DiagnosticDescriptor DependencySetSharedBaseSuggestion = new(
        "IOC055",
        "Shared dependency cluster on related services",
        "Services derived from '{0}' share common dependencies {1}. Move them into a base-oriented IDependencySet (or the base class) for reuse.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Info,
        true,
        "When multiple derived services share the same dependencies, centralize them in a base set to cut duplication and align lifetimes.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc055");

    public static readonly DiagnosticDescriptor RedundantDependencySetInInheritance = new(
        "IOC061",
        "Dependency set already applied in base class",
        "Class '{0}' already inherits dependency set '{1}' from '{2}'. Remove redundant [DependsOn<{1}>] on the derived class.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Place dependency sets once on the base type when all descendants need them; avoid duplicating the same [DependsOn<Set>] on derived classes.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc061");

    public static readonly DiagnosticDescriptor DependencySetBaseSuggestion = new(
        "IOC062",
        "Move shared dependency set to base class",
        "Services derived from '{0}' all reference dependency set '{1}'. Move [DependsOn<{1}>] to the base to reduce duplication.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Info,
        true,
        "When several derived services reference the same dependency set, place the [DependsOn<Set>] on their shared base class instead of repeating it.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc062");

    public static readonly DiagnosticDescriptor RedundantDependencyWrapper = new(
        "IOC076",
        "Property redundantly wraps IoCTools dependency field",
        "Property '{0}' on class '{1}' only returns dependency field '{2}'. Access the injected field directly or move the dependency to the base type instead of keeping a pass-through property.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Avoid trivial wrapper properties around IoCTools dependency fields; they add noise without behavior. Use the generated field directly or refactor the dependency location if the base class needs it.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc076");

    public static readonly DiagnosticDescriptor ManualDependencyFieldShadowsGenerated = new(
        "IOC077",
        "Manual field shadows IoCTools-generated dependency",
        "Field '{0}' on class '{1}' shadows the IoCTools-generated dependency for '{2}'. Remove the manual field and rely on [DependsOn]/[DependsOnConfiguration], or use [Inject] with a custom name instead of duplicating the slot.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Error,
        true,
        "Do not declare fields with the same name as IoCTools-generated dependencies; it prevents generation and leaves the dependency unassigned.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc077");

    public static readonly DiagnosticDescriptor DependsOnMemberNameSuppressedByField = new(
        "IOC078",
        "MemberNames entry is suppressed by existing field",
        "MemberNames value '{0}' on attribute '{1}' is ignored because a field with that name already exists in class '{2}'. Remove the field or drop MemberNames to let IoCTools generate and wire the dependency.",
        "IoCTools.Dependency",
        DiagnosticSeverity.Warning,
        true,
        "Avoid providing MemberNames that collide with existing fields; IoCTools will skip generation and the dependency will not be assigned.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc078");
}
