# IoCTools Ideas & Future Work

## Implementation Backlog

### typeof() Diagnostics

- Add typeof() argument parsing to ManualRegistrationValidator (foundation for IOC090-094)
- Add IOC090 - typeof() interface-implementation registration could use IoCTools
- Add IOC091 - typeof() registration duplicates IoCTools registration
- Add IOC092 - typeof() registration lifetime mismatch
- Add IOC094 - Open generic typeof() could use IoCTools attributes
- Add integration tests for all typeof() diagnostics
- Add typeof() diagnostic examples to sample project
- Update CLAUDE.md diagnostic reference for IOC090-094

### IOC032 + InstanceSharing.Shared awareness

**Gap:** `ValidateRegisterAsMatchesImplementedInterfaces` in `RedundantConfigurationValidator.cs` flags `[RegisterAs<I1, I2>]` as redundant whenever the type-argument set equals the implemented-interface set. The check ignores the `InstanceSharing` argument. When the user explicitly opts into `InstanceSharing.Shared`, the attribute is **not** redundant — removing it switches generated registration from a single shared instance (factory pattern) to default direct bindings (one instance per interface), which is a real semantic regression.

**Repro:**

```csharp
[Singleton]
[RegisterAs<IFoo, IBar>(InstanceSharing.Shared)]
public partial class MyService : IFoo, IBar { ... }
// IOC032: Class 'MyService' already registers interfaces IBar, IFoo by default. ❌
```

With `[RegisterAs Shared]`, generated code creates one singleton + factory aliases. Without it, IoCTools generates `services.AddSingleton<IFoo, MyService>(); services.AddSingleton<IBar, MyService>();` — two singleton instances.

**Fix sketch:** in `ValidateRegisterAsMatchesImplementedInterfaces`, after the interface-set equality check, inspect the attribute's `InstanceSharing` argument. If `Shared`, do not report IOC032 (the attribute is the only way to opt into shared-instance semantics).

Currently suppressed by downstream consumers with documented per-site `#pragma warning disable IOC032`.

### Diagnostic UX

- Add HelpLinkUri to all 87 diagnostic descriptors
- Use specific categories for IDE grouping (Lifetime, Dependency, Configuration, Registration, Structural)
- Suggest IServiceProvider/CreateScope() pattern in IOC012/013 for intentional lifetime violations
- Better config error messages with examples for IOC016-019
- Show full inheritance path in IOC015 diagnostic

### CLI Improvements

- Add --verbose flag for debugging (MSBuild diagnostics, generator timing, file paths)
- Add JSON output mode for all commands (--json flag; GraphPrinter already has precedent)
- Color-code diagnostic output by severity (red/yellow/cyan)
- Extend fuzzy type suggestions to all commands (WhyPrinter pattern already exists)
- Add wildcard/regex support to FilterByType in CLI services
- Add service count to profile command output
- Add .editorconfig recipe for suppressing IoCTools diagnostics

### Code Quality

- Centralize RegisterAsAllAttribute checks using AttributeTypeChecker (20 inconsistent locations)
- Adopt ReportDiagnosticDelegate pattern in 3-4 more validators
- Resolve CS8603 null reference warnings in sample code (3 instances in MultiInterfaceExamples.cs)
- Add code comments explaining InstanceSharing.Separate default behavior

### Documentation

- Cross-reference netstandard2.0 constraints in README
- Update CLAUDE.md diagnostic reference table for new diagnostic codes

### Inheritance-Aware Service Intent — SHIPPED 1.6.3-dev.2

**Gap:** `ServiceClassPipeline.cs:37-38` used `symbol.Interfaces.Any()` (direct interfaces only) when computing `isPartialWithInterfaces`. A partial class inheriting an interface from an IoCTools-managed base (e.g., abstract base with `[DependsOn]`) returned empty `Interfaces`, so the derived class was NOT detected as having service intent. Result: derived class with no own attribute failed to get a generated forwarding ctor; build error referenced missing args for the base ctor.

**Repro from Delta:**
```csharp
[DependsOn<IClock, IScheduler, ...>]
public abstract partial class ProcessAutomationRuntimeAdapterBase : IAutomationRuntimeAdapter { ... }

// Bare derived class — IoCTools didn't generate forwarding ctor.
// Build failed: "no argument given that corresponds to the required parameter 'clock' of base ctor"
public partial class CommandAutomationRuntimeAdapter : ProcessAutomationRuntimeAdapterBase
{
    protected override ILogger Logger => _logger;
}
```

**Fix:** Added `ServiceDiscovery.InheritsFromIoCToolsManagedBase` helper that walks `BaseType` chain checking for IoCTools attrs (lifetime, `[DependsOn]`, `[ConditionalService]`, `[RegisterAsAll]`, `[RegisterAs]`, `[Inject]`/`[InjectConfiguration]` fields). Wired into `ServiceClassPipeline.hasServiceIntent`. Bare derived class now auto-qualifies, default Scoped lifetime applies, no opt-in marker required. Tests in `InheritedServiceIntentTests.cs`.

---

## Future Considerations

These are worth revisiting later based on user feedback or project growth:

- Add diagnostic for 20+ dependencies as a code smell indicator (validate threshold first)
- Implement CodeFixProvider for common diagnostics (requires separate analyzer package)
- Detect validation attributes and suggest IValidateOptions (complex; better as docs)
- Add progress indicators for CLI long operations (most ops complete in 1-5 seconds)
- Standardize diagnostic assertion syntax across test suite (271 usages; internal-only)
- Annotate non-diagnostic ContainSingle usages in test code (low priority clarity)
