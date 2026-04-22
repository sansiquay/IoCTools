# Configuration

IoCTools reads configuration from MSBuild properties, `.editorconfig` files, and an optional `IoCTools.Generator.Configuration.GeneratorOptions` class.

## MSBuild Properties

Configure via `.csproj`, `Directory.Build.props`, or `.editorconfig`:

### Diagnostic Severity Override

Control analyzer severity per diagnostic category:

```xml
<PropertyGroup>
  <!-- Missing implementation diagnostics (IOC001, IOC002) -->
  <IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>

  <!-- Manual registration diagnostics (IOC081-IOC086, IOC090-IOC095) -->
  <IoCToolsManualSeverity>Warning</IoCToolsManualSeverity>

  <!-- Lifetime validation diagnostics (IOC012, IOC013, IOC015, IOC087) -->
  <IoCToolsLifetimeValidationSeverity>Error</IoCToolsLifetimeValidationSeverity>
</PropertyGroup>
```

**Severity options:** `Error`, `Warning`, `Info`, `Hidden`

### Disabling Diagnostics

```xml
<PropertyGroup>
  <!-- Disable all IoCTools diagnostics (not recommended) -->
  <IoCToolsDisableDiagnostics>true</IoCToolsDisableDiagnostics>

  <!-- Disable only lifetime validation -->
  <IoCToolsDisableLifetimeValidation>true</IoCToolsDisableLifetimeValidation>
</PropertyGroup>
```

**Use case:** Temporary suppression during large refactors.

### Service Filtering

Control which service types are registered:

```xml
<PropertyGroup>
  <!-- Skip ControllerBase (default) + additional patterns -->
  <IoCToolsSkipAssignableTypesAdd>Mediator.*;MediatR.*;MassTransit.*</IoCToolsSkipAssignableTypesAdd>

  <!-- Carve exceptions back into skipped types -->
  <IoCToolsSkipAssignableExceptions>Namespace.ImportantService</IoCToolsSkipAssignableExceptions>
</PropertyGroup>
```

**Default:** Skips ASP.NET `ControllerBase` only.

### Cross-Assembly Interface Patterns

Ignore IOC001/IOC002 for interfaces in separate assemblies (clean architecture):

```xml
<PropertyGroup>
  <IoCToolsIgnoredTypePatterns>*.Abstractions.*;*.Contracts.*;*.Interfaces.*</IoCToolsIgnoredTypePatterns>
</PropertyGroup>
```

**Default:** `*.Abstractions.*;*.Contracts.*;*.Interfaces.*;*.ILoggerService<`

### Default Service Lifetime

Change the implicit lifetime applied to services without explicit attributes:

```xml
<PropertyGroup>
  <IoCToolsDefaultServiceLifetime>Singleton</IoCToolsDefaultServiceLifetime>
</PropertyGroup>
```

**Options:** `Scoped` (default), `Singleton`, `Transient`

**Note:** Both generated registrations and IOC012/IOC013 diagnostics honor this setting.

---

### Auto-Deps Configuration (1.6.0+)

MSBuild properties for the auto-deps feature ([docs/auto-deps.md](auto-deps.md))
are **override-only** — they modulate behavior but never declare auto-deps.
Declarations live in assembly attributes (`[assembly: AutoDep<T>]`,
`[assembly: AutoDepOpen(...)]`, etc.).

```xml
<PropertyGroup>
  <!-- Kill switch: disables the entire auto-deps feature -->
  <IoCToolsAutoDepsDisable>false</IoCToolsAutoDepsDisable>

  <!-- Namespace glob; matching services are treated as [NoAutoDeps] -->
  <IoCToolsAutoDepsExcludeGlob></IoCToolsAutoDepsExcludeGlob>

  <!-- When true, generated files include a comment block listing resolved auto-deps and sources -->
  <IoCToolsAutoDepsReport>false</IoCToolsAutoDepsReport>

  <!-- When false, disables built-in Microsoft.Extensions.Logging.ILogger<T> auto-detection -->
  <IoCToolsAutoDetectLogger>true</IoCToolsAutoDetectLogger>

  <!-- Modulates IOC095 severity during [Inject] migration window -->
  <IoCToolsInjectDeprecationSeverity>Warning</IoCToolsInjectDeprecationSeverity>
</PropertyGroup>
```

