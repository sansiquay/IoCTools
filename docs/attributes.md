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

### `[Inject]` — **deprecated in 1.6.0**

`[Inject]` is marked `[Obsolete]` in 1.6.0 and fires
[IOC095](diagnostics.md#ioc095-primary-160--inject-is-deprecated) at warning
severity. Timeline:

- **1.6.x** — warning. Roslyn code fix and `ioc-tools migrate-inject` ship.
- **1.7.0** — error. `IOC095` defaults to error severity.
- **2.0.0** — `InjectAttribute` is removed.

Migrate with one of:

- IDE: accept the IOC095 quick-fix light-bulb per field.
- CLI (solution-wide): `ioc-tools migrate-inject --dry-run` to preview,
  then `ioc-tools migrate-inject` to apply.

See the [1.5.x → 1.6.x migration guide](migration.md#migrating-from-15x-to-16x)
for recommended workflow.

**Migration target:** `[DependsOn<T>]` on the class (replaces the per-field
declaration). Custom field names round-trip via `memberName1..N`; the
`[ExternalService]` flag round-trips via `external: true` on `DependsOn`.

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

Compatibility-only escape hatch for legacy configuration fields requiring manual control.

```csharp
[InjectConfiguration("Database:ConnectionString", Required = true)]
private readonly string _connectionString;
```

**When to use:** Never in new code. Prefer `[DependsOnConfiguration<T>]` or `[DependsOnOptions<T>]`. Keep `InjectConfiguration` only while migrating legacy code that cannot move immediately.

**See also:** [IOC043](diagnostics.md#ioc043) (prefer DependsOnConfiguration)

---

### `[DependsOnOptions]`

Wires strongly-typed options classes once and reuses them.

```csharp
[DependsOnOptions<BillingOptions>]
public partial class BillingService : IBillingService
{
    // _billingOptions auto-generated as BillingOptions (not IOptions<T>)
}
```

**Generates:** `IOptionsMonitor<T>` wiring behind the scenes.

**Preferred over:** `InjectConfiguration` for strongly typed options in new code.

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
// Generates: services.AddSingleton<SharedService>();
//           services.AddSingleton<IUserService>(sp => sp.GetRequiredService<SharedService>());
//           services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<SharedService>());
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

**Open generics:** The common `1.5.1` path is supported:

```csharp
[Scoped]
[RegisterAsAll]
public partial class Repository<T> : IRepository<T> where T : class { }
```

If you request `InstanceSharing.Shared` for open-generic interface aliases, IoCTools reports [IOC095](diagnostics.md#ioc095) and falls back to separate registrations because `Microsoft.Extensions.DependencyInjection` does not support open-generic implementation factories.

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

## Auto-Deps Attributes (1.6.0+)

Auto-deps let you declare ambient dependencies once — at assembly scope,
in a profile, or transitively across projects — so every matching service
receives them without repeating the declaration. See
[docs/auto-deps.md](auto-deps.md) for the full reference and worked recipes.

### `[assembly: AutoDep<T>]`

Universal closed-type auto-dep. Every service the generator processes in the
assembly receives `T` as a constructor parameter.

```csharp
[assembly: AutoDep<TimeProvider>]
[assembly: AutoDep<IMetrics>]
```

Supports `Scope = AutoDepScope.Transitive` to propagate to consumers of the
declaring assembly.

### `[assembly: AutoDepOpen(typeof(T<>))]`

Universal single-arity open-generic auto-dep. The generator closes the
unbound type with the concrete service type at codegen — applied to
`OrderController`, `AutoDepOpen(typeof(ILogger<>))` yields
`ILogger<OrderController>`.

```csharp
[assembly: AutoDepOpen(typeof(ILogger<>))]
```

The `typeof()` is the unavoidable exception to "generics all the way down"
because C# does not permit unbound generics as type arguments to generic
attributes. Multi-arity (`typeof(IFoo<,>)`) is rejected with
[IOC100](diagnostics.md#ioc100--autodepopen-on-multi-arity-generic).

**Built-in `ILogger<T>` detection.** When
`Microsoft.Extensions.Logging.ILogger<T>` is discoverable in the compilation,
the generator behaves as if a universal `AutoDepOpen(typeof(ILogger<>))`
had been declared. No user config required. Disable with
`<IoCToolsAutoDetectLogger>false</IoCToolsAutoDetectLogger>`.

### `[assembly: AutoDepIn<TProfile, T>]`

Add `T` to a named profile:

```csharp
public sealed class ControllerDefaults : IAutoDepsProfile { }

[assembly: AutoDepIn<ControllerDefaults, IMediator>]
[assembly: AutoDepIn<ControllerDefaults, IMapper>]
```

Profile types must implement `IAutoDepsProfile` (an empty marker interface)
and must be non-generic. Missing marker fires
[IOC097](diagnostics.md#ioc097--profile-missing-iautodepsprofile-marker);
generic profile fires [IOC104](diagnostics.md#ioc104--profile-type-is-generic).

### `[assembly: AutoDepsApply<TProfile, TBase>]`

Attach a profile to every service whose base class or implemented interface
is `TBase`:

```csharp
[assembly: AutoDepsApply<ControllerDefaults, ControllerBase>]
[assembly: AutoDepsApply<BackgroundDefaults, BackgroundService>]
```

### `[assembly: AutoDepsApplyGlob<TProfile>("pattern")]`

Attach a profile by namespace glob:

```csharp
[assembly: AutoDepsApplyGlob<AdminDefaults>("*.Admin.Controllers.*")]
```

**Library-author note:** When this attribute carries `Scope =
AutoDepScope.Transitive`, the glob evaluates against service namespaces in
the *consuming* assembly, not the declaring one. Prefer broad
convention-based patterns (`"*.Controllers.*"`) over assembly-specific ones.
Invalid patterns fire [IOC103](diagnostics.md#ioc103--invalid-glob-pattern).

### `[AutoDeps<TProfile>]`

Explicitly attach a profile to one service:

```csharp
[Scoped]
[AutoDeps<ReportingDefaults>]
public partial class ReportService { }
```

### `[NoAutoDeps]` / `[NoAutoDep<T>]` / `[NoAutoDepOpen(typeof(T<>))]`

Opt-out ladder for a service:

```csharp
[NoAutoDeps]                             // disable all auto-deps for this service
[NoAutoDep<TimeProvider>]                // disable one closed-type auto-dep
[NoAutoDepOpen(typeof(ILogger<>))]       // disable any auto-dep from an open-generic shape
[Scoped] public partial class LegacyService { }
```

`NoAutoDepOpen` is the rename-safe twin of `AutoDepOpen` — it suppresses by
shape regardless of closure and regardless of source (built-in, universal,
or transitive). Stale opt-outs fire
[IOC096](diagnostics.md#ioc096--stale-opt-out).

### `IAutoDepsProfile`

Empty marker interface identifying a type as a profile. Lives in
`IoCTools.Abstractions` at netstandard2.0 — declaring profiles never forces
consumers onto a newer target framework.

### `AutoDepScope` enum

Used as the `Scope` property on `AutoDep<T>`, `AutoDepOpen`,
`AutoDepIn<TProfile, T>`, `AutoDepsApply<TProfile, TBase>`, and
`AutoDepsApplyGlob<TProfile>`:

| Value | Meaning |
|---|---|
| `AutoDepScope.Assembly` | (Default) Policy applies only to services in the declaring assembly. |
| `AutoDepScope.Transitive` | Policy also applies to services in consuming assemblies. Consumer opt-outs always win. |

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
