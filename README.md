# IoCTools

[![NuGet](https://img.shields.io/nuget/v/IoCTools.Abstractions?label=IoCTools.Abstractions)](https://www.nuget.org/packages/IoCTools.Abstractions)
[![NuGet](https://img.shields.io/nuget/v/IoCTools.Generator?label=IoCTools.Generator)](https://www.nuget.org/packages/IoCTools.Generator)

> A Roslyn source generator that lets each service declare its own lifetime, dependencies, and registration intent with
> small attributes. IoCTools emits constructors, service registrations, and analyzers at build time—no reflection, no
> runtime scanning.

## Highlights

- **Self-describing services** – `[Scoped]`, `[DependsOn<T>]`, `[DependsOnConfiguration<…>]`, `[RegisterAs<…>]`, and
  `[ConditionalService]` live on the class, so intent never leaves the type. Use `[DependsOnConfiguration<…>]` for
  configuration whenever possible; fall back to `[InjectConfiguration]` only when you truly need a hand-authored field.
- **Dependency sets without drilling** – Implement `IDependencySet` on an interface/class and list `[DependsOn]`/
  `[DependsOnConfiguration]` there. Anywhere else, a plain `[DependsOn<MySet>]` flattens every dependency from the set (
  and its ancestor sets) directly into the consuming service’s generated constructor/fields—no `set.` dereferences, no
  runtime object. Cycles are rejected, lifetimes are validated across the expanded set, and origin info is preserved for
  diagnostics/CLI output.
- **Accurate registrations** – The generator produces `Add<YourAssembly>RegisteredServices()` extensions that register
  concrete types, interfaces, options bindings, conditional services, and background workers.
- **Inheritance-aware lifetimes** – Derived services inherit the base class lifetime automatically for registrations and
  diagnostics. Redundant lifetime warnings respect inheritance (no false “missing” when a base is `[Scoped]`), and
  an explicit `[Scoped]` is flagged as redundant (IOC033) whenever it matches the implicit default lifetime.
  conflicting lifetimes across the chain trigger IOC015.
- **Analyzer coverage** – 84 diagnostics (IOC001–IOC086) keep registrations honest: missing lifetimes, redundant
  `RegisterAs`, conflicting `[SkipRegistration]`, invalid config keys, singleton/scoped mismatches, manual-constructor
  mixing with IoCTools dependencies, options misuse, primitive/collection dependency bans, redundant/unused
  dependencies (including configuration), overlapping options/config sections, dependency set validation,
  inheritance-based redundancy detection, hosted service lifetime validation, and framework dependency recognition.
- **Zero reflection** – Everything happens at compile time. Startup cost stays flat, and generated code is plain C# you
  can inspect.

IoCTools treats each service class as the single source of truth for its registration story. Lifetimes, interface
exposure, configuration needs, and conditional flags live beside the implementation, so setup code isn’t forced to guess
or duplicate those concerns. This separation keeps startup/bootstrap files lean while ensuring services are designed
with the lifetime/registration model they actually require.

## Installation

```bash
dotnet add package IoCTools.Abstractions
dotnet add package IoCTools.Generator --prerelease
```

Or directly in your project file:

```xml
<ItemGroup>
  <PackageReference Include="IoCTools.Abstractions" Version="*" />
  <PackageReference Include="IoCTools.Generator" Version="*" PrivateAssets="all" />
</ItemGroup>
```

## Getting Started in Three Steps

1. **Annotate a partial service**

   ```csharp
   [DependsOn<ILogger<EmailService>>] // Scoped implied when DependsOn/Inject/etc. are present (IOC033 otherwise)
   public partial class EmailService : IEmailService
   {
       public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
   }
   ```

   _Tip:_ Any partial class that implements at least one interface is treated as a scoped service even without
  `[Scoped]`. Add `[Singleton]`/`[Transient]` (or `[Scoped]` when you truly need to override diagnostics) only when you
  want to change that default. You can change the implicit lifetime globally via
   `build_property.IoCToolsDefaultServiceLifetime`, and both the generated registrations and IOC012/IOC013 diagnostics
   honor whatever value you pick.

2. **Build** – IoCTools emits `Add<YourAssembly>RegisteredServices()` into `<AssemblyName>.Extensions.Generated`.

3. **Call the extension during startup**

   ```csharp
   using YourAssembly.Extensions.Generated;

   var builder = WebApplication.CreateBuilder(args);
   builder.Services.AddYourAssemblyRegisteredServices(builder.Configuration);
   ```

## IoCTools CLI

`IoCTools.Tools.Cli` ships as a dotnet global/local tool (`dotnet ioc-tools …`). It interrogates your project with the
real IoCTools generator, so you can see exactly what the build produced without spelunking through `obj/`.

### Installation

```bash
# From the repo root
dotnet pack IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj -c Release -o ./artifacts

# Install globally or add to a local manifest
dotnet tool install --global --add-source ./artifacts IoCTools.Tools.Cli
# or
dotnet new tool-manifest
dotnet tool install --add-source ./artifacts IoCTools.Tools.Cli
```

### Commands

| Command                                                                  | What it surfaces                                                                                                                                                        |
|--------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `fields --project <csproj> --file <class.cs> [--type Namespace.Service] [--source]` | Lists IoCTools-aware services in a file (or filtered types), showing generated `[DependsOn]` and `[DependsOnConfiguration]` fields, inferred names, and external flags. With `--source`, outputs the generated constructor source code. |
| `fields-path --project … --file … --type … [--output <dir>]`             | Emits the absolute path to the generated constructor `.g.cs` (defaults to `%TEMP%/IoCTools.Tools.Cli/<project>/<timestamp>` unless `--output` overrides).               |
| `services --project <csproj> [--output <dir>] [--source] [--type …]`     | Summaries of generated registration extension: lifetimes, interface/implementation pairings, factories, conditionals, and configuration bindings. With `--source`, outputs the raw generated source code. `--type` filters to specific service registrations. |
| `services-path --project <csproj> [--output <dir>]`                      | Prints the path to the generated registration extension so you can open or diff the raw file.                                                                           |
| `explain --project <csproj> --type Namespace.Service`                    | Explains a single service: generated dependency fields, config bindings (keys/required/reload), and external flags.                                                     |
| `graph --project <csproj> [--type …] [--format json                      | puml                                                                                                                                                                    |mermaid] [--output <dir>]` | Emits a lightweight graph of service registrations (service → implementation edges) in JSON/PlantUML/Mermaid. Optional `--type` filters the graph. |
| `why --project <csproj> --type … --dependency Fully.Qualified.Type`      | Shows which generated field/config binding matches the requested dependency for a service.                                                                              |
| `doctor --project <csproj> [--fixable-only]`                             | Runs the generator and prints diagnostics with locations; `--fixable-only` filters to warnings/infos (no auto-fix scripts yet).                                         |
| `compare --project <csproj> --output <dir> [--baseline <dir>]`           | Captures current generated artifacts into `--output`; if `--baseline` is provided, lists changed `.g.cs` files relative to that snapshot.                               |
| `profile --project <csproj> [--type …]`                                  | Prints generator warm/analysis timing for the project (type filter currently informational only).                                                                       |
| `config-audit --project <csproj> [--settings appsettings.json]`          | Lists required config bindings from IoCTools services and reports which keys are missing from an optional `appsettings.json`.                                           |

By default the CLI copies generator artifacts into your system temp directory under
`IoCTools.Tools.Cli/<project>/<timestamp>`, so running it against other repositories will never dirty their working
trees. Specify `--output` when you need the artifacts copied into a deterministic location.

Key switches: `--configuration` (default `Debug`), `--framework` (for multi-targeting), `--type` (can repeat),
`--output` (deterministic artifact directory), and `--source` (output raw generated code instead of summaries). Because
the CLI drives Roslyn directly, it works immediately even when IoCTools is referenced as a project dependency and
`EmitCompilerGeneratedFiles` is disabled.

## Before & After: Replacing DI Smells

### Legacy `BillingService` (manual, brittle)

```csharp
public class LegacyBillingService : IBillingService, ILegacyDiagnostics
{
    private readonly ILogger<LegacyBillingService> _logger;
    private readonly IHttpClientFactory _httpClients;
    private readonly BillingOptions _options;
    private readonly IConfiguration _config;

    public LegacyBillingService(
        ILogger<LegacyBillingService> logger,
        IHttpClientFactory httpClients,
        IOptionsMonitor<BillingOptions> options,
        IConfiguration config)
    {
        _logger = logger;
        _httpClients = httpClients;
        _options = options.CurrentValue;
        _config = config;
    }

    public async Task ChargeAsync(BillingRequest request)
    {
        var baseUrl = _config["Billing:BaseUrl"] ?? throw new InvalidOperationException("Billing options missing");
        // ... manual retry logic based on _options.RetryCount ...
    }
}

services.AddHttpClient();
services.AddSingleton<IOptionsMonitor<BillingOptions>, BillingOptionsMonitor>();
services.Configure<BillingOptions>(configuration.GetSection("Billing"));
services.AddScoped<IBillingService, LegacyBillingService>();
services.AddScoped<ILegacyDiagnostics, LegacyBillingService>();
```

Problems: duplicated registrations, runtime config lookups every call, manual interface wiring, no analyzer guardrails.

### IoCTools `BillingService` (attributes, analyzers, generated DI)

```csharp
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

[Scoped]
[RegisterAs<IBillingService, IBillingDiagnostics>(InstanceSharing.Shared)]
[ConditionalService(Environment = "Production,Staging")]
[DependsOn<ILogger<BillingService>, IHttpClientFactory, IClock>]
[SkipRegistration<ILegacyDiagnostics>] // Keep internal interface private to DI
public partial class BillingService : IBillingService, IBillingDiagnostics, ILegacyDiagnostics
{
    [InjectConfiguration("Billing:BaseUrl", Required = true)] private readonly string _baseUrl;
    [InjectConfiguration("Billing:RetryCount", DefaultValue = "3")] private readonly int _retryCount;
    [Inject] private readonly IMeter<BillingService> _meter; // field genuinely reused across methods

    public async Task ChargeAsync(BillingRequest request)
    {
        using var client = _httpClientFactory.CreateClient("billing");
        // Generated constructor supplies _logger, _httpClientFactory, _clock, and configuration fields.
    }
}
```

Generated code now:

- Creates a constructor that accepts `ILogger<BillingService> logger`, `IHttpClientFactory httpClientFactory`, and
  `IClock clock` (from `[DependsOn<…>]`).
- Injects `_meter`, `_baseUrl`, and `_retryCount` per attribute metadata.
- Registers the service once via `builder.Services.AddYourAssemblyRegisteredServices(configuration);` – IoCTools emits
  `services.AddScoped<IBillingService, BillingService>()` plus shared-instance factory wiring for `IBillingDiagnostics`.
- Configuration-backed types are auto-bound: any `[InjectConfiguration]` / `[DependsOnConfiguration]` (or `IOptions<T>`
  dependency) gets `AddOptions<T>().Bind(configuration.GetSection("…"))` and a singleton `T` via
  `TryAddSingleton(sp => sp.GetRequiredService<IOptions<T>>().Value)`, so manual options boilerplate isn’t needed.
- Emits diagnostics if `[Scoped]` becomes redundant (IOC033), if `[SkipRegistration<ILegacyDiagnostics>]` can never
  trigger (IOC009/IOC038), or if configuration keys are invalid (IOC016–IOC017).

### Class-level configuration with `[DependsOnConfiguration]`

```csharp
using IoCTools.Abstractions.Annotations;

[DependsOnConfiguration<string>("Billing:BaseUrl")]
[DependsOnConfiguration<int>("Billing:RetryCount", SupportsReloading = true)]
public partial class BillingService : IBillingService
{
    // IoCTools generates the `_baseUrl` and `_retryCount` fields plus binding logic.
}
```

Use this attribute when you want the analyzer/generator to manage configuration fields for you. You still get
`[InjectConfiguration]` semantics—required/default values, reload support, options binding—plus `[DependsOn]`-style
naming controls and multi-slot declarations.

> **Best practice:** Always reach for `[DependsOnConfiguration<…>]` first so the generator owns the field, naming, and
> binding logic. Resort to `[InjectConfiguration]` only when you need a mutable/manual field (for example, lazy caching
> with `??=` or instrumentation).

### Reusable dependency sets (flattened, natural syntax)

```csharp
// Define a set – metadata only, never registered
[DependsOn<ILogger<PaymentInfra>>]
[DependsOn<IHttpClientFactory>]
[DependsOnConfiguration<string>("Billing:BaseUrl")]
public interface PaymentInfra : IDependencySet {}

// Consume it – dependencies are flattened into ctor/fields, no drilling
[Scoped]
[DependsOn<PaymentInfra>] // expands logger, httpClientFactory, baseUrl
public partial class BillingService : IBillingService
{
    public Task ChargeAsync() { /* use _logger, _httpClientFactory, _baseUrl directly */ return Task.CompletedTask; }
}

// Inheritance works; ancestors are included and deduped
public interface CoreInfra : IDependencySet
{
    [DependsOn<IMeter<CoreInfra>>]
}

public interface ReportingInfra : CoreInfra
{
    [DependsOn<IClock>]
}

[Scoped]
[DependsOn<ReportingInfra>] // brings IMeter + IClock + any ReportingInfra deps
public partial class ReportingService {}
```

Rules and behavior:

- Implement `IDependencySet` on any interface/class to mark it as a bundle. Sets can inherit other sets; dependencies (
  services + configuration) from the entire chain are flattened and deduped.
- Consumers keep the natural form `[DependsOn<MySet>]`; when the target implements `IDependencySet`, the generator
  expands the set instead of expecting a DI registration.
- Lifetime safety: the set’s effective lifetime is the most restrictive of its members. Singleton consumers with
  scoped/transient members still trigger IOC012/IOC013.
- Cycles are rejected (e.g., `SetA -> SetB -> SetA`), and name collisions are errors unless type and member name match
  exactly.
- External/config metadata is preserved; CLI `fields` output labels each dependency with its originating set for
  readability.
- Flattening is fully recursive—sets can reference other sets. Member-name overrides flow through recursion, and
  conflicting names on the same dependency type surface IOC051.
- DRY nudges include configuration slots: IOC053–IOC055 consider both services and config bindings when proposing new
  sets, near-misses, or shared-base extraction.
- Optional future knobs (carrier injection, naming overrides) can hang off `[DependsOn<Set>(…)]` when the target is a
  set, without changing the default flat experience.

Refactor suggestions (info-level analyzers):

- **DRY new set** (IOC053): when ≥3 dependencies recur on ≥2 services (unordered match), suggest extracting an
  `IDependencySet`, generating it, and replacing the matching `[DependsOn]` blocks with `[DependsOn<NewSet>]` in all
  those services.
- **Near-miss reuse** (IOC054): if a service already has most of an existing set (e.g., 4/5 deps), suggest adopting the
  set plus adding the missing dep locally, or splitting a core set for broader reuse. Shown as a quick-fix preview.
- **Shared base refactor** (IOC055): when services that share a base type also share a dependency cluster, suggest
  moving the deps into a new set (or the base when appropriate) to reduce duplication while respecting lifetime rules.

## Naming & Generated Surface

### Dependency name derivation

IoCTools strips a leading `I` from interface names, applies the configured naming style, and prefixes fields with `_` by
default. When SnakeCase is enabled it uses:

```csharp
Regex.Replace(fieldBaseName, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
```

So `IEmailService` → `_emailService`, `IDetailedInvoiceAuditor` → `_detailedInvoiceAuditor`, and with snake_case
`_detailed_invoice_auditor`. Diagnostics IOC035 fire when an `[Inject]` field matches this auto-generated pattern,
nudging you back to `[DependsOn<…>]` when a field isn’t required.

Need a hand-authored name? Every `[DependsOn<…>]` overload now offers params-style `memberNames`, so you can write
`[DependsOn<ILogger<BillingService>, IHttpClientFactory>(memberNames: "logger", "client")]` instead of setting
`MemberNames = new[] { … }` on the attribute. Likewise, `[DependsOnConfiguration<…>]` keeps params-style
`configurationKeys` on its constructors, now alongside the naming options (`namingConvention`, `stripI`, `prefix`,
`stripSettingsSuffix`).

If you still use the legacy `MemberNames`/`ConfigurationKeys` named arguments, the analyzer now emits an informational
hint (IOC047) pointing to the params-friendly form.

Nullable dependencies are treated as misconfigurations: IOC048 warns when constructor-surface dependencies (including
`[DependsOn]`, `[Inject]`, and `[DependsOnConfiguration]`) are declared nullable—register a no-op implementation or
refactor the feature instead of using nullable dependency types.

### Generated registration extension

For assembly `IoCTools.Sample` the generator emits:

```csharp
namespace IoCTools.Sample.Extensions.Generated;

public static class GeneratedServiceCollectionExtensions
{
    public static IServiceCollection AddIoCToolsSampleRegisteredServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // registrations, configuration bindings, AddHostedService calls, etc.
        return services;
    }
}
```

- **Namespace**: `<AssemblyNameWithoutInvalidChars>.Extensions.Generated`.
- **Method name**: `Add<SafeAssemblyName>RegisteredServices` (periods removed, hyphens/spaces replaced with `_`).
- Bring the namespace into scope (`using YourAssembly.Extensions.Generated;`) and call the method from `Program.cs`.
  Background services, conditional registrations, and configuration bindings flow through that single call.
- The generator adds the `IConfiguration` parameter only when a project actually uses `[InjectConfiguration]` or
  `[ConditionalService]`—otherwise the extension is
  `AddYourAssemblyRegisteredServices(this IServiceCollection services)`.

## Attribute Reference

| Attribute                                                                                     | Category          | When to use                                                           | Notes                                                                                                                                                                                                                                                           |
|-----------------------------------------------------------------------------------------------|-------------------|-----------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `[Scoped]`, `[Singleton]`, `[Transient]`                                                      | Lifetime          | Declare how the service is registered.                                | Services own their lifetimes so startup code doesn’t. Use **one** per class; IOC036 warns otherwise. Scoped is implicit for partial classes that implement interfaces or when other service indicators exist.                                                   |
| `[DependsOn<T1, T2, …>]`                                                                      | Dependencies      | Request constructor parameters without fields.                        | Preferred approach—constructor is generated with parameters for each type. Apply the attribute multiple times (e.g., three `[DependsOn<…>]` blocks of five types each for 15 dependencies) when you need more than the generic arity allows.                    |
| `IDependencySet` marker + `[DependsOn<Set>]`                                                  | Dependency sets   | Reuse dependency bundles; consumer syntax stays natural.              | Any interface/class implementing `IDependencySet` is a bundle (not registered). `[DependsOn<Set>]` flattens the set (and inherited sets) into the consumer’s generated ctor/fields, preserving diagnostics and metadata. Cycles and name collisions are errors. |
| `[DependsOnConfiguration<T1, …>]`                                                             | Configuration     | Declare configuration dependencies without writing backing fields.    | Generator emits the fields, constructor parameters, and binding logic you’d normally get from `[InjectConfiguration]`, plus `[DependsOn]`-style naming controls and multi-slot support.                                                                         |
| `[Inject]`                                                                                    | Dependencies      | Last resort when a field must exist (custom naming, mutability).      | IOC035 tells you when the default naming regex already covers the dependency.                                                                                                                                                                                   |
| `[InjectConfiguration("Key", DefaultValue = "…", Required = bool, SupportsReloading = bool)]` | Configuration     | Bind simple values or options straight into fields.                   | **Last resort.** Use this only when a handwritten field is unavoidable (mutable state, custom lazy assignment). Otherwise prefer `[DependsOnConfiguration<…>]`.                                                                                                 |
| `[RegisterAs<T1, …>(InstanceSharing.Shared\|Separate)]`                                       | Interface control | Register only selected interfaces, optionally sharing instances.      | Shared mode emits factory registrations so all listed interfaces reuse one instance.                                                                                                                                                                            |
| `[RegisterAsAll(RegistrationMode.All\|Exclusionary\|DirectOnly, InstanceSharing)]`            | Interface control | Register every implemented interface (or concrete only).              | Combine with `[SkipRegistration<T>]` to prune specific interfaces.                                                                                                                                                                                              |
| `[SkipRegistration]` / `[SkipRegistration<T1, …>]`                                            | Interface control | Disable registration completely or exclude individual interfaces.     | IOC005/IOC037/IOC038 guard invalid combinations.                                                                                                                                                                                                                |
| `[ConditionalService(Environment = "Prod", ConfigValue = "Feature:X", Equals = "true")]`      | Conditional       | Register only when env/config matches.                                | Requires a lifetime attribute (IOC021).                                                                                                                                                                                                                         |
| `[ManualService]` / `[ExternalService]`                                                       | Advanced          | Mark services or dependencies that are registered manually.           | Keeps analyzers quiet when DI is handled elsewhere.                                                                                                                                                                                                             |
| `[InjectConfigurationOptions]` / `[DependsOnOptions]`                                         | Options           | Let IoCTools bind strongly typed options classes once and reuse them. | Automatically wires `IOptionsMonitor<T>` behind the scenes.                                                                                                                                                                                                     |

## Analyzer (Diagnostic) Reference

84 diagnostics (IOC001–IOC086) keep registrations honest. Each includes a remediation tip in Visual Studio / Rider / CLI build output.

| Rule | Severity | Summary |
|------|----------|---------|
| IOC001 | Error | Service depends on an interface with no implementation in the project. |
| IOC002 | Error | Implementation exists but is missing a lifetime attribute, so it never registers. |
| IOC003 | Error | Circular dependency detected (message lists the cycle). |
| IOC004 | Error | `[RegisterAsAll]` requires a lifetime attribute because it defines a service. |
| IOC005 | Warning | `[SkipRegistration]` without `[RegisterAsAll]` has no effect. |
| IOC006 | Warning | Duplicate dependency types across multiple `[DependsOn]` attributes. |
| IOC007 | Warning | Deprecated – replaced by IOC040 redundant dependency warnings. |
| IOC008 | Warning | Duplicate type listed inside a single `[DependsOn]` attribute. |
| IOC009 | Warning | `[SkipRegistration<T>]` targets an interface that would never be registered. |
| IOC010 | Warning | Deprecated (background-service lifetime warnings are handled by IOC014). |
| IOC011 | Error | Background services must be declared `partial`. |
| IOC012 | Error | Singleton service depends on a scoped service. |
| IOC013 | Warning | Singleton service depends on a transient service. |
| IOC014 | Error | Background service uses a non-singleton lifetime. |
| IOC015 | Error | Lifetime mismatch across an inheritance chain. |
| IOC016 | Error | `[InjectConfiguration]` uses an invalid configuration key. |
| IOC017 | Warning | `[InjectConfiguration]` targets an unsupported type. |
| IOC018 | Error | `[InjectConfiguration]` applied to a non-partial class. |
| IOC019 | Warning | `[InjectConfiguration]` cannot target static fields. |
| IOC020 | Warning | `[ConditionalService]` contains conflicting conditions. |
| IOC021 | Error | `[ConditionalService]` requires a lifetime attribute. |
| IOC022 | Warning | `[ConditionalService]` declared with no conditions. |
| IOC023 | Warning | `ConfigValue` set without `Equals` / `NotEquals`. |
| IOC024 | Warning | `Equals` / `NotEquals` provided without a `ConfigValue`. |
| IOC025 | Warning | `ConfigValue` is empty or whitespace. |
| IOC026 | Warning | Multiple `[ConditionalService]` attributes on the same class. |
| IOC027 | Info | Potential duplicate service registrations detected. |
| IOC028 | Error | `[RegisterAs]` used without any service indicators/lifetime metadata. |
| IOC029 | Error | `[RegisterAs]` lists an interface the class does not implement. |
| IOC030 | Warning | Duplicate interface listed inside `[RegisterAs]`. |
| IOC031 | Error | `[RegisterAs]` references a non-interface type. |
| IOC032 | Warning | `[RegisterAs]` duplicates what the generator already infers. |
| IOC033 | Warning | `[Scoped]` attribute is redundant because the service is implicitly scoped. |
| IOC034 | Warning | `[RegisterAsAll]` combined with `[RegisterAs]` is redundant. |
| IOC035 | Warning | `[Inject]` field matches the default `[DependsOn]` naming pattern. |
| IOC036 | Warning | Multiple lifetime attributes are applied to the same class. |
| IOC037 | Warning | `[SkipRegistration]` overrides other registration attributes on the same class. |
| IOC038 | Warning | `[SkipRegistration<T>]` does nothing when `[RegisterAsAll(RegistrationMode.DirectOnly)]` is used. |
| IOC039 | Warning | Dependency declared via `[Inject]`/`[DependsOn]` is never referenced. |
| IOC040 | Warning | A dependency type is declared multiple times via `[Inject]`, `[DependsOn]`, or configuration bindings (including inheritance). |
| IOC041 | Error | A class mixes IoCTools dependency annotations with a manual or primary constructor. |
| IOC042 | Warning | `[DependsOn(..., external: true)]` used even though an implementation exists. Remove `external: true`. |
| IOC043 | Warning | IOptions-based dependencies detected. Use `[DependsOnConfiguration<…>]` instead. |
| IOC044 | Warning | Dependency type is not a service (primitive/struct/string). Prefer `[DependsOnConfiguration<…>]`. |
| IOC045 | Warning | Collection dependency shape is unsupported. Only `IReadOnlyCollection<T>` is allowed. |
| IOC046 | Warning | Overlapping configuration bindings (options + per-field bindings for the same section). |
| IOC047 | Info | Use params-style attribute arguments for cleaner syntax. |
| IOC048 | Warning | Dependencies must be non-nullable. Prefer non-nullable types. |
| IOC049 | Error | Dependency set types (`IDependencySet`) must be metadata-only. |
| IOC050 | Error | Dependency set recursion detected (`SetA → SetB → SetA`). Remove the cycle. |
| IOC051 | Error | Dependency set expansion collides with an existing dependency of the same type but different member name. |
| IOC052 | Warning | Type implementing `IDependencySet` is marked for registration. Dependency sets must not be registered. |
| IOC053 | Info | Repeated dependency cluster found across services; extract an `IDependencySet`. |
| IOC054 | Info | Service is a near-match to an existing dependency set; consider using the set. |
| IOC055 | Info | Shared dependency cluster on services with a common base suggests moving dependencies into a set. |
| IOC056 | Info | Configuration section is bound both as options and primitive values in the same hierarchy. |
| IOC057 | Warning | Configuration binding not found for options type. |
| IOC058 | Info | Apply lifetime attribute to shared base class instead of each derived class. |
| IOC059 | Warning | `[Singleton]` attribute is redundant because base class already declares it. |
| IOC060 | Warning | `[Transient]` attribute is redundant because base class already declares it. |
| IOC061 | Warning | Dependency set already applied in base class. Remove redundant `[DependsOn<Set>]`. |
| IOC062 | Info | Move shared dependency set to base class to reduce duplication. |
| IOC063 | Warning | `[RegisterAs]` attribute is redundant on derived class (inherited from base). |
| IOC064 | Info | Move shared `[RegisterAs]` to base class to reduce duplication. |
| IOC065 | Warning | `[RegisterAsAll]` attribute is redundant on derived class. |
| IOC066 | — | Reserved (move shared `[RegisterAsAll]` to base class suggestion). |
| IOC067 | Warning | `[ConditionalService]` attribute is redundant on derived class (same condition as base). |
| IOC068 | Info | Class has a manual constructor with injectable parameters but no IoCTools attributes. Consider opting in. |
| IOC069 | Warning | `[RegisterAs]` requires a lifetime attribute. |
| IOC070 | Warning | `[DependsOn]`/`[Inject]` used without lifetime. Add `[Scoped]`, `[Singleton]`, or `[Transient]`. |
| IOC071 | Warning | `[ConditionalService]` missing lifetime. Add a lifetime attribute. |
| IOC072 | Warning | Hosted service declares explicit lifetime. Remove it (IHostedService is registered implicitly). |
| IOC073 | — | Reserved (previously had collision, now unused). |
| IOC074 | Info | Multi-interface class could use `[RegisterAsAll]` to register all interfaces. |
| IOC075 | Warning | Inconsistent lifetimes across inherited services. Move lifetime to base class. |
| IOC076 | Warning | Property redundantly wraps IoCTools dependency field. Access the field directly. |
| IOC077 | Error | Manual field shadows IoCTools-generated dependency. Remove the manual field. |
| IOC078 | Warning | `MemberNames` entry is suppressed by existing field. Remove the field or drop `MemberNames`. |
| IOC079 | Warning | Prefer `[DependsOnConfiguration<…>]` over raw `IConfiguration` dependency. |
| IOC080 | Error | Class uses IoCTools code-generating attributes but is not marked as `partial`. |
| IOC081 | Error | Manual registration duplicates IoCTools registration with same lifetime. |
| IOC082 | Error | Manual registration lifetime differs from IoCTools. Align lifetimes or remove manual registration. |
| IOC083 | Error | Manual options registration duplicates IoCTools binding. Remove manual `AddOptions`/`Configure`. |
| IOC084 | Warning | Lifetime attribute duplicates inherited lifetime. Remove redundant attribute. |
| IOC085 | Warning | Member name matches default naming. Remove explicit `memberName` parameter. |
| IOC086 | Warning | Manual registration could use IoCTools attributes instead.

## Key Workflows

- **Dependency hygiene** – IOC039 warns when `[Inject]` or `[DependsOn]` declarations never get referenced, and IOC040
  catches redundant combinations of `[Inject]` fields and `[DependsOn]` attributes before they reach generated
  constructors.
- **Configuration injection**: `[InjectConfiguration]` supports complex objects, primitives, and arrays, and
  `[DependsOnConfiguration<…>]` gives you the same binding behavior without writing backing fields—IoCTools generates
  them from the class-level attribute and still enforces IOC016–IOC019 diagnostics.
- **Dependency sets**: IOC049 forbids non-metadata members on `IDependencySet` types; IOC050 flags cycles (including
  recursive nesting); IOC051 reports name/type collisions when sets are flattened into consumers; IOC052 warns if a
  dependency set is ever considered for registration.
- **DRY set suggestions**: info-level analyzers (IOC053–IOC055) spot repeated dependency clusters, near-misses with
  existing sets, and base-class sharing opportunities, offering quick-fixes to extract or reuse `IDependencySet`
  bundles. IOC056 surfaces mixed options/primitive bindings even when they arrive through dependency sets.
- **Conditional services**: Use `Environment`/`NotEnvironment` for environment-specific registrations and
  `ConfigValue` + `Equals`/`NotEquals` for feature toggles.
- **Background workers**: Any partial `BackgroundService` is registered through `AddHostedService<T>()`; analyzers
  enforce singleton lifetimes.
- **Lifetime validation**: IOC012/IOC013 warn when a singleton captures scoped or transient services; IOC015 watches
  inheritance chains so longer-lived services never depend on shorter-lived implementations.
- **Inheritance chains**: Partial base/derived services share the same constructor graph. The generator walks the
  hierarchy so lifetimes stay consistent (IOC015 protects you) and dependencies from base classes are included
  automatically.
- **Manual/External services**: Mark services you register yourself with `[ManualService]` or `[ExternalService]` to
  satisfy the analyzers without disabling them globally.
- **Collections**: The generator produces `IEnumerable<T>`/`IReadOnlyList<T>` wrappers when multiple implementations
  exist—no manual `services.AddSingleton<IEnumerable<T>>` needed.

## Future Ideas

The current roadmap builds on IOC039/IOC040 by surfacing more of the generator’s work directly in source, so developers
rarely need to open `.g.cs` files:

- **IDE quick fixes** – ship Roslyn light-bulb actions for IOC039/IOC040 so you can convert `[Inject]` → `[DependsOn]`,
  drop redundant declarations, or remove dead dependencies with a click.
- **CLI parity for fixes** – pair the IDE actions with a `dotnet ioc fix/report/describe` tool so console and CI
  workflows can apply the same quick fixes, view redundancy reports, and inspect generated members without opening
  `.g.cs`.
- **Structured warning aggregates** – add a low-severity diagnostic summarizing the dependency hygiene status per
  class (e.g., “2 unused dependencies, 1 redundant”) to make large refactors easier to triage.
- **Fine-grained analyzer knobs** – expose MSBuild properties (e.g., `IoCToolsUnusedDependencySeverity`,
  `IoCToolsRedundantDependencyScope`) so teams can tune these warnings per project or per configuration.
- **Rich XML documentation & metadata** – have generated fields/constructors emit `<summary>`/`<param>` details noting
  which attribute produced them, and optionally tag them with `[GeneratedDependency(Attribute = …, DeclaredAt = …)]` so
  Go-To-Definition jumps back to the originating partial.
- **Analyzer-assisted navigation** – provide info diagnostics/code actions that list generated constructor signatures
  inline or open a preview window, eliminating the need to browse generated files.
- **Partial-class alignment hints** – warn (info) when another partial already defines a conflicting field or when
  `[DependsOn]` output is unused, including the generated signature in the message for quick fixes.
- **Debugger-friendly instrumentation** – optionally register a lightweight inspector in DEBUG builds so you can inspect
  the generated dependency graph at runtime without touching the `.g.cs` output.
- **Service graph dumps** – behind an MSBuild flag (e.g., `IoCToolsDumpServiceGraph=true`), emit a compact JSON summary
  of each service, its generated fields, and registrations to simplify reviews and CI audits.
- **Partial-type mapping guidance** – extend analyzers to suggest relocating `[DependsOn]` declarations to the partial
  that actually consumes the dependency, preventing future IOC039 hits.
- **DependsOnConfiguration diagnostics** – add IOC04x warnings that: (1) surface duplicate slots across
  `[DependsOnConfiguration]` + `[InjectConfiguration]`, (2) highlight unused configuration slots, (3) flag redundant
  attributes when an identical key/type combo is declared twice, (4) detect conflicting `MemberNames`/
  `ConfigurationKeys` lengths, and (5) offer a fixer to convert eligible `[InjectConfiguration]` fields into class-level
  attributes.

## Configuration

IoCTools reads configuration from MSBuild properties/`.editorconfig` and from an optional
`IoCTools.Generator.Configuration.GeneratorOptions` class. Common knobs:

| Property / API                                                                                                    | Purpose                                                                                                                                                                                                      | Example                                                                                    |
|-------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------|
| `build_property.IoCToolsNoImplementationSeverity`, `IoCToolsManualSeverity`, `IoCToolsLifetimeValidationSeverity` | Override analyzer severity per category.                                                                                                                                                                     | `.editorconfig`: `build_property.IoCToolsNoImplementationSeverity = error`                 |
| `build_property.IoCToolsDisableDiagnostics`                                                                       | Disable all IoCTools diagnostics (not recommended except in migration).                                                                                                                                      | `true`                                                                                     |
| `build_property.IoCToolsDisableLifetimeValidation`                                                                | Turn off lifetime-specific analyzers (IOC012–IOC015).                                                                                                                                                        | `true`                                                                                     |
| `build_property.IoCToolsSkipAssignableTypesUseDefaults` / `IoCToolsSkipAssignableTypes` / `…Add` / `…Remove`      | Control “skip-by-assignable” generator style filters (exclude categories of services from registration). Default skips ASP.NET `ControllerBase` only; add more (e.g., Mediator/MediatR handlers) via `…Add`. | `IoCToolsSkipAssignableTypesAdd = Mediator.*;MediatR.*`                                    |
| `build_property.IoCToolsSkipAssignableExceptions`                                                                 | Carve exceptions back in when using skip lists.                                                                                                                                                              | `IoCToolsSkipAssignableExceptions = Namespace.ImportantService`                            |
| `build_property.IoCToolsIgnoredTypePatterns`                                                                     | Patterns for cross-assembly interfaces to ignore (semicolon-separated). Supports `*` wildcard. Use for clean architecture where interfaces are in separate assemblies.                              | `IoCToolsIgnoredTypePatterns = *.Abstractions.*;*.Contracts.*;*.Interfaces.*` (default: `*.Abstractions.*;*.Contracts.*;*.Interfaces.*;*.ILoggerService<`) |
| `build_property.IoCToolsDefaultServiceLifetime`                                                                   | Sets the implicit lifetime applied when a service has intent but no explicit `[Scoped]/[Singleton]/[Transient]`; generator output and IOC012/IOC013 both use the configured value.                           | `IoCToolsDefaultServiceLifetime = Singleton` (values: `Scoped`, `Singleton`, `Transient`). |
| `IoCTools.Generator.Configuration.GeneratorOptions` class                                                         | Configure the same skip/exceptions options via code when MSBuild isn’t convenient.                                                                                                                           | Define a static class with `public static GeneratorStyleOptions Current => new(...);`      |

All properties can live in `Directory.Build.props`, `.editorconfig`, or project files. The generator merges “base list +
add/remove + exceptions” so you can set organization-wide defaults then fine-tune per project.

## Samples & License

- `IoCTools.Sample` demonstrates every attribute, diagnostic, and configuration scenario (background services,
  RegisterAs vs RegisterAsAll, shared instances, options binding, etc.).
- Licensed under MIT. See `LICENSE`.