**Severity options** for `IoCToolsInjectDeprecationSeverity`: `Error`,
`Warning` (default in 1.6), `Info`, `Hidden`. In 1.7 the override is
raise-only.

**Assembly-attribute configuration is the primary surface.** Declare auto-deps
in code:

```csharp
// Universal auto-deps for this assembly
[assembly: AutoDep<TimeProvider>]
[assembly: AutoDepOpen(typeof(ILogger<>))]

// Cross-assembly policy: library publishes defaults to consumers
[assembly: AutoDep<ITracer>(Scope = AutoDepScope.Transitive)]
```

**Caveat.** Declaring `[assembly: AutoDep<IUnregistered>]` where the type
has no DI registration fires `IOC001` on every service in the assembly — the
blast radius is intentional (a broken auto-dep should be loud) but worth
knowing before declaring.

For the full auto-deps reference see [docs/auto-deps.md](auto-deps.md).

---

## `.editorconfig` Configuration

Configure in `.editorconfig` for IDE-wide settings:

```ini
[*.cs]
# Diagnostic severity
build_property.IoCToolsNoImplementationSeverity = error
build_property.IoCToolsManualSeverity = warning

# Service filtering
build_property.IoCToolsSkipAssignableTypesAdd = Mediator.*;MediatR.*

# Default lifetime
build_property.IoCToolsDefaultServiceLifetime = Scoped
```

---

## Code-Based Configuration (GeneratorOptions)

Configure via code when MSBuild is inconvenient:

```csharp
using IoCTools.Generator.Configuration;

namespace IoCTools.Generator.Configuration;

public static class GeneratorOptions
{
    public static GeneratorStyleOptions Current => new(
        skipAssignableTypesUseDefaults: new[]
        {
            typeof(ControllerBase) // Default skip
        },
        skipAssignableTypesAdd: new[]
        {
            typeof(Mediator),
            typeof(IRequestHandler<>), // MediatR
            typeof(IConsumer<>) // MassTransit
        },
        skipAssignableExceptions: Array.Empty<Type>(),
        ignoredTypePatterns: new[]
        {
            "*.Abstractions.*",
            "*.Contracts.*",
            "*.Interfaces.*"
        },
        defaultServiceLifetime: ServiceLifetime.Scoped
    );
}
```

**Requirements:**
- Define in `IoCTools.Generator.Configuration` namespace
- Class name must be `GeneratorOptions`
- Must have `public static GeneratorStyleOptions Current { get; }` property

---

## Diagnostic Severity Reference

| MSBuild Property | Affects Diagnostics | Default |
|------------------|-------------------|---------|
| `IoCToolsNoImplementationSeverity` | IOC001, IOC002 | Error |
| `IoCToolsManualSeverity` | IOC081-IOC086, IOC090-IOC095 | Info |
| `IoCToolsLifetimeValidationSeverity` | IOC012, IOC013, IOC015, IOC087 | Error |
| `IoCToolsInjectDeprecationSeverity` | IOC095 (`[Inject]` deprecation, 1.6.0+) | Warning (1.6), Error (1.7) |

[See all diagnostics](diagnostics.md)

---

## Platform Constraints

The IoCTools generator targets `netstandard2.0` for broad compatibility. This constrains **the generator internally**, NOT your service code.

**Your service code** can use:
- Any .NET version (Framework 4.6.1+, .NET Core 2.0+, .NET 5+)
- Any C# features your framework supports
- Any language version

**The generator** is limited to:
- `netstandard2.0` APIs (no records, init-only properties, required members)
- C# language features available in netstandard2.0

[Full platform constraints documentation](platform-constraints.md)

---

## Related

- [Diagnostics Reference](diagnostics.md) — Core diagnostics through `IOC095`, plus testing and FluentValidation guidance
- [Platform Constraints](platform-constraints.md) — netstandard2.0 limitations and workarounds
- [Attribute Reference](attributes.md) — Complete attribute documentation
