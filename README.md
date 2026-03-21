# IoCTools

[![NuGet](https://img.shields.io/nuget/v/IoCTools.Abstractions?label=IoCTools.Abstractions)](https://www.nuget.org/packages/IoCTools.Abstractions)
[![NuGet](https://img.shields.io/nuget/v/IoCTools.Generator?label=IoCTools.Generator)](https://www.nuget.org/packages/IoCTools.Generator)

> A Roslyn source generator that lets each service declare its own lifetime, dependencies, and registration intent with small attributes. IoCTools emits constructors, service registrations, and analyzers at build time—no reflection, no runtime scanning.

## Highlights

- **Self-describing services** – `[Scoped]`, `[DependsOn<T>]`, `[RegisterAs<…>]`, and `[ConditionalService]` live on the class, so intent never leaves the type
- **Dependency sets** – Implement `IDependencySet` to reuse dependency bundles across services
- **Inheritance-aware** – Derived services inherit base class lifetime; diagnostics validate across the full chain
- **94+ diagnostics** – Build-time validation catches missing lifetimes, circular dependencies, lifetime mismatches, and more
- **Zero reflection** – Everything happens at compile time; generated code is plain C# you can inspect

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

## What's New in v1.5.0

- [Test fixture generation](docs/testing.md) — Auto-generate Mock fields, CreateSut(), and setup helpers with `[Cover<T>]`
- [typeof() diagnostics](docs/diagnostics.md#ioc090) — Build-time validation for manual DI registrations (IOC090-IOC094)
- [CLI enhancements](docs/cli-reference.md) — JSON output, verbose mode, color-coded diagnostics, and config-audit
- [Improved diagnostics](docs/diagnostics.md) — Enhanced messages with CreateScope() suggestions and inheritance paths

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
        var sut = CreateSut(); // Auto-generated
    }
}
```

[Full testing guide](docs/testing.md)

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
| `config-audit --project <csproj> [--settings appsettings.json]`          | Lists required config bindings and reports missing keys                                                             |
| `suppress --project <csproj> <diagnostic-id>`                            | Generates `.editorconfig` diagnostic suppression recipe                                                             |

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

Key attributes: `[Scoped]`, `[Singleton]`, `[Transient]`, `[DependsOn<T>]`, `[RegisterAs<T>]`, `[ConditionalService]`, `[InjectConfiguration]`

## Diagnostics Reference

IoCTools provides 94+ diagnostics (IOC001-IOC094, TDIAG-01 through TDIAG-05). See [complete reference](docs/diagnostics.md).

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
| TDIAG-04 | `[Cover<T>]` service has no generated constructor |
| TDIAG-05 | Test class with `[Cover<T>]` must be partial |

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
