namespace IoCTools.Tools.Cli;

/// <summary>
/// Static catalog of all IoCTools diagnostic descriptors for CLI commands.
/// </summary>
internal static class DiagnosticCatalog
{
    /// <summary>Describes a catalog entry with id, title, category, and default severity.</summary>
    internal readonly struct Entry
    {
        public string Id { get; }
        public string Title { get; }
        public string Category { get; }
        public string DefaultSeverity { get; }

        public Entry(string id, string title, string category, string defaultSeverity)
        {
            Id = id;
            Title = title;
            Category = category;
            DefaultSeverity = defaultSeverity;
        }
    }

    private static readonly IReadOnlyList<Entry> _entries = BuildCatalog();

    public static IReadOnlyList<Entry> GetAll() => _entries;

    private static IReadOnlyList<Entry> BuildCatalog()
    {
        return new List<Entry>
        {
            // IoCTools.Dependency
            new("IOC001", "No implementation found for interface", "IoCTools.Dependency", "Error"),
            new("IOC002", "Implementation exists but not registered", "IoCTools.Dependency", "Error"),
            new("IOC003", "Circular dependency detected", "IoCTools.Dependency", "Error"),
            new("IOC006", "Duplicate dependency type in DependsOn attributes", "IoCTools.Dependency", "Warning"),
            new("IOC007", "DependsOn type conflicts with Inject field", "IoCTools.Dependency", "Warning"),
            new("IOC008", "Duplicate type in single DependsOn attribute", "IoCTools.Dependency", "Warning"),
            new("IOC009", "SkipRegistration for interface not registered by RegisterAsAll", "IoCTools.Dependency", "Warning"),
            new("IOC039", "Dependency declared but never used", "IoCTools.Dependency", "Warning"),
            new("IOC040", "Redundant dependency declarations", "IoCTools.Dependency", "Warning"),
            new("IOC041", "Manual constructor conflicts with IoCTools dependencies", "IoCTools.Dependency", "Error"),
            new("IOC042", "External dependency flag is not required", "IoCTools.Dependency", "Warning"),
            new("IOC043", "IOptions<T> should use DependsOnConfiguration", "IoCTools.Dependency", "Warning"),
            new("IOC044", "Dependency type is not a service", "IoCTools.Dependency", "Warning"),
            new("IOC045", "Collection dependency type is not supported", "IoCTools.Dependency", "Warning"),
            new("IOC048", "Dependencies must be non-nullable", "IoCTools.Dependency", "Warning"),
            new("IOC049", "Dependency sets must be metadata-only", "IoCTools.Dependency", "Error"),
            new("IOC050", "Dependency set cycle detected", "IoCTools.Dependency", "Error"),
            new("IOC051", "Dependency set expansion collided with an existing dependency", "IoCTools.Dependency", "Error"),
            new("IOC052", "Dependency sets must not be registered as services", "IoCTools.Dependency", "Warning"),
            new("IOC053", "Repeated dependency cluster could be a dependency set", "IoCTools.Dependency", "Info"),
            new("IOC054", "Service nearly matches an existing dependency set", "IoCTools.Dependency", "Info"),
            new("IOC055", "Shared dependency cluster on related services", "IoCTools.Dependency", "Info"),
            new("IOC061", "Dependency set already applied in base class", "IoCTools.Dependency", "Warning"),
            new("IOC062", "Move shared dependency set to base class", "IoCTools.Dependency", "Info"),
            new("IOC076", "Property redundantly wraps IoCTools dependency field", "IoCTools.Dependency", "Warning"),
            new("IOC077", "Manual field shadows IoCTools-generated dependency", "IoCTools.Dependency", "Error"),
            new("IOC078", "MemberNames entry is suppressed by existing field", "IoCTools.Dependency", "Warning"),

            // IoCTools.Lifetime
            new("IOC012", "Singleton service depends on Scoped service", "IoCTools.Lifetime", "Error"),
            new("IOC013", "Singleton service depends on Transient service", "IoCTools.Lifetime", "Warning"),
            new("IOC014", "Background service with non-Singleton lifetime", "IoCTools.Lifetime", "Error"),
            new("IOC015", "Service lifetime mismatch in inheritance chain", "IoCTools.Lifetime", "Error"),
            new("IOC033", "Scoped lifetime attribute is redundant", "IoCTools.Lifetime", "Warning"),
            new("IOC059", "Singleton lifetime attribute is redundant", "IoCTools.Lifetime", "Warning"),
            new("IOC060", "Transient lifetime attribute is redundant", "IoCTools.Lifetime", "Warning"),
            new("IOC075", "Inconsistent lifetimes across inherited services", "IoCTools.Lifetime", "Warning"),
            new("IOC084", "Lifetime attribute duplicates inherited lifetime", "IoCTools.Lifetime", "Warning"),
            new("IOC087", "Transient service depends on Scoped service", "IoCTools.Lifetime", "Error"),

            // IoCTools.Registration
            new("IOC004", "RegisterAsAll attribute requires Service attribute", "IoCTools.Registration", "Error"),
            new("IOC005", "SkipRegistration attribute has no effect without RegisterAsAll", "IoCTools.Registration", "Warning"),
            new("IOC027", "Potential duplicate service registration", "IoCTools.Registration", "Info"),
            new("IOC028", "RegisterAs attribute requires service indicators", "IoCTools.Registration", "Error"),
            new("IOC029", "RegisterAs specifies unimplemented interface", "IoCTools.Registration", "Error"),
            new("IOC030", "RegisterAs contains duplicate interface", "IoCTools.Registration", "Warning"),
            new("IOC031", "RegisterAs specifies non-interface type", "IoCTools.Registration", "Error"),
            new("IOC032", "RegisterAs attribute is redundant", "IoCTools.Registration", "Warning"),
            new("IOC034", "RegisterAsAll already registers every interface", "IoCTools.Registration", "Warning"),
            new("IOC035", "Inject field should use DependsOn", "IoCTools.Registration", "Warning"),
            new("IOC036", "Multiple lifetime attributes declared", "IoCTools.Registration", "Warning"),
            new("IOC037", "SkipRegistration override other registration attributes", "IoCTools.Registration", "Warning"),
            new("IOC038", "SkipRegistration for interfaces has no effect in RegisterAsAll(DirectOnly)", "IoCTools.Registration", "Warning"),
            new("IOC047", "Use params-style attribute arguments", "IoCTools.Registration", "Info"),
            new("IOC063", "RegisterAs attribute is redundant on derived class", "IoCTools.Registration", "Warning"),
            new("IOC064", "Move shared RegisterAs to base class", "IoCTools.Registration", "Info"),
            new("IOC065", "RegisterAsAll attribute is redundant on derived class", "IoCTools.Registration", "Warning"),
            new("IOC069", "RegisterAs requires a lifetime attribute", "IoCTools.Registration", "Warning"),
            new("IOC070", "DependsOn/Inject used without lifetime", "IoCTools.Registration", "Warning"),
            new("IOC071", "ConditionalService missing lifetime", "IoCTools.Registration", "Warning"),
            new("IOC072", "Hosted service lifetime should be implicit", "IoCTools.Registration", "Warning"),
            new("IOC074", "Multi-interface class could use RegisterAsAll", "IoCTools.Registration", "Info"),
            new("IOC081", "Manual registration duplicates IoCTools registration", "IoCTools.Registration", "Warning"),
            new("IOC082", "Manual registration lifetime differs from IoCTools", "IoCTools.Registration", "Error"),
            new("IOC083", "Manual options registration duplicates IoCTools binding", "IoCTools.Registration", "Error"),
            new("IOC085", "Member name matches default", "IoCTools.Registration", "Warning"),
            new("IOC086", "Manual registration could use IoCTools attributes", "IoCTools.Registration", "Warning"),
            new("IOC090", "typeof() registration could use IoCTools attributes", "IoCTools.Registration", "Warning"),
            new("IOC091", "typeof() registration duplicates IoCTools registration", "IoCTools.Registration", "Warning"),
            new("IOC092", "typeof() registration lifetime differs from IoCTools", "IoCTools.Registration", "Error"),
            new("IOC094", "Open generic typeof() registration could use IoCTools attributes", "IoCTools.Registration", "Info"),
            // NOTE: IOC095 is defined by two descriptors in the generator today
            // (OpenGenericSharedInstanceFallsBackToSeparate + InjectDeprecated). The 1.6
            // milestone reassigned IOC095 to InjectDeprecated; the Registration-side entry
            // appears to be a stale holdover. The IoCTools.Usage entry below handles both.

            // IoCTools.Structural
            new("IOC010", "Background service with non-Singleton lifetime (deprecated)", "IoCTools.Structural", "Warning"),
            new("IOC011", "Background service class must be partial", "IoCTools.Structural", "Error"),
            new("IOC020", "Conditional service has conflicting conditions", "IoCTools.Structural", "Warning"),
            new("IOC021", "ConditionalService attribute requires Service attribute", "IoCTools.Structural", "Error"),
            new("IOC022", "ConditionalService attribute has no conditions", "IoCTools.Structural", "Warning"),
            new("IOC023", "ConfigValue specified without Equals or NotEquals", "IoCTools.Structural", "Warning"),
            new("IOC024", "Equals or NotEquals specified without ConfigValue", "IoCTools.Structural", "Warning"),
            new("IOC025", "ConfigValue is empty or whitespace", "IoCTools.Structural", "Warning"),
            new("IOC026", "Multiple ConditionalService attributes on same class", "IoCTools.Structural", "Warning"),
            new("IOC058", "Apply lifetime attribute to shared base class", "IoCTools.Structural", "Info"),
            new("IOC067", "ConditionalService attribute is redundant on derived class", "IoCTools.Structural", "Warning"),
            new("IOC068", "Constructor parameters could be expressed with [DependsOn] and lifetime attribute", "IoCTools.Structural", "Info"),
            new("IOC080", "Service class must be partial", "IoCTools.Structural", "Error"),

            // IoCTools.Configuration
            new("IOC016", "Invalid configuration key", "IoCTools.Configuration", "Error"),
            new("IOC017", "Unsupported configuration type", "IoCTools.Configuration", "Warning"),
            new("IOC018", "InjectConfiguration requires partial class", "IoCTools.Configuration", "Error"),
            new("IOC019", "InjectConfiguration on static field not supported", "IoCTools.Configuration", "Warning"),
            new("IOC046", "Overlapping configuration bindings", "IoCTools.Configuration", "Warning"),
            new("IOC056", "Use a single configuration binding style per section", "IoCTools.Configuration", "Info"),
            new("IOC057", "Configuration binding not found", "IoCTools.Configuration", "Warning"),
            new("IOC079", "Prefer DependsOnConfiguration over IConfiguration", "IoCTools.Configuration", "Warning"),
            new("IOC088", "Configuration type has circular reference", "IoCTools.Configuration", "Error"),
            new("IOC089", "SupportsReloading is only supported for Options pattern types", "IoCTools.Configuration", "Warning"),

            // IoCTools.Testing
            new("TDIAG-01", "Test class has manual mock fields that could be generated", "IoCTools.Testing", "Info"),
            new("TDIAG-02", "Test class has manual SUT construction that could be generated", "IoCTools.Testing", "Info"),
            new("TDIAG-03", "Test class covers a service not registered with IoCTools", "IoCTools.Testing", "Warning"),
            new("TDIAG-04", "Cover attribute references a type that is not a service", "IoCTools.Testing", "Error"),
            new("TDIAG-05", "Test class has multiple Cover attributes", "IoCTools.Testing", "Error"),

            // IoCTools.AutoDeps (1.6 — Auto-dependencies milestone)
            new("IOC096", "NoAutoDep[Open] target is not in resolved auto-dep set", "IoCTools.AutoDeps", "Info"),
            new("IOC097", "Profile type does not implement IAutoDepsProfile", "IoCTools.AutoDeps", "Warning"),
            new("IOC098", "[DependsOn<T>] overlaps with an active auto-dep", "IoCTools.AutoDeps", "Info"),
            new("IOC099", "Profile attachment rule matches zero services", "IoCTools.AutoDeps", "Info"),
            new("IOC103", "AutoDepsApplyGlob pattern is invalid", "IoCTools.AutoDeps", "Error"),
            new("IOC104", "Profile type is generic", "IoCTools.AutoDeps", "Error"),
            new("IOC105", "Redundant profile attachment", "IoCTools.AutoDeps", "Info"),
            new("IOC106", "AutoDepOpen requires single-arity unbound generic", "IoCTools.AutoDeps", "Error"),
            new("IOC107", "AutoDepOpen requires an unbound generic type", "IoCTools.AutoDeps", "Error"),
            new("IOC108", "AutoDepOpen closure violates type parameter constraint", "IoCTools.AutoDeps", "Error"),

            // IoCTools.Usage (1.6 — [Inject] deprecation)
            new("IOC095", "[Inject] is deprecated; use [DependsOn<T>]", "IoCTools.Usage", "Warning"),
        };
    }
}
