# Attribute Reference

Complete reference for all IoCTools attributes with usage examples.

## Lifetime Attributes

### `[Scoped]`

Marks a service as scoped — created once per request/scope.

```csharp
[Scoped]
public partial class UserService : IUserService { }
```

**When to use:** Services that handle HTTP requests, units of work, or per-request data.

**Default:** Partial classes implementing interfaces are implicitly Scoped. Add `[Scoped]` explicitly only when overriding diagnostics.

**See also:** [IOC033](diagnostics.md#ioc033) (redundant Scoped warning)

---

### `[Singleton]`

Marks a service as singleton — created once for the application lifetime.

```csharp
[Singleton]
public partial class CacheService : ICacheService { }
```

**When to use:** Stateful services, caches, or stateless utilities where one instance serves all requests.

**Lifetime rules:** Singletons can only depend on other singletons (see [IOC012](diagnostics.md#ioc012)).

---

### `[Transient]`

Marks a service as transient — created each time it's requested.

```csharp
[Transient]
public partial class EmailValidator : IEmailValidator { }
```

**When to use:** Lightweight, stateless services with no shared state.

---

## Dependency Attributes

### `[DependsOn<T1, T2, ...>]`

Requests constructor parameters without declaring fields.

```csharp
[DependsOn<ILogger<UserService>, IUserRepository, IEmailService>]
public partial class UserService : IUserService
{
    // Fields auto-generated: _logger, _userRepository, _emailService
}
```

**Usage notes:**
- Preferred approach for declaring dependencies
- Constructor is generated with parameters for each type
- Field names follow naming convention (strip `I` prefix, camelCase)
- Use multiple `[DependsOn<>]` attributes for more than 8 dependencies
- Add `memberNames` parameter for custom names: `[DependsOn<...>(memberNames: "logger", "repo")]`

**See also:** [IOC035](diagnostics.md#ioc035) (Inject field matches default pattern)

---

### `[Inject]`

Last-resort marker for fields that must exist (custom naming, mutability).

```csharp
[Inject]
private readonly IMeter _meter; // Field genuinely reused across methods
```

**When to use:** Only when a field must be manually written (custom naming, mutable state, lazy caching).

**Diagnostic:** [IOC035](diagnostics.md#ioc035) warns when `[Inject]` matches the default `[DependsOn]` pattern.

---

### `IDependencySet` + `[DependsOn<TSet>]`

Reusable dependency bundles — declare once, use everywhere.

```csharp
// Define a set (metadata only, never registered)
[DependsOn<ILogger<PaymentInfra>>]
[DependsOn<IHttpClientFactory>]
[DependsOnConfiguration<string>("Billing:BaseUrl")]
public interface PaymentInfra : IDependencySet { }

// Consume it — dependencies flatten into constructor
[Scoped]
[DependsOn<PaymentInfra>]
public partial class BillingService : IBillingService
{
    // _logger, _httpClientFactory, _baseUrl auto-generated
}
```

**Rules:**
- Sets can inherit other sets (dependencies flatten recursively)
- Cycles are rejected (IOC050)
- Name collisions are errors (IOC051)
- Sets cannot be registered (IOC052)

**See also:** [IOC053](diagnostics.md#ioc053) (suggest new set), [IOC054](diagnostics.md#ioc054) (near-match reuse)

---

## Configuration Attributes

### `[DependsOnConfiguration<T>(string key, ...)]`

Declare configuration dependencies without writing backing fields.

```csharp
[DependsOnConfiguration<string>("Email:FromAddress", Required = true)]
[DependsOnConfiguration<int>("Email:RetryCount", DefaultValue = "3")]
[DependsOnConfiguration<EmailOptions>("Email", SupportsReloading = true)]
public partial class EmailService : IEmailService
{
    // _emailFromAddress, _emailRetryCount, _emailOptions auto-generated
}
```

**Parameters:**
- `key` / `configurationKeys`: Configuration key path (supports params-style)
- `Required`: Whether the key must exist (default: false)
- `DefaultValue`: Fallback value if key is missing
- `SupportsReloading`: Bind to `IOptionsSnapshot<T>` for hot reload (complex types only)
- `namingConvention`: Override global naming style
- `stripI`, `prefix`, `stripSettingsSuffix`: Field name customizations

**Best practice:** Use `[DependsOnConfiguration<>]` first — it generates fields, constructor parameters, and binding logic automatically.

---

### `[InjectConfiguration(string key, ...)]`

Last resort for configuration fields requiring manual control.

```csharp
[InjectConfiguration("Database:ConnectionString", Required = true)]
private readonly string _connectionString;
```

**When to use:** Only when you need a mutable field (lazy caching with `??=`, instrumentation).

**See also:** [IOC043](diagnostics.md#ioc043) (prefer DependsOnConfiguration)

---

### `[InjectConfigurationOptions]` / `[DependsOnOptions]`

Wires strongly-typed options classes once and reuses them.

```csharp
[DependsOnOptions<BillingOptions>]
public partial class BillingService : IBillingService
{
    // _billingOptions auto-generated as BillingOptions (not IOptions<T>)
}
```

**Generates:** `IOptionsMonitor<T>` wiring behind the scenes.

---

## Interface Control Attributes

### `[RegisterAs<T1, T2, ...>(InstanceSharing mode)]`

Register only selected interfaces, optionally sharing instances.

```csharp
[RegisterAs<IUserService, INotificationService>]
public partial class MyService : IUserService, INotificationService { }
// Generates: services.AddScoped<IUserService, MyService>();
//           services.AddScoped<INotificationService, MyService>();

[Singleton] // Explicit non-default lifetime
[RegisterAs<IUserService, INotificationService>(InstanceSharing.Shared)]
public partial class SharedService : IUserService, INotificationService { }
// Generates: services.AddScoped<SharedService>();
//           services.AddScoped<IUserService>(sp => sp.GetRequiredService<SharedService>());
//           services.AddScoped<INotificationService>(sp => sp.GetRequiredService<SharedService>());
```

**Modes:**
- `Separate` (default): Different instances per interface
- `Shared`: Same instance across all interfaces (factory pattern)

**EF Core DbContext pattern:**
```csharp
[RegisterAs<ITransactionService>(InstanceSharing.Shared)]
public partial class MyDbContext : DbContext, ITransactionService
{
    // No lifetime attribute — registered by AddDbContext
}
```

---

### `[RegisterAsAll(RegistrationMode mode, InstanceSharing mode)]`

Register every implemented interface automatically.

```csharp
[RegisterAsAll(RegistrationMode.All)] // Default: register all interfaces
public partial class MultiService : IServiceA, IServiceB, IServiceC { }

[RegisterAsAll(RegistrationMode.Exclusionary)]
[SkipRegistration<IServiceC>] // Exclude specific interfaces
public partial class SelectiveService : IServiceA, IServiceB, IServiceC { }

[RegisterAsAll(RegistrationMode.DirectOnly)] // Register concrete type only
public partial class ConcreteOnly { }
```

**Modes:**
- `All`: Register all interfaces (default)
- `Exclusionary`: Register all except `[SkipRegistration<>]` targets
- `DirectOnly`: Register concrete type only, skip interfaces

---

### `[SkipRegistration]` / `[SkipRegistration<T>]`

Disable registration completely or exclude individual interfaces.

```csharp
[SkipRegistration] // This class is not registered
public partial class ManualService { }

[RegisterAsAll]
[SkipRegistration<IServiceInternal>] // Exclude specific interface
public partial class PublicService : IPublicService, IServiceInternal { }
```

**See also:** [IOC005](diagnostics.md#ioc005), [IOC009](diagnostics.md#ioc009), [IOC038](diagnostics.md#ioc038)

---

## Conditional Attributes

### `[ConditionalService(Environment, NotEnvironment, ConfigValue, Equals, NotEquals)]`

Register only when environment or configuration matches.

```csharp
[ConditionalService(Environment = "Production,Staging")]
public partial class ProductionService : IProductionService { }

[ConditionalService(ConfigValue = "Feature:NewCheckoutEnabled", Equals = "true")]
public partial class NewCheckoutService : INewCheckoutService { }
```

**Parameters:**
- `Environment` / `NotEnvironment`: Comma-separated environment names
- `ConfigValue`: Configuration key path to check
- `Equals` / `NotEquals`: Value comparison for ConfigValue

**Requires:** A lifetime attribute ([IOC021](diagnostics.md#ioc021))

---

## Advanced Attributes

### `[ManualService]` / `[ExternalService]`

Mark services or dependencies that are registered manually.

```csharp
[ExternalService] // Keeps analyzers quiet
private readonly IThirdPartyService _externalService;
```

**Use when:** DI is handled elsewhere (third-party container, framework registration, etc.)

---

## Naming and Member Names

### Default Field Name Generation

IoCTools strips `I` prefix, applies naming convention, prefixes with `_`:

| Interface | Field Name (CamelCase) | Field Name (SnakeCase) |
|-----------|----------------------|----------------------|
| `IEmailService` | `_emailService` | `_email_service` |
| `IDetailedInvoiceAuditor` | `_detailedInvoiceAuditor` | `_detailed_invoice_auditor` |

### Custom Names

```csharp
// Params-style (preferred)
[DependsOn<ILogger<BillingService>, IHttpClientFactory>(
    memberNames: "logger", "client")]

// Legacy named argument
[DependsOn<ILogger<BillingService>, IHttpClientFactory>(
    MemberNames = new[] { "logger", "client" })]
```

**See also:** [IOC047](diagnostics.md#ioc047) (use params-style), [IOC085](diagnostics.md#ioc085) (redundant name)

---

## Related

- [Getting Started](getting-started.md) — Tutorial introduction
- [Diagnostics Reference](diagnostics.md) — All diagnostic codes
- [Configuration](configuration.md) — MSBuild properties and settings
