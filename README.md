# IoCTools

[![NuGet](https://img.shields.io/nuget/v/IoCTools.Abstractions?label=IoCTools.Abstractions)](https://www.nuget.org/packages/IoCTools.Abstractions)
[![NuGet](https://img.shields.io/nuget/v/IoCTools.Generator?label=IoCTools.Generator)](https://www.nuget.org/packages/IoCTools.Generator)

> A Roslyn source generator that lets each service declare its own lifetime, dependencies, and registration intent with small attributes. IoCTools emits constructors, service registrations, and analyzers at build time—no reflection, no runtime scanning.

## Highlights

- **Self-describing services** – `[Scoped]`, `[DependsOn<T>]`, `[RegisterAs<…>]`, and `[ConditionalService]` live on the class, so intent never leaves the type
- **Dependency sets** – Implement `IDependencySet` to reuse dependency bundles across services
- **Inheritance-aware** – Derived services inherit base class lifetime; diagnostics validate across the full chain
- **100+ diagnostics** – Build-time validation catches missing lifetimes, circular dependencies, lifetime mismatches, open-generic edge cases, and FluentValidation anti-patterns
- **Zero reflection** – Everything happens at compile time; generated code is plain C# you can inspect

## Authoring Posture

- `[Inject]` is **deprecated** in 1.6.0 and emits `IOC095`. A Roslyn code
  fix plus [`ioc-tools migrate-inject`](docs/cli-reference.md) migrate in
  bulk. Scheduled for removal in 2.0.
- `[DependsOn<T>]` is the source-of-truth for explicit service dependencies.
- `[DependsOnConfiguration<T>]` / `[DependsOnOptions<T>]` for config
  dependencies. `InjectConfiguration` stays supported but is not preferred.
- **Auto-deps (1.6.0+)** declare ambient dependencies once per assembly:
  `[assembly: AutoDep<T>]`, `[assembly: AutoDepOpen(typeof(ILogger<>))]`,
  plus `Microsoft.Extensions.Logging.ILogger<T>` is auto-detected with zero
  configuration. See [docs/auto-deps.md](docs/auto-deps.md).

## Installation

```bash
dotnet add package IoCTools.Abstractions
dotnet add package IoCTools.Generator
```

Or directly in your project file:

```xml
<ItemGroup>
  <PackageReference Include="IoCTools.Abstractions" Version="*" />
  <PackageReference Include="IoCTools.Generator" Version="*" PrivateAssets="all" />
</ItemGroup>
```

## What's New in v1.7.1

> v1.7.0 was tagged but never published to NuGet — CI failed in the CLI fixture-evidence build step before the publish job ran. v1.7.1 is the first published release with these features.

- **`AnalysisScope` model + `DiagnosticGate`** — each diagnostic declares
  whether it fires in production projects, test projects, or both.
  `IOC081`/`IOC082`/`IOC086` are reclassified as `AnalysisScope.Production`;
  test projects are auto-exempt via the `IsTestProject` MSBuild property
  (forwarded as a `CompilerVisibleProperty` from `Microsoft.NET.Test.Sdk`).
  No naming heuristics.
- **IOC110** (Warning, configurable via `IoCToolsLifetimeValidationSeverity`)
  — deterministic multi-impl lifetime diagnostic. Fires when *some* (not all)
  implementations of an interface violate the lifetime dependency rule.
  Replaces the previous non-deterministic single-impl selection that caused
  IOC012 to fire in Rider but pass silently in CLI.
- **IOC073 / IOC066** — open-generic and inaccessible `IHostedService`
  implementers are skipped at registration with observable diagnostics
  (rather than emitting CS0246/CS0122 in consumer code).
- **`[RegisterAs<T>]` + `[RegisterAsAll]` compose with `IHostedService`** —
  the concrete class is registered once at the declared lifetime, companion
  interfaces bridged via `GetRequiredService<TImpl>()`, and `IHostedService`
  bridged to the same instance.
- **Test fixture generator v2** (`IoCTools.Testing`) — `FixtureMemberPlanner`
  separates planning from emission. Generated fixtures enable nullable
  context, emit `new` modifier on derived hidden members, support
  `GetRequiredSection` and binder-style configuration reads, and treat
  `IClock` as fixture-provided.
- **IOC032 InstanceSharing.Shared exemption**, **IOC081/082/086 carve-outs**
  for `Replace`, factory lambdas, `TryAddEnumerable`, and IoCTools-unaware
  assemblies, **buildTransitive packaging** of `IoCTools.Generator.targets`,
  and **CLI host-path resolver** that walks `PATH` before throwing.
- **Multitarget CLI** — `IoCTools.Tools.Cli` ships for both `net9.0` and
  `net10.0`. Existing .NET 9 global-tool consumers are not broken.
- **CI build fix (1.7.1)** — removed accidental `IoCTools.Testing` analyzer
  reference from `FixtureEvidence.TestsProject` (CLI evidence corpus only needs
  `IoCTools.Testing.Abstractions` for `[Cover<T>]`); removed unused
  `ProductionPreferenceHelperTests` helper that combined IoCTools deps with a
  manual constructor, triggering IOC041 through diagnostics core (`net9.0`
  builds pass; `net8.0` CI now installs SDK `8.0.x`).

## Getting Started in Three Steps

1. **Annotate a partial service**

   ```csharp
   [DependsOn<ILogger<EmailService>>]
   public partial class EmailService : IEmailService
   {
       public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
   }
   ```

   _Tip:_ `[Scoped]` is implied for partial classes implementing interfaces. Add `[Singleton]`/`[Transient]` only when you want to change that default.

   [Full getting started guide](docs/getting-started.md)

2. **Build** – IoCTools emits `Add<YourAssembly>RegisteredServices()` into `<AssemblyName>.Extensions.Generated`.

3. **Call the extension during startup**

   ```csharp
   using YourAssembly.Extensions.Generated;

   var builder = WebApplication.CreateBuilder(args);
   builder.Services.AddYourAssemblyRegisteredServices(builder.Configuration);
   ```

## Common Open-Generic Pattern

```csharp
[Scoped]
[RegisterAsAll]
public partial class Repository<T> : IRepository<T> where T : class
{
}
```

That common open-generic path is supported. If you ask for
`InstanceSharing.Shared` across open-generic interface aliases, IoCTools
emits the secondary `IOC095` descriptor and falls back to valid direct
registrations because `Microsoft.Extensions.DependencyInjection` does not
support open-generic factory aliases. (In 1.6.0+, the primary `IOC095`
descriptor is the `[Inject]` deprecation diagnostic — both ship under the
same ID; see [diagnostics.md](docs/diagnostics.md).)

## Platform Support

IoCTools works with .NET Framework 4.6.1+, .NET Core 2.0+, and .NET 5+. The generator targets netstandard2.0 internally, but your service code can use any C# features your framework supports. See [platform constraints](docs/platform-constraints.md) for details.

## Testing with IoCTools

The [IoCTools.Testing](docs/testing.md) package auto-generates test fixtures, eliminating mock declaration boilerplate.

```csharp
using IoCTools.Testing.Annotations;

[Cover<UserService>]
public partial class UserServiceTests
{
    [Fact]
    public void Test() {
        var result = Sut.GetById(1); // Lazy auto-generated SUT
    }
}
```

[Full testing guide](docs/testing.md)

The CLI includes `test scaffold` and fixture-aware `evidence --test-fixtures`
paths for downstream adoption:
```bash
dotnet ioc-tools test scaffold --project src/App.csproj --type MyApp.Services.UserService --dry-run
dotnet ioc-tools evidence --project tests/App.Tests.csproj --production-project src/App.csproj --test-fixtures
```

## IoCTools CLI

`IoCTools.Tools.Cli` ships as a dotnet global/local tool (`dotnet ioc-tools …`). It interrogates your project with the real IoCTools generator.

### Installation

```bash
dotnet pack IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj -c Release -o ./artifacts
dotnet tool install --global --add-source ./artifacts IoCTools.Tools.Cli
```

### Commands

| Command                                                                  | What it surfaces                                                                                                    |
|--------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------|
| `fields --project <csproj> --file <class.cs> [--type ...] [--source]`    | Lists generated `[DependsOn]` fields; outputs constructor source with `--source`                                     |
| `services --project <csproj> [--output <dir>] [--source] [--type ...]`   | Summarizes registrations (lifetimes, interfaces, factories); outputs source with `--source`                          |
| `explain --project <csproj> --type Namespace.Service`                    | Explains a single service: dependencies, config bindings, external flags                                             |
| `graph --project <csproj> [--type ...] [--format json\|puml\|mermaid]`   | Emits a service graph in JSON/PlantUML/Mermaid                                                                     |
| `why --project <csproj> --type ... --dependency Fully.Qualified.Type`    | Shows which generated field matches a dependency                                                                    |
| `doctor --project <csproj> [--fixable-only]`                             | Runs generator and prints diagnostics; `--fixable-only` filters to warnings/infos                                   |
| `evidence --project <csproj> [--type ...] [--settings ...]`              | Emits one correlated evidence bundle across services, diagnostics, configuration, validators, profile, hints, and fingerprinted artifacts |
| `config-audit --project <csproj> [--settings appsettings.json]`          | Lists required config bindings and reports missing keys                                                             |
| `suppress --project <csproj> [--codes IOC035,IOC092] [--json]`           | Generates `.editorconfig` suppression recipes plus structured rule metadata                                         |
| `validators --project <csproj> [--filter ...]`                           | Lists discovered FluentValidation validators                                                                        |
| `validator-graph --project <csproj> [--why ValidatorName] [--json]`      | Shows validator composition tree or structured lifetime explanation                                                 |

[Full CLI reference](docs/cli-reference.md)

## Before & After: Replacing DI Smells

### Legacy (manual, brittle)

```csharp
public class LegacyBillingService : IBillingService
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
}

services.AddHttpClient();
services.Configure<BillingOptions>(configuration.GetSection("Billing"));
services.AddScoped<IBillingService, LegacyBillingService>();
```

Problems: duplicated registrations, runtime config lookups, no analyzer guardrails.

### IoCTools (attributes, analyzers, generated DI)

```csharp
using IoCTools.Abstractions.Annotations;

[Scoped]
[DependsOn<ILogger<BillingService>, IHttpClientFactory, IClock>]
[DependsOnConfiguration<string>("Billing:BaseUrl", Required = true)]
[DependsOnConfiguration<int>("Billing:RetryCount", DefaultValue = "3")]
public partial class BillingService : IBillingService
{
    public async Task ChargeAsync(BillingRequest request)
    {
        using var client = _httpClientFactory.CreateClient("billing");
        // _logger, _httpClientFactory, _clock, _baseUrl, _retryCount all available
    }
}
```

Generated code creates the constructor, binds configuration, and registers everything via `builder.Services.AddYourAssemblyRegisteredServices(configuration)`.

## Attribute Reference

Complete attribute reference: [docs/attributes.md](docs/attributes.md)

Key attributes: `[Scoped]`, `[Singleton]`, `[Transient]`, `[DependsOn<T>]`, `[DependsOnConfiguration<T>]`, `[DependsOnOptions<T>]`, `[RegisterAs<T>]`, `[ConditionalService]`, and the 1.6.0 [auto-deps surface](docs/auto-deps.md) (`[assembly: AutoDep<T>]`, `[assembly: AutoDepOpen(...)]`, `[AutoDeps<TProfile>]`, `[NoAutoDeps]`, …)

## Diagnostics Reference

IoCTools provides core diagnostics through `IOC110`, plus testing diagnostics (`TDIAG01` through `TDIAG08`) and FluentValidation diagnostics (`IOC100`-`IOC102`). See [diagnostics.md](docs/diagnostics.md) for the full list.

### Error-Severity Diagnostics

| Rule | Summary |
|------|---------|
| IOC001 | Service depends on unimplemented interface |
| IOC002 | Implementation missing lifetime attribute |
| IOC003 | Circular dependency detected |
| IOC004 | `[RegisterAsAll]` requires lifetime |
| IOC011 | Background service must be partial |
| IOC012 | Singleton depends on Scoped |
| IOC014 | Background service with non-Singleton lifetime |
| IOC015 | Lifetime mismatch in inheritance chain |
| IOC016 | Invalid configuration key |
| IOC018 | `[InjectConfiguration]` requires partial class |
| IOC021 | `[ConditionalService]` requires lifetime |
| IOC028 | `[RegisterAs]` without service indicators |
| IOC029 | `[RegisterAs]` specifies unimplemented interface |
| IOC031 | `[RegisterAs]` specifies non-interface type |
| IOC041 | Manual constructor conflicts with IoCTools |
| IOC049 | Dependency set with non-metadata members |
| IOC050 | Dependency set cycle detected |
| IOC051 | Dependency set name collision |
| IOC077 | Manual field shadows generated dependency |
| IOC080 | Code-generating attributes require partial |
| IOC081 | Manual registration duplicates IoCTools |
| IOC082 | Manual registration lifetime differs |
| IOC087 | Transient depends on Scoped |
| IOC088 | Configuration circular reference |
| IOC092 | typeof() registration lifetime mismatch |
| TDIAG04 | `[Cover<T>]` service has no generated constructor |
| TDIAG05 | Test class with `[Cover<T>]` must be partial |
| TDIAG07 | Fixture setup helper called after `Sut` access |
| TDIAG08 | Test class manually constructs a service that could use `[Cover<T>]` |

[View all diagnostics including warnings and info](docs/diagnostics.md)

## Configuration

IoCTools reads configuration from MSBuild properties/`.editorconfig`. Common knobs:

| Property | Purpose | Example |
|----------|---------|---------|
| `IoCToolsNoImplementationSeverity`, `IoCToolsManualSeverity`, `IoCToolsLifetimeValidationSeverity` | Override analyzer severity per category | `build_property.IoCToolsNoImplementationSeverity = error` |
| `IoCToolsDisableDiagnostics` | Disable all IoCTools diagnostics | `true` |
| `IoCToolsIgnoredTypePatterns` | Patterns for cross-assembly interfaces to ignore | `*.Abstractions.*;*.Contracts.*;*.Interfaces.*` |
| `IoCToolsDefaultServiceLifetime` | Sets the implicit lifetime when no explicit attribute | `Scoped` \| `Singleton` \| `Transient` |

See [configuration reference](docs/configuration.md)

## Samples & License

- `IoCTools.Sample` demonstrates every attribute, diagnostic, and configuration scenario
- Licensed under MIT. See `LICENSE`
