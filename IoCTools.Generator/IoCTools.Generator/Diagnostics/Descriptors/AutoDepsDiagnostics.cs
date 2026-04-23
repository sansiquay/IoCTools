namespace IoCTools.Generator.Diagnostics;

internal static partial class DiagnosticDescriptors
{
    private const string AutoDepsHelpBase = "https://github.com/sansiquay/IoCTools/blob/main/docs/auto-deps.md#";
    private const string MigrationHelpBase = "https://github.com/sansiquay/IoCTools/blob/main/docs/migration.md#";

    public static readonly DiagnosticDescriptor InjectDeprecated = new(
        "IOC095",
        "[Inject] is deprecated; use [DependsOn<T>]",
        "[Inject] on field '{0}' is deprecated. Use [DependsOn<{1}>] on the class. A code fix is available.",
        "IoCTools.Usage",
        DiagnosticSeverity.Warning,
        true,
        "Migrate to [DependsOn<T>] on the class. See migration guide for full deprecation timeline (1.6 warning → 1.7 error → 2.0 removed).",
        MigrationHelpBase + "migrating-from-15x-to-16x");

    public static readonly DiagnosticDescriptor NoAutoDepStale = new(
        "IOC096",
        "NoAutoDep[Open] target is not in resolved auto-dep set",
        "The type '{0}' suppressed by {1} on {2} is not in the resolved auto-dep set. This opt-out has no effect.",
        "IoCTools.AutoDeps",
        DiagnosticSeverity.Info,
        true,
        "Remove the stale opt-out attribute. If you meant to suppress a different type, double-check the type argument.",
        AutoDepsHelpBase + "ioc096");

    public static readonly DiagnosticDescriptor ProfileMissingMarker = new(
        "IOC097",
        "Profile type does not implement IAutoDepsProfile",
        "Profile type '{0}' does not implement IAutoDepsProfile. Add the interface to make '{0}' a valid profile target.",
        "IoCTools.AutoDeps",
        DiagnosticSeverity.Warning,
        true,
        "Add ': IAutoDepsProfile' to the profile class declaration.",
        AutoDepsHelpBase + "ioc097");

    public static readonly DiagnosticDescriptor DependsOnAutoDepOverlap = new(
        "IOC098",
        "[DependsOn<T>] overlaps with an active auto-dep",
        "[DependsOn<{0}>] overlaps with an active auto-dep for the same type (source: {1}). The explicit DependsOn takes precedence; the auto-dep is suppressed. Consider removing one.",
        "IoCTools.AutoDeps",
        DiagnosticSeverity.Info,
        true,
        "Remove the redundant [DependsOn<T>] (auto-dep covers it), or remove the auto-dep if the explicit declaration is preferred.",
        AutoDepsHelpBase + "ioc098");

    public static readonly DiagnosticDescriptor AutoDepsApplyStale = new(
        "IOC099",
        "Profile attachment rule matches zero services",
        "{0} matches zero services in this assembly. Verify the match criterion is correct or remove the rule.",
        "IoCTools.AutoDeps",
        DiagnosticSeverity.Info,
        true,
        "Remove the stale AutoDepsApply/AutoDepsApplyGlob attribute, or adjust the target type / glob pattern.",
        AutoDepsHelpBase + "ioc099");

    // Note: IOC100-IOC102 were originally planned for these descriptors but collided with
    // IoCTools.FluentValidation's IOC100-IOC102 (released in 1.5.1). Moved to IOC106-IOC108
    // prior to the 1.6.0 release so a `.editorconfig` suppression of IOC10X does not
    // silence both families at once.
    public static readonly DiagnosticDescriptor AutoDepOpenMultiArity = new(
        "IOC106",
        "AutoDepOpen requires single-arity unbound generic",
        "AutoDepOpen requires a single-arity unbound generic. '{0}' has arity {1}. Multi-arity open generics have no universal closing rule.",
        "IoCTools.AutoDeps",
        DiagnosticSeverity.Error,
        true,
        "Use AutoDep<T> with a fully-closed type instead, or declare per-service dependencies via [DependsOn<T>].",
        AutoDepsHelpBase + "ioc106");

    public static readonly DiagnosticDescriptor AutoDepOpenNonGeneric = new(
        "IOC107",
        "AutoDepOpen requires an unbound generic type",
        "AutoDepOpen requires an unbound generic type. '{0}' is not generic. Use AutoDep<T> for closed types.",
        "IoCTools.AutoDeps",
        DiagnosticSeverity.Error,
        true,
        "Change [AutoDepOpen(typeof(T))] to [AutoDep<T>] for non-generic closed types.",
        AutoDepsHelpBase + "ioc107");

    public static readonly DiagnosticDescriptor AutoDepOpenConstraintViolation = new(
        "IOC108",
        "AutoDepOpen closure violates type parameter constraint",
        "AutoDepOpen closure of '{0}' to service '{1}' violates type parameter constraint '{2}'. Consider suppressing on this service via [NoAutoDepOpen(typeof({3}))].",
        "IoCTools.AutoDeps",
        DiagnosticSeverity.Error,
        true,
        "The service type does not satisfy the open generic's constraint. Add [NoAutoDepOpen(typeof(T<>))] to the service, or change the open generic to a compatible one.",
        AutoDepsHelpBase + "ioc108");

    public static readonly DiagnosticDescriptor AutoDepsApplyGlobInvalid = new(
        "IOC103",
        "AutoDepsApplyGlob pattern is invalid",
        "AutoDepsApplyGlob pattern '{0}' is invalid. Patterns use the same glob grammar as IoCToolsIgnoredTypePatterns: '*' for any sequence, '?' for a single character.",
        "IoCTools.AutoDeps",
        DiagnosticSeverity.Error,
        true,
        "Fix the glob pattern. Common errors: unterminated character class (e.g., '[unterminated'), empty pattern.",
        AutoDepsHelpBase + "ioc103");

    public static readonly DiagnosticDescriptor ProfileIsGeneric = new(
        "IOC104",
        "Profile type is generic",
        "Profile type '{0}' is generic. Profiles must be non-generic in 1.6. Define a non-generic class implementing IAutoDepsProfile instead.",
        "IoCTools.AutoDeps",
        DiagnosticSeverity.Error,
        true,
        "Define the profile as a non-generic class. Generic profile classes are a deferred feature (1.7+).",
        AutoDepsHelpBase + "ioc104");

    public static readonly DiagnosticDescriptor RedundantProfileAttachment = new(
        "IOC105",
        "Redundant profile attachment",
        "Service '{0}' is attached to profile '{1}' via multiple paths: {2}. The attachment is deduped, but consider removing redundant rules.",
        "IoCTools.AutoDeps",
        DiagnosticSeverity.Info,
        true,
        "Remove duplicate attachment paths. The profile is applied once regardless, but keeping a single source-of-truth reduces configuration drift.",
        AutoDepsHelpBase + "ioc105");
}
