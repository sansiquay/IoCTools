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
