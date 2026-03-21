namespace IoCTools.Generator.Diagnostics;

internal static partial class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor SingletonDependsOnScoped = new(
        "IOC012",
        "Singleton service depends on Scoped service",
        "Singleton service '{0}' depends on Scoped service '{1}'. Singleton services cannot capture shorter-lived dependencies.",
        "IoCTools.Lifetime",
        DiagnosticSeverity.Error,
        true,
        "Fix the lifetime mismatch by: 1) Changing dependency '{1}' to [Singleton], 2) Changing this service to [Scoped] or [Transient], 3) Inject IServiceProvider and call CreateScope() to resolve '{1}' on demand, or 4) Use a factory delegate Func<{1}> to create instances per-use.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc012");

    public static readonly DiagnosticDescriptor SingletonDependsOnTransient = new(
        "IOC013",
        "Singleton service depends on Transient service",
        "Singleton service '{0}' depends on Transient service '{1}'. Consider if this transient should be Singleton or if the dependency is appropriate.",
        "IoCTools.Lifetime",
        DiagnosticSeverity.Warning,
        true,
        "Review the design: 1) If '{1}' should be shared, change it to [Singleton], 2) If truly transient, inject IServiceProvider and call CreateScope() to resolve '{1}' on demand, or 3) Use a factory delegate Func<{1}> to create a new instance each time it is needed.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc013");

    public static readonly DiagnosticDescriptor BackgroundServiceLifetimeValidation = new(
        "IOC014",
        "Background service with non-Singleton lifetime",
        "Background service '{0}' has {1} lifetime. Background services should typically be Singleton.",
        "IoCTools.Lifetime",
        DiagnosticSeverity.Error,
        true,
        "Fix options: 1) Change to [Singleton] for optimal background service lifetime, 2) Use [BackgroundService(SuppressLifetimeWarnings = true)] to suppress this warning if the current lifetime is intentional, or 3) Consider if this should inherit from BackgroundService at all.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc014");

    public static readonly DiagnosticDescriptor InheritanceChainLifetimeValidation = new(
        "IOC015",
        "Service lifetime mismatch in inheritance chain",
        "Service lifetime mismatch in inheritance chain: '{0}' ({1}) inherits from dependencies with {2} lifetime. Inheritance path: {3}",
        "IoCTools.Lifetime",
        DiagnosticSeverity.Error,
        true,
        "Fix the inheritance lifetime hierarchy by: 1) Making all services in the chain Singleton, 2) Changing consuming service to Scoped/Transient, or 3) Breaking the inheritance chain to avoid lifetime conflicts.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc015");

    public static readonly DiagnosticDescriptor RedundantScopedLifetimeAttribute = new(
        "IOC033",
        "Scoped lifetime attribute is redundant",
        "Class '{0}' is already implicitly registered as Scoped via {1}. Remove redundant [Scoped] attribute or change the lifetime to a non-default value.",
        "IoCTools.Lifetime",
        DiagnosticSeverity.Warning,
        true,
        "Scoped is the default lifetime for implicit services; only specify it when clarifying intent or when no other service indicators exist.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc033");

    public static readonly DiagnosticDescriptor RedundantSingletonLifetimeAttribute = new(
        "IOC059",
        "Singleton lifetime attribute is redundant",
        "Class '{0}' inherits [Singleton] from '{1}'. Remove redundant [Singleton] on the derived class.",
        "IoCTools.Lifetime",
        DiagnosticSeverity.Warning,
        true,
        "When a base class is already marked [Singleton], derived classes do not need to repeat the attribute unless changing lifetime.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc059");

    public static readonly DiagnosticDescriptor RedundantTransientLifetimeAttribute = new(
        "IOC060",
        "Transient lifetime attribute is redundant",
        "Class '{0}' inherits [Transient] from '{1}'. Remove redundant [Transient] on the derived class.",
        "IoCTools.Lifetime",
        DiagnosticSeverity.Warning,
        true,
        "When a base class is already marked [Transient], derived classes do not need to repeat the attribute unless changing lifetime.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc060");

    public static readonly DiagnosticDescriptor BaseClassLifetimeMismatch = new(
        "IOC075",
        "Inconsistent lifetimes across inherited services",
        "Base class '{0}' is inherited by IoCTools services with mixed or missing lifetimes: {1}. Move a single lifetime attribute to the base class and remove conflicting child lifetimes to align registrations.",
        "IoCTools.Lifetime",
        DiagnosticSeverity.Warning,
        true,
        "Place one lifetime attribute ([Scoped]/[Singleton]/[Transient]) on the shared base class so all derived IoCTools services register consistently, and drop duplicate child lifetimes.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc075");

    public static readonly DiagnosticDescriptor InheritedLifetimeRedundant = new(
        "IOC084",
        "Lifetime attribute duplicates inherited lifetime",
        "Class '{0}' declares lifetime '{1}' but already inherits the same lifetime from base class '{2}'. Remove the redundant lifetime attribute or change it to a different lifetime if intended.",
        "IoCTools.Lifetime",
        DiagnosticSeverity.Warning,
        true,
        "Avoid repeating the same lifetime attribute on derived classes when the base class already establishes the lifetime.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc084");

    public static readonly DiagnosticDescriptor TransientDependsOnScoped = new(
        "IOC087",
        "Transient service depends on Scoped service",
        "Transient service '{0}' depends on Scoped service '{1}'. Transient services resolved from the root scope cannot depend on Scoped services.",
        "IoCTools.Lifetime",
        DiagnosticSeverity.Error,
        true,
        "Fix the lifetime mismatch by: 1) Changing dependency '{1}' to [Singleton] or [Transient], 2) Changing this service to [Scoped], 3) Inject IServiceProvider and call CreateScope() to resolve '{1}' on demand, or 4) Use a factory delegate Func<{1}> to create instances per-use.",
        "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc087");
}
