# IoCTools Auto-Deps and `[Inject]` Deprecation Design

Date: 2026-04-22
Status: Draft for approval
Target Release: `1.6.0`
Scope: Introduce an "auto-deps" feature that injects common dependencies (starting with `ILogger<T>`) into every generated service without per-service declaration, and deprecate `[Inject]` in favor of `[DependsOn<T>]` as the sole canonical dependency-declaration mechanism.

## Purpose

IoCTools services today incur two forms of boilerplate that this design eliminates:

1. Every service declares `[Inject] private readonly ILogger<TSelf> _logger;` — a near-universal line that the generator can and should supply.
2. The library has two redundant ways to declare dependencies (`[Inject]` on fields, `[DependsOn<T>]` on the class). `[Inject]` is visually noisier, field-oriented, and diverges from the declarative spirit of the rest of the attribute surface.

The design consolidates to one declaration mechanism (`[DependsOn<T>]`), makes truly universal dependencies implicit (`AutoDep<T>`), and provides a profile system for category-scoped defaults (controllers, background services, etc.).

## Design Goals

- Eliminate the `ILogger<T>` boilerplate line in the default service template.
- Make `[DependsOn<T>]` the only recommended way to declare a dependency; deprecate `[Inject]` on a clear timeline with tooling assistance.
- Keep the consumer-facing surface entirely generic-attribute-based; avoid `typeof()` except where C# language rules require it.
- Stay consistent with modern .NET idioms: assembly-level attributes for cross-cutting config, MSBuild properties as CI/override channel only.
- Preserve strict behavioral parity between `[Inject]` and `[DependsOn<T>]` so that the migration is mechanical.
- Surface clear diagnostics for every misuse path, including open-generic edge cases.

## Non-Goals

- Rewrite the existing `[Inject]` attribute semantics in-place. The migration is a deprecation, not a breaking change in 1.6.
- Introduce implicit generator behavior broadly. The **only** implicit auto-dep in 1.6 is the `Microsoft.Extensions.Logging.ILogger<T>` built-in detection (opt-out via `IoCToolsAutoDetectLogger=false` or `[NoAutoDepOpen(typeof(ILogger<>))]`). Every other auto-dep — closed-type, open-generic, profile-scoped, or transitive — requires an explicit assembly attribute declared somewhere in the reference chain. This carve-out is deliberate: ILogger earns zero-config treatment because it is genuinely universal, no other type does.
- Introduce a new code-based config convention. `GeneratorOptions` const-class remains supported for existing knobs but is not the surface for auto-deps.
- Ship profile-scoped open-generic auto-deps in 1.6. That is a future extension (`AutoDepInOpen<TProfile>`), not in scope.
- Change how the DI container resolves services at runtime. All auto-deps flow through the normal generated constructor.
- Support primary-constructor-style injection. `[DependsOn<T>]` remains the mechanism; the generated constructor is still owned by IoCTools.

## Auto-Deps System

### Universal Defaults

Declared at assembly scope with generic attributes:

```csharp
[assembly: AutoDep<TimeProvider>]
[assembly: AutoDep<IMetrics>]
[assembly: AutoDepOpen(typeof(ILogger<>))]
```

`AutoDep<T>` applies a closed type to every service the generator produces a constructor for, in that assembly.

`AutoDepOpen(typeof(T<>))` handles single-arity unbound generics. The generator closes the unbound type with the concrete service type at codegen time. `AutoDepOpen(typeof(ILogger<>))` applied to `OrderService` yields a constructor parameter typed `ILogger<OrderService>`.

The `typeof()` in `AutoDepOpen` is the one unavoidable exception to the "generics all the way down" principle. C# does not permit unbound generics as type arguments to generic attributes, so `AutoDep<ILogger<>>` is a language-level impossibility. `AutoDepOpen` is named distinctly so that the exception is obvious and narrowly scoped.

### Built-in Auto-Detection: `ILogger<T>`

`Microsoft.Extensions.Logging.ILogger<T>` is universal in modern .NET. Requiring every consuming project to declare `[assembly: AutoDepOpen(typeof(ILogger<>))]` to regain logging would be a config tax that defeats the feature's premise. Instead, the generator detects the MEL `ILogger<T>` open-generic in the compilation's references and treats it as if a universal `AutoDepOpen(typeof(ILogger<>))` had been declared — no user config required.

**Detection rules:**

- The type name is matched exactly and fully-qualified: `Microsoft.Extensions.Logging.ILogger\`1` via `Compilation.GetTypeByMetadataName`. User-defined `ILogger<T>` types in consumer assemblies do not trigger detection. `Serilog.ILogger` (non-generic) does not trigger detection.
- Detection runs once per compilation and the result is cached in the resolver's equatable output value, keyed by the referenced-assembly MVID set.
- If the MEL `ILogger<T>` type is not discoverable in any reference, detection yields no built-in auto-dep and the generator behaves exactly as if this section did not exist. Projects that do not use logging are unaffected.

**Opt-outs:**

- **Project-wide:** MSBuild property `IoCToolsAutoDetectLogger=false` disables the detection entirely. Universal `AutoDep<T>` / `AutoDepOpen` declarations still work; this flag only disables the *implicit* logger behavior.
- **Per-service, targeted:** `[NoAutoDepOpen(typeof(ILogger<>))]` on a service class. The twin of `AutoDepOpen`, suppresses any auto-dep derived from the matching open-generic shape regardless of closure **and regardless of source** — the suppression is source-agnostic and affects the built-in detection, local universal `AutoDepOpen`, and transitive `AutoDepOpen` declarations identically. This is the recommended per-service opt-out because it does not repeat the service's own type name.
- **Per-service, closed form:** `[NoAutoDep<ILogger<MyService>>]` still works but requires the service to repeat its own type — a rename silently breaks the match. Retained for completeness; `NoAutoDepOpen` is preferred.
- **Per-service, nuclear:** `[NoAutoDeps]` disables every auto-dep.
- **Project-wide, nuclear:** `IoCToolsAutoDepsDisable=true` disables the entire feature.

**Attribution.** In CLI output and the debug report, the detected logger appears as `auto-builtin:ILogger` — distinct from `auto-universal` (declared assembly attribute) so users can tell which is which.

Other types that might seem like candidates for auto-detection (`TimeProvider`, `IMemoryCache`, `IServiceProvider`) are **not** in 1.6. Only `ILogger<T>` earns zero-config treatment because only `ILogger<T>` is near-universal across the .NET ecosystem. Additional auto-detected types, if ever, would each get their own specific MSBuild opt-out (`IoCToolsAutoDetectXxx=false`) rather than a generic "builtins" umbrella.

### Transitive Scope Across Assembly Boundaries

By default, every assembly-level auto-dep attribute applies only to services declared in the declaring assembly. For two real use cases — a library publishing opinionated defaults to consumers, and a shared project carrying solution-wide policy — the policy needs to flow across assembly boundaries. This is supported via an `AutoDepScope` enum on five assembly-level attributes:

```csharp
namespace IoCTools.Abstractions.Annotations;

public enum AutoDepScope
{
    Assembly,     // default — policy applies only to services in the declaring assembly
    Transitive    // policy also applies to services in consuming assemblies
}
```

The `Scope` named property is added to `AutoDep<T>`, `AutoDepOpen`, `AutoDepIn<TProfile, T>`, `AutoDepsApply<TProfile, TBase>`, and `AutoDepsApplyGlob<TProfile>`:

```csharp
// Library "Acme.Platform" declares transitive intent
[assembly: AutoDep<ITracer>(Scope = AutoDepScope.Transitive)]
[assembly: AutoDepOpen(typeof(ILogger<>), Scope = AutoDepScope.Transitive)]
[assembly: AutoDepIn<ControllerDefaults, IMediator>(Scope = AutoDepScope.Transitive)]
[assembly: AutoDepsApply<ControllerDefaults, ControllerBase>(Scope = AutoDepScope.Transitive)]

// Consumer "Acme.WebApp" automatically inherits the above for its own services
// by virtue of referencing Acme.Platform. No additional declaration required.
```

**Generator mechanics.** When processing assembly B, the generator walks `Compilation.SourceModule.ReferencedAssemblySymbols`, filters to assemblies that transitively reference `IoCTools.Abstractions`, reads their assembly-level attributes via `IAssemblySymbol.GetAttributes()`, and includes any whose `Scope` property equals `AutoDepScope.Transitive` in B's resolved auto-dep set. This reuses the existing cross-assembly scanning pattern that IoCTools already performs for `[RegisterAs<T>]`-registered types in referenced assemblies.

**Solution-wide policy via shared project.** For a team that wants one declaration across every project in a solution, the idiomatic pattern is to create a tiny `MyCompany.DiConfig` project containing only assembly attributes with `Scope.Transitive`, and have every other project `<ProjectReference>` it. This is the same pattern .NET teams already use for coordinating `[assembly: InternalsVisibleTo]`, shared analyzers, and cross-cutting attribute policy. No new mechanism required — transitivity over a common dependency is the answer. MSBuild is *not* used for declaring auto-deps; the principle that MSBuild is override-only stays intact.

**Conflict rules for transitive auto-deps:**

- **Consumer opt-outs always win.** `[NoAutoDep<T>]` or `[NoAutoDeps]` on a service in the consuming assembly suppresses any transitive auto-dep for that type/service, regardless of how many upstream assemblies declared it. Consumer sovereignty is a hard rule.
- **Multi-library union.** When multiple referenced assemblies each declare transitive auto-deps, their sets are silently unioned and deduplicated. No diagnostic fires on overlap.
- **Structural match still required.** `AutoDepsApply<TProfile, TBase>(Scope = Transitive)` from library A propagates the *rule*, but the match against `TBase` is evaluated in the consuming compilation against services in that consumer. A service in B only receives the profile if it structurally inherits/implements `TBase` — transitivity propagates the declaration, not a blanket attachment.
- **Glob pattern matching for transitive `AutoDepsApplyGlob`.** The glob pattern is evaluated against service **namespaces in the consuming assembly**, not the declaring assembly. A library publishing `[assembly: AutoDepsApplyGlob<ControllerDefaults>("*.Controllers.*", Scope = Transitive)]` can only match services whose namespaces fit the pattern in consumer code; library authors should therefore prefer broad, convention-based patterns (e.g., `"*.Controllers.*"`) over assembly-specific ones. This is documented explicitly in `docs/attributes.md` to help library authors pick sensible patterns.
- **IOC097 / IOC099 / IOC102 / IOC104 still fire across the boundary.** If a library declares an invalid transitive attribute, the consumer's compilation still surfaces the diagnostic, with the primary location on any consumer-side service the attribute would affect and a secondary location (metadata reference, non-navigable) pointing at the declaring assembly.

**Cross-assembly implementation notes for the planner:**

- **Incremental cache keys.** The resolver must key transitive-attribute inputs by referenced-assembly MVID (or name+version) rather than by attribute content. Changes in an upstream assembly invalidate the consumer's resolver cache; unchanged references do not.
- **Symbol equality.** Type symbols read from referenced-assembly metadata are distinct object references from locally-authored symbols. All type comparisons in the resolver use `SymbolEqualityComparer.Default`.
- **Diagnostic locations.** Consumer-side services are the primary location for any diagnostic caused by a transitive attribute. The declaring attribute's secondary location is a metadata reference — the user cannot navigate to source unless the declaring project is in the solution — so the primary location must stand alone as actionable.
- **Forward-compatibility with meta-packages.** If a future shipped package (the deferred `IoCTools.AspNetCore`) emits `[assembly: ...]` attributes into its own compilation with `Scope.Transitive`, consumers read them exactly like hand-written ones. The mechanism is package-shape-agnostic.
- **Cross-version generator tolerance.** A 1.6 generator in consumer B may encounter referenced assemblies on older `IoCTools.Abstractions` versions (1.5.x or earlier) that do not define `AutoDepScope`, `Scope = Transitive`, `NoAutoDepOpen`, or the other new attributes. The resolver filters references to those whose transitive reference chain resolves the `AutoDepScope` type symbol in the compilation — references predating 1.6 simply contribute nothing to the transitive set and do not error. Symmetrically, a 1.5.x generator in a consumer reading a 1.6 library's transitive attributes cannot interpret them; it behaves as if they were not there. The two directions are compatible by omission, not by explicit handshake.

### Profiles

A profile is a marker class implementing an empty `IAutoDepsProfile` interface. The marker interface lives in `IoCTools.Abstractions` (netstandard2.0) so declaring a profile never drags consumers into a newer target framework. Deps are added to the profile via assembly attributes:

```csharp
public sealed class ControllerDefaults : IAutoDepsProfile { }
public sealed class BackgroundDefaults : IAutoDepsProfile { }

[assembly: AutoDepIn<ControllerDefaults, IMediator>]
[assembly: AutoDepIn<ControllerDefaults, IMapper>]
[assembly: AutoDepIn<BackgroundDefaults, IServiceScopeFactory>]
```

The marker interface serves three purposes: it makes profile types discoverable by the generator, it enables IntelliSense filtering on the `TProfile` type argument, and it gates a diagnostic (IOC097) when a class used in `AutoDepIn<TProfile, ...>` forgets the marker.

### Profile Attachment

Three attachment mechanisms, in increasing specificity:

```csharp
// 1. Base class or implemented interface
[assembly: AutoDepsApply<ControllerDefaults, ControllerBase>]
[assembly: AutoDepsApply<BackgroundDefaults, BackgroundService>]

// 2. Namespace glob (string — structural necessity)
[assembly: AutoDepsApplyGlob<AdminDefaults>("*.Admin.Controllers.*")]

// 3. Per-service explicit attachment
[Scoped]
[AutoDeps<ReportingDefaults>]
public partial class ReportService { }
```

`AutoDepsApply<TProfile, TBase>` matches both base-class inheritance and interface implementation. The generator determines which at match time by inspecting the matched type symbol.

A single service may match multiple profiles. All matched profiles' deps are unioned, deduplicated, and merged with the universal set.

### Opt-Out

```csharp
[NoAutoDeps]                             // disable all auto-deps for this service
[NoAutoDep<TimeProvider>]                // disable one specific closed-type auto-dep
[NoAutoDepOpen(typeof(ILogger<>))]       // disable any auto-dep derived from an open-generic shape
[Scoped] public partial class LegacyService { }
```

`NoAutoDep<T>` works for closed types. `NoAutoDepOpen(typeof(T<>))` is the twin of `AutoDepOpen` — it suppresses any auto-dep derived from a matching open-generic shape, regardless of what the closure would yield for this service. This is the recommended per-service opt-out for `ILogger<>` because it does not force the service to repeat its own type name (`[NoAutoDepOpen(typeof(ILogger<>))]` on `MyService` is stable across renames; `[NoAutoDep<ILogger<MyService>>]` silently breaks when the class is renamed).

### Resolution Order

For any given service, the resolved auto-dep set is computed as follows:

1. Start with the universal set:
   - Built-in: if `Microsoft.Extensions.Logging.ILogger\`1` is discoverable in the compilation and `IoCToolsAutoDetectLogger` is not `false`, treat it as an implicit `AutoDepOpen(typeof(ILogger<>))` declaration.
   - Local: all `[assembly: AutoDep<T>]` and `[assembly: AutoDepOpen(typeof(T<>))]` declared in the service's own assembly (`Scope.Assembly` and `Scope.Transitive` alike).
   - Transitive: all `[assembly: AutoDep<T>(Scope = Transitive)]` and `[assembly: AutoDepOpen(typeof(T<>), Scope = Transitive)]` declared in any referenced assembly that transitively references `IoCTools.Abstractions`.
   - Open-generics are closed to the service's concrete type at this step.
2. Add deps from every profile attached to the service (via `AutoDepsApply`, `AutoDepsApplyGlob`, or `[AutoDeps<TProfile>]`). Profile contributions come from `AutoDepIn` declarations in the same assembly *or* in any referenced assembly when `Scope.Transitive` is set. Profile attachment rules (`AutoDepsApply`, `AutoDepsApplyGlob`) likewise honor `Scope.Transitive` — the rule propagates, the structural match is evaluated in the consuming compilation.
3. Subtract any type in `[NoAutoDep<T>]` on the service, and any type whose derivation matches a `[NoAutoDepOpen(typeof(T<>))]` on the service (matches the open-generic shape regardless of closure).
4. Remove the entire set if `[NoAutoDeps]` is present on the service.
5. Reconcile against the service's explicit `[DependsOn<T>]` set. The rule is: **explicit `[DependsOn<T>]` always wins over an auto-dep for the same type**, on the principle that the user wrote it deliberately. Customization is evaluated **per type-argument slot**, with one important caveat: `DependsOnAttribute<T1..Tn>` has an *attribute-wide* `external` flag (not a per-slot one), so when `external: true` is present it applies to every slot in that attribute, and every slot in that attribute is considered customized for reconciliation purposes. In a `[DependsOn<IFoo, IBar>(memberName1: "_specialFoo")]` without `external:` set, the `IFoo` slot carries customization while the `IBar` slot does not; each is reconciled independently. Two sub-cases per slot:
   - If the slot is bare (no `memberNameN` for its position, attribute-wide `external` at its default of `false`), it is redundant with the auto-dep. Emit IOC098 (info) to surface the redundancy; the resulting constructor is identical either way.
   - If the slot carries customization (position-indexed `memberNameN`, or attribute-wide `external: true` which implicitly customizes every slot in that attribute), it is a deliberate override. Suppress the auto-dep for that type, emit the `DependsOn` slot as-is, and do not fire IOC098.

Because `external` is attribute-wide, the code fix (described below) emits one `DependsOn` attribute per divergent-`external` group of migrated `[Inject]` fields. The runtime rule and the code-fix shape compose: divergent externals produce divergent attributes, each evaluated independently at step 5.
6. If the service has a user-authored manual constructor, the entire auto-dep set is skipped (existing manual-constructor behavior is reused).
7. When the service is declared across multiple `partial` class files, every attribute on every partial participates in the resolution. `[NoAutoDeps]`, `[NoAutoDep<T>]`, `[AutoDeps<TProfile>]`, and `[DependsOn<T>]` are unioned across partials before step 3 onward runs.

The resolved set is merged into the `[DependsOn<T>]` list that `ConstructorEmitter` already consumes, so the existing codegen path handles auto-deps without a parallel pipeline. Because merged auto-deps travel through the same downstream pipeline as explicit `[DependsOn<T>]`, they participate in all existing diagnostics (IOC001 no-implementation, IOC003 lifetime validation, etc.) without any new plumbing.

**Attribution data model for auto-deps.** The existing generator pipeline uses a `DependencySource` enum (values: `Inject`, `DependsOn`) to track per-dependency provenance through analysis and emission. Auto-deps need finer-grained attribution than a single new enum value because CLI output and diagnostics distinguish `auto-builtin:ILogger`, `auto-universal`, `auto-profile:<Name>`, and `auto-transitive:<Assembly>`. The implementation plan extends the attribution in one of two equivalent ways — either by expanding `DependencySource` with the four variants plus a sibling field carrying the profile or assembly name, or by introducing a parallel `AutoDepAttribution` value type threaded through `InheritanceHierarchyDependencies` and `ServiceClassInfo` alongside the existing `DependencySource`. The resolver emits this attribution as part of its output so that every downstream consumer (emitter, diagnostics pipeline, CLI resolvers, code fix) reads the same source-of-truth. Prior spec mentions of `AutoDepAttribution` in the CLI section refer to this same record. A consequence worth surfacing: declaring `[assembly: AutoDep<IUnregistered>]` where `IUnregistered` has no DI registration will fire IOC001 on every service in the assembly. This is desired behavior — a broken auto-dep should be loud — but the Documentation Changes entry for `docs/configuration.md` calls it out explicitly so the blast radius is not a surprise.

This runtime rule (explicit `DependsOn` wins) is consistent with the code fix's "Delete entirely" branch described below. The fixer deletes an `[Inject]` field only when the resulting service would have **no** `[DependsOn<T>]` for that type (auto-dep alone satisfies it). If the original `[Inject]` field carried customization that the fixer preserves as a `[DependsOn<T>(external: true, ...)]`, the resulting `DependsOn` then correctly wins over the auto-dep at gen time. The two rules compose without contradiction.

### Open-Generic Closure Rule

`AutoDepOpen(typeof(T<>))` closes the single unbound type parameter with the service's final concrete declared type. Worked examples:

- `OrderController` → `ILogger<OrderController>`.
- `PremiumOrderService : OrderService` → `ILogger<PremiumOrderService>` (the derived concrete type, not the base).
- `Repository<TEntity>` → the generated constructor parameter is typed `ILogger<Repository<TEntity>>`, which remains generic at codegen time and is closed by MS.DI at resolution. This is compatible with the standard open-generic registration that `Microsoft.Extensions.Logging` already performs for `ILogger<>`.

**Inheritance and `base()` chaining with auto-deps.** When a service has a base class that is also an IoCTools service, `ConstructorEmitter` chains `base(...)` with the base's dependencies. Auto-deps interact with this as follows: each level of the hierarchy gets its open-generic auto-dep closed to its own concrete type. For `PremiumOrderService : OrderService` under `AutoDepOpen(typeof(ILogger<>))`:

- `OrderService`'s generated constructor takes `ILogger<OrderService>` and stores it as `_logger`.
- `PremiumOrderService`'s generated constructor takes its own `ILogger<PremiumOrderService>` and ALSO an `ILogger<OrderService>` parameter that is forwarded via `base(..., orderServiceLogger)` to satisfy the base ctor.

The derived-class constructor therefore has both its own logger parameter and its base's logger parameter; the DI container injects two distinct `ILogger<T>` instances because MS.DI closes the open-generic registration per resolution. If the user wants only one logger, they suppress the derived-level auto-dep via `[NoAutoDepOpen(typeof(ILogger<>))]` on the derived class — which suppresses the derived-level closure, not the base's. The derived class then receives only `ILogger<OrderService>` forwarded from base. This behavior is counter-intuitive and must be surfaced in `docs/auto-deps.md` with a worked example; a test-matrix bullet covers the specific base/derived logger-distinctness scenario.

Multi-arity unbound generics (`typeof(IFoo<,>)`) have no universal "close with self" convention and are rejected at config-parse time (IOC100). The ecosystem's actual multi-arity generics — `IRequestHandler<TRequest, TResponse>`, `IValidator<TSource, TDest>` — use their type parameters for domain entities, not for the service type, so no default rule can apply.

The "final concrete service type" is the concrete class symbol that IoCTools is generating a constructor for — never an interface from `[RegisterAs<T>]`. A service with `[RegisterAs<IFoo>][RegisterAs<IBar>](InstanceSharing.Shared)` closed under `AutoDepOpen(typeof(ILogger<>))` still resolves to `ILogger<TheConcreteClass>`, not `ILogger<IFoo>`. The closure rule is "close with the declaring type," not "close with any registered interface."

## `[Inject]` Deprecation

### Timeline

- **1.6.0** — `[Inject]` is marked `[Obsolete(...)]` with a warning-severity diagnostic (IOC095). A Roslyn code fix is shipped. All first-party samples, test projects, and docs migrate off `[Inject]` (see "Sample and First-Party Migration" below).
- **1.7.0** — IOC095 is upgraded to error-severity (the `[Obsolete]` attribute gains `error: true`). Consumers must migrate or suppress.
- **2.0.0** — `InjectAttribute` is removed from `IoCTools.Abstractions`. The generator's `InjectFieldAnalyzer` and `InjectUsageValidator` are deleted.

During the 1.6.x window, consumers can modulate the diagnostic severity via the MSBuild property `IoCToolsInjectDeprecationSeverity` (values: `Error`, `Warning`, `Info`, `Hidden`), following the existing precedent of `IoCToolsNoImplementationSeverity`. This gives teams a pressure valve for staged migrations without silencing the diagnostic entirely. In 1.7.0 the property is honored only above its default (i.e. it can raise to error but not lower below warning).

**Soft-rollout posture for first-upgrade teams.** IOC095 defaults to warning severity in 1.6.0, per the approved deprecation timeline. A team upgrading a large codebase from 1.5.x to 1.6.0 will see warnings proportional to their `[Inject]` usage — potentially hundreds on first build. This is intentional (the deprecation must be visible) but not catastrophic because a Roslyn code fix ships alongside the diagnostic. The recommended first-upgrade workflow is documented in the migration guide: (1) upgrade the IoCTools packages, (2) optionally set `IoCToolsInjectDeprecationSeverity=Info` to silence warnings during a short triage window, (3) run the code fix across the solution, (4) remove the severity override. Teams that prefer in-place progress can leave the warnings on and migrate file-by-file with the quick-fix light-bulb.

### `[Inject][ExternalService]` Migration Mapping

The existing codebase idiomatically pairs `[Inject]` with a sibling `[ExternalService]` attribute on the same field to flag external dependencies. `InjectFieldAnalyzer.GetInjectedFieldsForTypeWithExternalFlag` reads both attributes and threads the external-service flag through the pipeline. The code fix maps this pairing as follows:

- `[Inject][ExternalService] private readonly IFoo _foo;` → `[DependsOn<IFoo>(external: true)]` on the class (with `memberName1: "_foo"` preserved if the field name is non-default, per the standard migration rules).
- When coalescing multiple fields into a single `[DependsOn<T1, T2, ...>]`, any field whose `[ExternalService]` flag diverges from the group's majority is split into its own `[DependsOn<T>]` attribute — because `external:` is attribute-wide on `DependsOn`. This is the same divergent-external splitting rule described in the Code Fix Behavior section above, with `[ExternalService]` explicitly named as one of the input forms that trigger it.

### Parity Baseline

Per the parity analysis run during design, `[Inject]` and `[DependsOn<T>]` are behaviorally equivalent in every dimension that affects runtime or generated code:

- Both produce private readonly fields.
- Both support external service flagging.
- Both honor the same naming conventions (`NamingConvention`, `stripI`, `prefix`).
- Both support generic type substitution identically.
- Neither controls instance sharing or lifetime at the field level — those are service-level concerns.

The one migration-relevant divergence is field-name preservation: `[Inject] private readonly IFoo _customName;` preserves `_customName` as-is, whereas `[DependsOn<IFoo>]` generates a name via the naming convention. `[DependsOn<T>]` already supports per-dep name overrides via `memberName1..N` parameters on the existing overloads. The code fix uses these to produce behaviorally identical output.

This means no pre-1.6 parity work is required. The migration is mechanical and lossless at the library level; all nuance lives in the code fix.

### Code Fix Behavior

The code fix for IOC095 operates per-field. For each `[Inject]` field on a service, it chooses one of three outcomes:

1. **Delete entirely** — the field's type is covered by a resolved auto-dep for that service (universal or via an attached profile), **and** the field carries no customization beyond the default (no explicit field attributes, default camelCase naming, no `external: true` flag). The user never needed to declare it. The generator will supply the same dependency implicitly.
2. **Convert to bare `[DependsOn<T>]`** — the field is not covered by an auto-dep, and the field name matches the name `DependsOn` would generate by default. A clean `[DependsOn<T>]` attribute is added to the class.
3. **Convert to `[DependsOn<T>(...)]` preserving customization** — the field has a custom name, is flagged external, or otherwise diverges from defaults. The fix emits the appropriate `DependsOn` overload with the customization round-tripped. Custom names go to `memberName1..N`; external-service semantics are preserved via the `external:` parameter on `DependsOn`. If the field was covered by an auto-dep but the user deliberately customized it, the conversion wins at gen time (per the Resolution Order rule above) and the auto-dep is suppressed for that type.

Multiple fields on the same service are coalesced into a single `[DependsOn<T1, T2, ...>]` attribute when possible, with `memberNameN` parameters filled in only for positions with custom names, and separate `DependsOn` attributes emitted when `external:` flags diverge across positions.

**Interaction with `IoCToolsAutoDetectLogger=false`.** The fixer reads the consumer project's MSBuild properties (including `IoCToolsAutoDetectLogger`) and resolves the auto-dep set accordingly. When detection is disabled, the built-in `ILogger` is not part of the resolved set, so a migrated `[Inject] private readonly ILogger<MyService> _log;` is *converted* to `[DependsOn<ILogger<MyService>>]` rather than deleted. This keeps the fixer's behavior consistent with the generator: both read the same inputs and produce the same decisions. The same logic applies to `IoCToolsAutoDepsDisable=true` — detection, universal, transitive, and profile-sourced auto-deps all yield an empty resolved set, so every migrated `[Inject]` field becomes a `[DependsOn<T>]` attribute.

The fixer must resolve the service's auto-dep set at fix time. This requires walking the `Compilation` for assembly attributes (`AutoDep<T>`, `AutoDepOpen`, `AutoDepIn`, `AutoDepsApply`, `AutoDepsApplyGlob`) and the service's own `[AutoDeps<TProfile>]` / `[NoAutoDeps]` / `[NoAutoDep<T>]` attributes — including attributes across all `partial` class files for the service. The resolution logic is the same algorithm the generator uses at codegen time; it is factored into a shared library (`IoCTools.Generator.Shared.AutoDepsResolver`) accessible from both the generator and the code-fix provider. The resolver's output must be an equatable value type (consistent with the existing `ServiceClassInfo` convention) so incremental-generator caching is not broken by assembly-attribute changes in unrelated parts of the compilation.

**Equatability prerequisite for incremental caching.** `ServiceClassInfo` today carries `INamedTypeSymbol`/`SemanticModel` references and is not itself value-equatable in the strict sense incremental generators require. `AutoDepsResolver` output must be equatable so that unchanged inputs produce cache hits in the generator pipeline. The implementation plan therefore treats resolver-output equatability as a *separate prerequisite task* — introducing a value-typed attribution record (keyed by symbol identifiers like metadata name + assembly MVID) rather than retrofitting equatability onto `ServiceClassInfo` itself. This is a planner-visible constraint, not a claim that `ServiceClassInfo` changes.

**Packaging of `AutoDepsResolver`.** Source generators, analyzers with code fixes, and the out-of-process CLI load in three different environments. The resolver is built as a standalone `netstandard2.0` class library with no external runtime dependencies beyond Roslyn's `Microsoft.CodeAnalysis.CSharp`. It is physically shipped via link-in (source inclusion) in the generator assembly, the analyzer/code-fix assembly, and the CLI project. This follows the existing IoCTools pattern of keeping generator internals free of NuGet-time coupling and avoids ALC-load issues. A single canonical source location (`IoCTools.Generator.Shared/AutoDepsResolver.cs`) is linked via `<Compile Include="..." Link="..." />` in all three consumers, so there is one source of truth and three physical copies of the compiled code.

### Unaffected Siblings: `[InjectConfiguration]`

`[InjectConfiguration]` is a distinct attribute with distinct semantics: it binds configuration sections to fields via `Microsoft.Extensions.Options` and related infrastructure. It is **not** part of this deprecation. IOC095 does not fire on `[InjectConfiguration]`. The code fix ignores it. Its paired class-level attribute `[DependsOnConfiguration<T>]` remains the declarative alternative and continues to be supported on equal footing. The deprecation applies only to `InjectAttribute`.

### Collection Injection (`IEnumerable<T>`) Via Auto-Deps

Collection injection works with auto-deps using standard closed-type generics. `[assembly: AutoDep<IEnumerable<IValidator>>]` declares that every service receives an `IEnumerable<IValidator>` constructor parameter (which MS.DI populates with every `IValidator` registration). From the auto-deps resolver's perspective, `IEnumerable<IValidator>` is a closed type like any other — no special handling. `[NoAutoDep<IEnumerable<IValidator>>]` matches the same closed type and suppresses it per service.

`AutoDepOpen(typeof(IEnumerable<>))` is legal syntactically and closes to `IEnumerable<TService>` per service, but this is almost never what users want (services rarely consume a collection of themselves); no built-in rejection, but the Documentation Changes entry for `docs/attributes.md` calls out that this is a foot-gun with an illustrative note.

### Residual Migration Losses

Two minor losses are accepted and documented:

- Field-level sibling attributes on `[Inject]` fields (`[Inject][SomeMetadata] private readonly IFoo _foo;`) are dropped by the fix. The generator never processed them; no runtime behavior changes. The migration guide notes this and suggests manual re-attachment at the class level if semantically meaningful.
- `[Inject]` rejects non-private field access modifiers via `InjectUsageValidator`. `[DependsOn<T>]` has no such restriction; migrated code is more permissive than the original. This is a strict widening, not a regression.

### Sample and First-Party Migration

The IoCTools Sample project (`IoCTools.Sample`) contains approximately 395 `[Inject]` occurrences across 21 files, covering inheritance, collection injection, config injection, external-service flagging, conditional services, and background services. This is a real migration workstream, not a trivial find-and-replace.

The migration policy is:

- **Most services migrate via the code fix.** The fixer runs across the Sample project as part of the 1.6.0 release preparation; the resulting diff is committed as "chore: migrate Sample project off `[Inject]`." This exercises the fixer on real production-like code and surfaces edge cases before publication.
- **A small set of Sample services intentionally retain `[Inject]`** to exercise IOC095 during ongoing development. These live in a dedicated file (`IoCTools.Sample/Services/InjectDeprecationExamples.cs`) with a top-of-file comment explaining their purpose. They are removed in 1.7 when IOC095 becomes error-severity.
- **Services with architectural-limit interactions** (protected/internal field modifiers, complex generic constraints) receive manual migration review. Those cases are already documented in CLAUDE.md as architectural limits; the migration doesn't change the limit surface.

First-party packages beyond the core generator:

- **`IoCTools.Testing`** — the test fixture generator. Audit required; any `[Inject]` usage inside the generated test fixtures themselves must migrate to `[DependsOn<T>]` codegen. No public API changes.
- **`IoCTools.FluentValidation`** — the validation source generator. Audit required; if its generated validator classes use `[Inject]`, the generation output switches to `[DependsOn<T>]`. The package version bumps to `1.6.0` in lockstep with the core surface.
- **`IoCTools.Tools.Cli`** — the `ioc-tools` CLI. No source-generator surface; no `[Inject]` migration needed. Version bumps to `1.6.0` for release coherence and gains the `ioc-tools auto-deps` subcommand (see CLI Integration below).

All first-party package versions move to `1.6.0` together, per the coherent-release-surface policy established in the 1.5.1 design.

## Configuration

### Assembly Attributes (Primary Surface)

All user-authored auto-deps policy lives in assembly attributes. This is the idiomatic modern .NET pattern (see `[assembly: InternalsVisibleTo]`, `[assembly: SuppressMessage]`, `[assembly: MediatorOptions]`) and is consistent with the rest of the IoCTools generic-attribute surface.

| Attribute | Purpose |
|---|---|
| `[assembly: AutoDep<T>]` | Universal closed-type auto-dep. |
| `[assembly: AutoDepOpen(typeof(T<>))]` | Universal single-arity open-generic auto-dep. |
| `[assembly: AutoDepIn<TProfile, T>]` | Profile-scoped closed-type auto-dep. |
| `[assembly: AutoDepsApply<TProfile, TBase>]` | Attach profile to all services inheriting or implementing `TBase`. |
| `[assembly: AutoDepsApplyGlob<TProfile>("pattern")]` | Attach profile by namespace glob. |

### Service-Level Attributes

| Attribute | Purpose |
|---|---|
| `[AutoDeps<TProfile>]` | Explicitly attach a profile to this service. |
| `[NoAutoDeps]` | Suppress all auto-deps on this service. |
| `[NoAutoDep<T>]` | Suppress a specific auto-dep on this service. |

### Attribute Scope Reference

Every attribute introduced by this feature, at a glance:

| Attribute | Target | Scope | Purpose |
|---|---|---|---|
| `AutoDep<T>` | assembly | universal | Add a closed-type auto-dep to every service. Supports `Scope` (default `Assembly`). |
| `AutoDepOpen(typeof(T<>))` | assembly | universal | Add a single-arity open-generic auto-dep, closed per service. Supports `Scope`. |
| `AutoDepIn<TProfile, T>` | assembly | profile definition | Add a closed-type dep to a profile. Supports `Scope`. |
| `AutoDepsApply<TProfile, TBase>` | assembly | profile attachment | Apply a profile to every service whose base class or implemented interface is `TBase`. Supports `Scope`. |
| `AutoDepsApplyGlob<TProfile>("pattern")` | assembly | profile attachment | Apply a profile by namespace glob. Supports `Scope`. |
| `AutoDeps<TProfile>` | class | profile attachment | Apply a profile to this specific service. |
| `NoAutoDeps` | class | opt-out | Suppress all auto-deps for this service. |
| `NoAutoDep<T>` | class | opt-out | Suppress a specific closed-type auto-dep for this service. |
| `NoAutoDepOpen(typeof(T<>))` | class | opt-out | Suppress any auto-dep derived from an open-generic shape (twin of `AutoDepOpen`). |
| `IAutoDepsProfile` | interface | marker | Identifies a class as a profile type (empty marker interface). |
| `AutoDepScope` | enum | (not an attribute) | Values: `Assembly` (default), `Transitive`. Used as the `Scope` property on assembly-level attributes. |

Generic profile types (`ControllerDefaults<T>` style) are not supported in 1.6 — profile classes must be non-generic. The generator emits IOC104 (Error) when a type decorated with `IAutoDepsProfile` or used as `TProfile` in any profile-aware attribute is generic. This keeps closure semantics tractable and defers the question of "what does the profile's own generic parameter mean" to a future release.

### MSBuild Overrides (Secondary)

MSBuild properties are override-only. They never declare auto-deps; they only modulate behavior at build time (CI, per-environment debugging, escape hatches).

| Property | Purpose |
|---|---|
| `IoCToolsAutoDepsDisable` | Boolean kill switch. When `true`, the entire feature is a no-op. |
| `IoCToolsAutoDepsExcludeGlob` | Namespace glob. Services matching are treated as if they had `[NoAutoDeps]`. |
| `IoCToolsAutoDepsReport` | Boolean. When `true`, every generated constructor file includes a debug comment block listing resolved auto-deps and their sources. |
| `IoCToolsAutoDetectLogger` | Boolean (default `true`). When `false`, disables the built-in auto-detection of `Microsoft.Extensions.Logging.ILogger<T>`. Does not affect universal `AutoDep<T>` or `AutoDepOpen` declarations. |
| `IoCToolsInjectDeprecationSeverity` | Modulates IOC095 severity within its allowed band (`Error`, `Warning`, `Info`, `Hidden` in 1.6; raise-only in 1.7). Follows the precedent set by `IoCToolsNoImplementationSeverity`. |

## Diagnostics

| Code | Severity | Trigger |
|---|---|---|
| IOC095 | Warning (1.6) → Error (1.7) → Removed (2.0) | `[Inject]` usage. Ships with code fix. |
| IOC096 | Info | `[NoAutoDep<T>]` references a type not in the service's resolved auto-dep set, or `[NoAutoDepOpen(typeof(T<>))]` references an open-generic shape with no matching auto-dep derivation for this service (typo or stale opt-out). |
| IOC097 | Warning | `AutoDepIn<TProfile, ...>` or `AutoDepsApply<TProfile, ...>` targets a class that does not implement `IAutoDepsProfile`. |
| IOC098 | Info | Service has `[DependsOn<T>]` for a type also covered by an auto-dep (built-in detection, local universal, transitive, or profile-sourced). Deduped silently; surfaced for visibility. The message names the auto-dep's source (e.g., `auto-builtin:ILogger`, `auto-universal`, `auto-transitive:<AssemblyName>`, `auto-profile:<Name>`) so users can pick the right remediation. Does not fire when the auto-dep source is inactive — e.g., when `IoCToolsAutoDetectLogger=false` disables detection, `[DependsOn<ILogger<MyService>>]` does not fire IOC098. |
| IOC099 | Info | `AutoDepsApply<TProfile, TBase>` or `AutoDepsApplyGlob<TProfile>` matches zero services in the assembly (stale rule). |
| IOC100 | Error | `AutoDepOpen` given a multi-arity unbound generic (`typeof(IFoo<,>)`). No universal closing convention. |
| IOC101 | Error | `AutoDepOpen` given a non-generic type. Suggests `AutoDep<T>`. |
| IOC102 | Error | `AutoDepOpen` closure for a matching service would violate the unbound's type-parameter constraints. Primary location: the service declaration (where codegen would fail). Secondary location: the `AutoDepOpen` assembly attribute. |
| IOC103 | Error | `AutoDepsApplyGlob<TProfile>` has an invalid glob-pattern argument. The validator uses the same glob grammar as the existing `IoCToolsIgnoredTypePatterns` / `IoCToolsSkipAssignableTypes*` pattern surface (the `Glob` helper in the generator). Empty strings, unterminated character classes, and unsupported metacharacters all fire this diagnostic. |
| IOC104 | Error | A profile type used in `AutoDepIn`, `AutoDepsApply`, `AutoDepsApplyGlob`, or `AutoDeps` is generic. Profiles must be non-generic in 1.6. |
| IOC105 | Info | Redundant profile attachment: a service is attached to the same profile via more than one path (e.g., both `AutoDepsApplyGlob` match and explicit `[AutoDeps<TProfile>]` on the class). The attachment is deduped silently; the diagnostic surfaces the redundancy. |

All diagnostics include `HelpLinkUri` following the scheme `https://github.com/sansiquay/IoCTools/blob/main/docs/auto-deps.md#iocXXX` (lowercased anchor), pointing at a per-diagnostic section of the canonical `docs/auto-deps.md` reference. IOC095 specifically links to `docs/migration.md#migrating-from-15x-to-16x` instead. The URL scheme is fixed now so that the `HelpLinkUri` values emitted by the generator in 1.6.0 match the documentation URLs at release time.

## CLI Integration

The `ioc-tools` CLI already ships a rich inspection surface: `graph`, `why`, `explain`, `evidence`, `doctor`, `compare`, `suppress`, `config-audit`, `validators`, `validator-graph`, and the existing `profile` subcommand. The existing `profile` subcommand does project-load timing plus service and configuration counts — it is a build/project-load benchmark, not runtime performance profiling. The plural/singular distinction is still meaningful: singular `profile` covers project-load benchmarking (existing); plural `profiles` covers auto-deps profile introspection (new). The distinction is documented, not renamed.

Rather than bolt a parallel `auto-deps` subcommand onto the existing surface, the 1.6 CLI story is to **make auto-deps first-class in the existing inspection subcommands** and add two narrow new ones (`profiles` plural, `migrate-inject`). The `config-audit`, `validators`, and `validator-graph` subcommands are unchanged — they do not touch the dep graph and have nothing auto-deps-relevant to surface. The `suppress` subcommand, which generates suppression files from live diagnostics, gains awareness of the new diagnostic codes IOC095 through IOC105 so generated suppressions cover the new surface.

All integrations route through the same `IoCTools.Generator.Shared.AutoDepsResolver` library the generator and the code-fix provider use. The `migrate-inject` subcommand also depends on a sibling shared library — `IoCTools.Generator.Shared.InjectMigrationRewriter` — a pure `SyntaxNode → SyntaxNode` Roslyn transform consumed by both the IDE-hosted `CodeFixProvider` and the headless CLI. Same link-in packaging pattern as `AutoDepsResolver`. One resolver plus one rewriter, each with multiple consumers, no runtime coupling.

**Cross-version tolerance.** The CLI's `MSBuildWorkspace` loads consumer projects that may still reference `IoCTools.Abstractions` 1.5.x, which predates `AutoDep<T>`, `AutoDepOpen`, `AutoDepIn`, `AutoDepsApply`, `AutoDepsApplyGlob`, `AutoDeps<TProfile>`, `NoAutoDeps`, `NoAutoDep<T>`, and `IAutoDepsProfile`. When the resolver runs against a compilation in which these symbols are not present, it returns an empty auto-dep set and does not error. `migrate-inject` emits a one-line notice in this case ("target project references IoCTools.Abstractions < 1.6.0; the `Delete entirely` migration branch is disabled for this project, conversions to `[DependsOn<T>]` still run"). This keeps the CLI usable during staged ecosystem upgrades where some projects in a solution are still on 1.5.x.

**Kill-switch interaction with `migrate-inject`.** When `IoCToolsAutoDepsDisable=true` or a project matches `IoCToolsAutoDepsExcludeGlob`, the resolver returns an empty auto-dep set for that project's services. As a direct consequence, the `migrate-inject` "Delete entirely" branch never fires — every `[Inject]` field converts to a `[DependsOn<T>]` attribute, including ones that would have been covered by an auto-dep in a non-disabled build. This is the correct behavior (the kill switch by definition means auto-deps are not trusted as substitutes for explicit declarations), but the spec states it so the planner does not have to re-derive it.

**Concurrency posture.** `migrate-inject` processes documents sequentially in 1.6 to avoid locking complexity around shared `Compilation` state and to keep output deterministic for CI diffs. `AutoDepsResolver` is written to be thread-safe (no mutable shared state between resolution calls) so future parallelization remains an option. Parallel `migrate-inject` is explicitly deferred.

### Cross-command flag: `--hide-auto-deps`

The universal auto-dep (`ILogger<T>`, possibly `TimeProvider`) adds one or two dependency nodes to every service. For a graph of a service with three business-logic deps, the two implicit ones represent 40% of the visual noise. The `--hide-auto-deps` flag is supported on `graph`, `why`, `explain`, and `evidence` — it collapses implicit auto-dep nodes/rows out of the output, leaving the explicit surface. The complementary `--only-auto-deps` inverts the view for audit use cases.

The default posture is **show everything** — users seeing the full dep set by default preserves correctness and surfaces surprises. Hiding is an explicit opt-in.

**Plumbing reality:** `CommandLineParser` uses a distinct `ParseX` method per subcommand with per-method `NormalizeKey`/`IsFlag` tables. There is no cross-command flag system today, so adding `--hide-auto-deps` and `--only-auto-deps` touches four parsers plus their normalization tables. The implementation plan should factor a small `CommonAutoDepsOptions` helper (a record of the two boolean flags plus a shared parsing routine) that each of the four parsers composes in, rather than copy-pasting the plumbing. This is acknowledged as real work, not a free annotation.

**Behavior when `why <type> <dep>` is asked about an auto-dep with `--hide-auto-deps` set:** the flag is an output filter for list/graph views, not a muzzle on the direct target of a query. `why` always produces a full attribution block for the explicitly-asked-about dep, auto-dep or not. `--hide-auto-deps` on `why` only affects downstream context (e.g., related deps shown in an extended view), not the primary answer.

**Mutual exclusion of `--hide-auto-deps` and `--only-auto-deps`:** the two flags are semantically contradictory. When both are passed to the same command, the CLI exits with a non-zero status and a clear message identifying the conflict. This is enforced in the parser, not at runtime.

### `graph` — visual attribution

Today `ioc-tools graph <type>` renders the dep tree via `GraphPrinter` operating on `RegistrationSummary.Records` — a flat list of service/impl registrations without per-dependency node metadata. In 1.6, each node gains a source-attribution marker:

- explicit `[DependsOn<T>]` — unmarked (the business-logic default)
- universal auto-dep — marked with `ℹ` (dim/gray in terminal output)
- profile-sourced auto-dep — marked with `▣` plus the profile name in the node label

This is **not a printer-only change.** Supporting per-dependency source attribution requires augmenting the data path upstream of `GraphPrinter`: either extending `RegistrationSummaryBuilder` to emit per-dep source metadata, or adding a parallel inspection pass via `ServiceFieldInspector` that threads auto-dep attribution into the summary. The implementation plan must include this data-model work as a first-class task; treating the change as cosmetic would underestimate it.

A legend is emitted at the bottom of non-JSON outputs. Both JSON paths (`--format json` and the legacy `--json` flag) gain a `source` field on every node with one of these values:

- `"explicit"` — the dep is declared via `[DependsOn<T>]` on the service.
- `"auto-universal"` — the dep comes from an `[assembly: AutoDep<T>]` or `[assembly: AutoDepOpen(...)]` in the same assembly.
- `"auto-profile:<ProfileName>"` — the dep comes from a profile attached to the service.
- `"auto-transitive:<AssemblyName>"` — the dep comes from a `Scope.Transitive` declaration in a referenced assembly.
- `"auto-builtin:ILogger"` — the dep is the auto-detected MEL `ILogger<T>`.

**Attribution precedence when multiple sources produce the same closed dep type.** A service can end up with the same closed type (e.g. `ILogger<OrderController>`) contributed by more than one source — for example, local universal `[assembly: AutoDepOpen(typeof(ILogger<>))]` plus transitive `[assembly: AutoDepOpen(typeof(ILogger<>), Scope = Transitive)]` from a referenced library. Dedup wins at codegen (one constructor parameter), but the attribution tag shown in `graph`/`evidence` follows a precedence: **explicit → auto-profile → auto-universal → auto-transitive → auto-builtin**. The primary tag is the most-specific source. `why` always shows every contributing source so users see the full picture.

The two JSON paths remain divergent in shape as they are today, but both carry the new attribution so external tooling on either path can pivot. Reconciling the two JSON shapes is out of scope for this spec.

`--hide-auto-deps` collapses both marker classes into nothing, leaving a graph of just the service's declared business-logic deps.

### `why` — source attribution as the debugging centerpiece

Today `ioc-tools why <type> <dep>` traces one dependency's origin via `WhyPrinter`, which consumes `ServiceFieldReport.DependencyFields` where each dep carries a free-form `Source` string. Auto-deps make `why` the primary "where did this come from?" tool, but the structured output shown below requires that dep attribution flow as structured data, not as a retrofit into the existing string. The implementation plan must add a new `AutoDepAttribution` record (source kind, declaring file/line of the assembly attribute, profile name if applicable, suppression hint) threaded from the resolver through `ServiceFieldInspector` into `DependencyFields`. The current string-typed `Source` field is retained for pre-auto-dep origin traces.

The output gains explicit source blocks:

```
ILogger<OrderController> on OrderController
  source: auto-builtin:ILogger (Microsoft.Extensions.Logging.ILogger<T> detected in references)
  closed to: ILogger<OrderController> (service's concrete type)
  disable detection: IoCToolsAutoDetectLogger=false
  suppress here: [NoAutoDepOpen(typeof(ILogger<>))]

ITracer on OrderController
  source: auto-transitive:Acme.Platform
  declared at: <metadata reference> — Acme.Platform/DiConfig.cs (not navigable)
  contributes: [assembly: AutoDep<ITracer>(Scope = AutoDepScope.Transitive)]
  suppress here: [NoAutoDep<ITracer>]

IMediator on OrderController
  source: auto-profile:ControllerDefaults
  attached by: [assembly: AutoDepsApply<ControllerDefaults, ControllerBase>]
  declared at: Program.cs:18
  contributes: [assembly: AutoDepIn<ControllerDefaults, IMediator>] (Program.cs:19)
  suppress here: [NoAutoDep<IMediator>] or remove from profile

IMetrics on OrderController
  source: auto-universal
  declared at: [assembly: AutoDep<IMetrics>] Program.cs:12
  suppress here: [NoAutoDep<IMetrics>]

IPaymentService on OrderController
  source: explicit
  declared at: OrderController.cs:14 via [DependsOn<IPaymentService>]
```

When a dep has multiple sources (universal + explicit, or multiple profile attachments), all are listed with the precedence order visible.

### `explain` — narrative integration

Today `ioc-tools explain <type>` narrates a service. In 1.6 the narrative includes auto-deps as prose: "`OrderController` receives `IMediator` from the `ControllerDefaults` profile, attached via base-class match against `ControllerBase`, and `ILogger<OrderController>` from the universal `AutoDepOpen` declaration in `Program.cs`." This is the human-readable surface for docs, onboarding, and code reviews. `--hide-auto-deps` shortens the narrative to business-logic-only.

### `evidence` — authoritative surface

`ioc-tools evidence` is the canonical registration-evidence artifact; it is already what external audits consume. In 1.6 each service's evidence block lists its resolved auto-dep set alongside its explicit declarations, attributed by source. This is the most important integration for trust: evidence artifacts automatically gain auto-dep visibility without any consumer-side change.

### `doctor` — preflight checks

`ioc-tools doctor` is the health check. Three auto-dep-specific checks land in 1.6:

- Every universal auto-dep type (`AutoDep<T>`, closed form of `AutoDepOpen`) has at least one DI registration discoverable by the CLI. Catches "broken auto-dep spams IOC001 across the whole assembly" before it happens.
- No `AutoDepsApply` or `AutoDepsApplyGlob` rule matches zero services (same signal as IOC099 but aggregated to one report line per stale rule).
- No profile type is declared with `IAutoDepsProfile` but unreferenced by any `AutoDepIn`/`AutoDepsApply`/`AutoDeps` usage (dead profile detection).

### `profiles` — new plural subcommand

The existing `ioc-tools profile` subcommand is unrelated (runtime/perf profiling) and cannot be repurposed without a breaking rename. The new subcommand is **`profiles`** (plural):

```
ioc-tools profiles                    # list all profiles, each with their contributed deps
ioc-tools profiles --matches          # also list the services each profile attaches to
ioc-tools profiles <ProfileName>      # drill into one profile (deps + matches + sources)
```

Naming distinction: singular `profile` = project-load benchmarking (existing); plural `profiles` = auto-deps profile introspection (new). A doc note clarifies the distinction.

The `<ProfileName>` positional argument accepts either a simple name (e.g., `ControllerDefaults`) or a fully-qualified name (e.g., `MyApp.DiProfiles.ControllerDefaults`). When a simple name matches profile types in more than one namespace, the CLI exits with a non-zero status listing all candidates and prompting the user to disambiguate. Exact fully-qualified names always match precisely one type.

### `migrate-inject` — bulk `[Inject]` migration

The Roslyn code fix is IDE-only. Teams on CI pipelines, non-IDE editors, or scripted migration paths need a headless equivalent. The new subcommand:

```
ioc-tools migrate-inject [--dry-run] [--path <dir>]
```

Invokes the same transform logic as the IDE light-bulb in bulk across a project or solution. `--dry-run` prints the would-be diffs without writing. On completion it emits a summary (files touched, fields deleted because auto-dep covered them, fields converted to `[DependsOn<T>]`, fields converted with `memberName*` preservation).

The subcommand name follows the hyphenated single-token convention of the existing CLI (`validator-graph`, `config-audit`, `fields-path`). It also reserves `migrate-` as a prefix namespace for future migration subcommands.

**How it actually works mechanically.** The Roslyn `CodeFixProvider` is IDE-hosted and not directly runnable headless. However, the code fix's core logic — "given an `[Inject]` field and a resolved auto-dep set, produce a replacement `SyntaxNode`" — is a pure function. That function is factored into `IoCTools.Generator.Shared.InjectMigrationRewriter`, a `SyntaxNode → SyntaxNode` rewriter with no IDE or workspace coupling. The rewriter is consumed by:

1. The IDE-hosted `CodeFixProvider`, which invokes it per-field when the user hits the light-bulb.
2. The CLI, which walks the `MSBuildWorkspace` (already loaded via `ProjectContext`), discovers every `[Inject]` usage, invokes the rewriter, and writes the modified syntax trees back to disk.

Same link-in packaging as `AutoDepsResolver`: one canonical source location, three physical copies (generator assembly for in-process reuse, analyzer assembly for the code-fix host, CLI assembly for headless execution). This means there is exactly one code path that migrates `[Inject]` to `[DependsOn<T>]`, and it behaves identically in the IDE and in CI.

This is the single highest-leverage addition to the CLI for the deprecation story — it converts the migration from "hope your team has IDE support for code fixes" into "run one command."

### Deferred to 1.7

- **`compare` diff mode for auto-dep resolution.** A PR adding `[assembly: AutoDep<T>]` changes the implicit dep set of every service in the assembly; `ioc-tools compare` could surface this as a structured diff. Powerful but adds real complexity (baseline state capture, canonical-form comparison). Ship in 1.7 once real users have articulated what they want diffed.
- **`auto-deps validate` standalone subcommand.** Pre-build CI lint of assembly-attribute configs (IOC100-IOC105 surfaced without running the generator). Useful but duplicates diagnostics that the generator produces anyway; defer until someone actually asks.

## Debug Report

When `IoCToolsAutoDepsReport=true`, every generated constructor file gains a leading comment block:

```
// === Auto-Deps Report for OrderController ===
// Universal:
//   - ILogger<OrderController>            (from AutoDepOpen(typeof(ILogger<>)))
//   - TimeProvider                        (from AutoDep<TimeProvider>)
// Profile: ControllerDefaults             (matched by AutoDepsApply<ControllerDefaults, ControllerBase>)
//   - IMediator
//   - IMapper
// Explicit (DependsOn):
//   - IPaymentService
// Suppressed:
//   (none)
```

The report resolves the "where did this dep come from?" question in one read and is invaluable for debugging profile-attachment rules. It is opt-in to avoid noise in normal builds.

## Documentation Changes

The feature touches enough user-facing surface that documentation updates are a first-class deliverable, not an afterthought. Concrete file-by-file plan:

- **`docs/auto-deps.md`** — **new file**; the canonical auto-deps reference and the anchor target for every IOC095-IOC105 `HelpLinkUri`. Covers concepts (universal, profile, transitive, built-in detection), complete attribute reference with worked examples, the inheritance + base-ctor-chaining edge case, the opt-out ladder, the shared-project pattern for solution-wide policy, and a "recipes" section showing typical setups (pure greenfield, large legacy codebase migration, multi-team library ecosystem). This file is the primary onramp for users encountering the feature for the first time.
- **`docs/attributes.md`** — add entries for `AutoDep<T>`, `AutoDepOpen`, `AutoDepIn<TProfile, T>`, `AutoDepsApply<TProfile, TBase>`, `AutoDepsApplyGlob<TProfile>`, `AutoDeps<TProfile>`, `NoAutoDeps`, `NoAutoDep<T>`, `NoAutoDepOpen`, `IAutoDepsProfile`, and the `AutoDepScope` enum with its `Assembly` and `Transitive` values. Cover the auto-detection of `ILogger<T>` as the one built-in universal auto-dep, with a note on what qualifies for auto-detection versus explicit declaration. Add a callout on the `AutoDepsApplyGlob` + `Scope.Transitive` pattern-authoring subtlety (patterns evaluate against consumer namespaces). Mark `InjectAttribute` as deprecated with a link to the migration guide.
- **`docs/getting-started.md`** — rewrite the "first service" example to use `[DependsOn<T>]` + universal `AutoDepOpen(typeof(ILogger<>))`, eliminating the `[Inject] ILogger<T>` line from the onboarding surface.
- **`docs/migration.md`** — add a new top-level section "Migrating from 1.5.x to 1.6.x" covering the `[Inject]` deprecation, the code fix, the resulting `[DependsOn<T>]` style, the `memberName1..N` preservation behavior, the residual migration losses, the timeline to 2.0, the opt-out ladder for auto-detected `ILogger<T>` (from `[NoAutoDepOpen(typeof(ILogger<>))]` through `IoCToolsAutoDetectLogger=false` to `IoCToolsAutoDepsDisable=true`), and a dedicated subsection on the shared-project pattern for solution-wide auto-dep policy via `Scope.Transitive`.
- **`docs/diagnostics.md`** — add IOC095 through IOC105 with descriptions, examples of triggering code, and remediation guidance.
- **`docs/configuration.md`** — add the five new MSBuild properties (`IoCToolsAutoDepsDisable`, `IoCToolsAutoDepsExcludeGlob`, `IoCToolsAutoDepsReport`, `IoCToolsAutoDetectLogger`, `IoCToolsInjectDeprecationSeverity`) with severity-option notes consistent with existing entries. Add the assembly-attribute configuration pattern as a new primary subsection, covering both `Scope.Assembly` (default) and `Scope.Transitive`. Note the relationship to the existing `GeneratorOptions` const-class convention (which is unchanged). Call out that transitive propagation is declared in code via `Scope.Transitive`, not via MSBuild — MSBuild stays override-only.
- **`docs/platform-constraints.md`** — note the netstandard2.0 constraint on `IAutoDepsProfile` (it lives in `IoCTools.Abstractions` and must not require newer targets).
- **`docs/cli-reference.md`** — substantial update. Document the two new subcommands (`profiles` plural, `migrate-inject`), the `--hide-auto-deps` and `--only-auto-deps` flags available on `graph`/`why`/`explain`/`evidence`, the enhanced output formats (source markers in `graph`, source blocks in `why`, auto-dep sections in `explain` and `evidence`, preflight checks in `doctor`), and a callout noting the intentional distinction between singular `profile` (runtime profiling, existing) and plural `profiles` (auto-deps introspection, new).
- **`docs/testing.md`** — if any `[Inject]` appears in testing examples, migrate them and note the new patterns.
- **`README.md`** — update the headline example and feature list. Surface auto-deps as a first-class selling point.
- **`CLAUDE.md`** — update the "Key Patterns" bullet list: `[DependsOn<T>]` is the canonical dependency declaration; `[Inject]` is deprecated; universal `AutoDep<T>` and profile-based auto-deps join the pattern roster. Remove or rephrase any CLAUDE-level guidance that implies `[Inject]` is current.

Each document change ships in the same release as the code change so that docs never lag. The migration guide doc is the single canonical reference that IOC095's `HelpLinkUri` points to.

## Naming Rationale

**Closed vs. open — `AutoDep<T>` vs. `AutoDepOpen(typeof(T<>))`.** One uses a generic argument, the other uses a `typeof()` expression. The asymmetry is deliberate. A single unified `AutoDep` name cannot carry both forms because C# forbids unbound generics as type arguments to generic attributes, so any uniform-named pair would still require two distinct call shapes. Given that, naming them distinctly (`AutoDep` for the normal case, `AutoDepOpen` for the structural exception) makes the one `typeof()` in the surface obvious and locally scoped. Users who never need open-generic auto-deps never encounter `AutoDepOpen` and never type `typeof()`.

**Singular vs. plural — `AutoDep` vs. `AutoDeps`.** The singular/plural split follows a consistent rule: **singular names target one auto-dep type; plural names target one or more profiles**. So `AutoDep<T>` declares a single dep for all services, `NoAutoDep<T>` suppresses one specific dep, but `AutoDeps<TProfile>` attaches a profile (which bundles multiple deps), `NoAutoDeps` suppresses the whole auto-dep set, and `AutoDepsApply<...>` / `AutoDepsApplyGlob<...>` attach profiles. Once stated, the rule is memorable; the convention is documented in `docs/auto-deps.md` so users aren't expected to derive it themselves.

**Preposition choice — `AutoDepIn<TProfile, T>` for profile contributions.** "In" reads as "this dep goes in this profile," framing the attribute as *placing* the dep within the profile's definition (comparable to how `[assembly: InternalsVisibleTo("Other")]` reads as "internals visible *to* Other"). Alternatives considered: `AutoDepFor<TProfile, T>` (reads as "an auto-dep *for use by* this profile," which shifts the mental model to consumption), `AutoDepsAdd<TProfile, T>` (imperative — reads as a method call more than a declaration). `In` won because profile *definition* is the mental model the rest of the surface reinforces: profiles are objects that have deps, and `AutoDepIn` populates them. Reasonable people could disagree; documenting the choice here lets future contributors understand the intent rather than rename for style later.

## Out of Scope / Future

- `AutoDepInOpen<TProfile>(typeof(T<>))` — profile-scoped open generics. Defer to 1.7 pending user demand. Adding it later is purely additive — no 1.6 API shape precludes it — so the deferral creates no forward-compat risk.
- Shipped meta-package `IoCTools.AspNetCore` with pre-built `ControllerDefaults`, `MinimalApiDefaults` profiles that consumers can reference directly. Worth considering as a follow-up in 1.7; not in 1.6 scope.
- Primary-constructor-style injection. Blocked on C# language features (partial constructors) that have not fully landed.
- Replacing the `GeneratorOptions` const-class convention. It remains supported for existing knobs; this design does not extend it or retire it.

## Testing Surface

The feature requires test coverage across several layers. The exhaustive list is the planning concern, but the spec names the categories to make the scope explicit:

- Universal auto-dep emission: closed types, open generics, combined with `[DependsOn<T>]`.
- Built-in ILogger auto-detection: present-in-compilation triggers detection; absent does not; `IoCToolsAutoDetectLogger=false` disables; user-defined `ILogger<T>` types in other namespaces do not trigger false positives; `Serilog.ILogger` does not trigger.
- Profile resolution: base-class match, interface match, glob match, explicit attach, multi-profile union.
- Opt-out: `[NoAutoDeps]`, `[NoAutoDep<T>]` for closed and open-generic auto-deps, `[NoAutoDepOpen(typeof(T<>))]` for open-generic-shape suppression (stable across class renames, distinct from closed-form `NoAutoDep<T>`).
- Transitive scope (`AutoDepScope.Transitive`): library declares transitive auto-deps, consumer receives them; consumer `[NoAutoDep<T>]`/`[NoAutoDepOpen]`/`[NoAutoDeps]` override transitive-inherited; multi-library union of overlapping transitive sets; `AutoDepsApply<TProfile, TBase>(Scope = Transitive)` requires structural match on the consumer's service; shared-project solution-wide pattern end-to-end.
- Cross-assembly stale-cache invariance: changes to a referenced assembly's transitive attributes invalidate the consumer's resolver cache; unrelated changes in references do not.
- Symbol-equality hygiene: resolver uses `SymbolEqualityComparer.Default` for all type comparisons, including across assembly boundaries; no false negatives from reference-identity comparisons.
- Regression coverage against existing IOC001-IOC094: all 1650+ existing generator tests pass unchanged after 1.6 changes land. Where pre-existing test fixtures would otherwise receive unwanted auto-deps (e.g., fixtures that assert a specific constructor signature), they are updated to include `[NoAutoDeps]` rather than having the test expectations shifted — the principle is that the existing behavior is preserved as a regression baseline, not that every test is rewritten to match new output.
- Error recovery: resolver behavior under malformed source (syntax errors in attributes, invalid typeof expressions), partial compilations (missing references that aren't `IoCTools.Abstractions`), and broken transitive references (a referenced assembly that itself fails to load cleanly). In every case, the resolver degrades gracefully — emits a diagnostic where actionable, returns an empty or best-effort set otherwise, and never throws out of the generator.
- Inheritance: `PremiumOrderService : OrderService` under open-generic auto-dep closes per-level to distinct `ILogger<T>` instances both injected via `base()` chaining; `[NoAutoDepOpen(typeof(ILogger<>))]` on derived class suppresses the derived-level closure only, leaving base's logger forwarded through.
- Kill-switch CLI end-to-end: `migrate-inject` under `IoCToolsAutoDepsDisable=true` emits every converted field rather than deleting any; `migrate-inject` against a 1.5.x-referencing project emits the "Delete entirely disabled" notice and proceeds with conversions only.
- Resolution precedence: `[DependsOn<T>]` + auto-dep overlap, manual constructor skip, cross-assembly non-leakage.
- Diagnostics: one test per diagnostic code (IOC095-IOC105), including the positive cases.
- Code fix: covered-by-auto-dep deletion, default-name conversion, custom-name preservation with `memberName1..N`, multi-field coalescing.
- Parity: behavioral-equivalence tests that run the same service through both pre- and post-migration attribute forms and verify identical generated output.
- MSBuild overrides: `IoCToolsAutoDepsDisable` as full no-op, `IoCToolsAutoDepsExcludeGlob` suppression, `IoCToolsAutoDepsReport` comment emission, `IoCToolsInjectDeprecationSeverity` modulation of IOC095.
- Generic-service handling: `Repository<TEntity>` with open-generic auto-dep produces a valid constructor whose open-generic logger parameter is successfully resolved by the default `Microsoft.Extensions.DependencyInjection` container via its standard open-generic `ILogger<>` registration. This is an integration-style smoke test, not an assumption — it is verified against an actual running container, not just checked in generated source.
- Incremental-generator caching: `AutoDepsResolver` output is an equatable value type; cache invalidation happens only when a relevant assembly attribute, profile type, or service class actually changes. Keystroke-level edits in unrelated files do not bust the cache.
- Multi-partial-class services: attributes on any partial (e.g., `[NoAutoDep<T>]` on partial A while `[Scoped]` is on partial B) are unioned into one resolution input set.
- CLI integrations: golden-output tests for `graph` with source markers, `why` with source attribution blocks, `explain` with auto-dep narrative sections, `evidence` with auto-dep inclusion, `doctor` with the three new preflight checks, `profiles` (plural) subcommand at each invocation form, and `migrate-inject` including `--dry-run` mode.
- CLI `--hide-auto-deps` / `--only-auto-deps` behavior: golden-output tests on `graph`, `why`, `explain`, `evidence` for both the filtered and the inverse views.
- CLI naming distinction: a regression test that ensures `ioc-tools profile` (singular, runtime profiling — existing) and `ioc-tools profiles` (plural, auto-deps introspection — new) dispatch to different runners and produce distinct output.

## Approval

The spec has been through eight independent review rounds and converged. The next step, on user approval, is to invoke the `writing-plans` skill to produce an implementation plan that sequences:

- attribute additions (`AutoDep<T>`, `AutoDepOpen`, `AutoDepIn<TProfile, T>`, `AutoDepsApply<TProfile, TBase>`, `AutoDepsApplyGlob<TProfile>`, `AutoDeps<TProfile>`, `NoAutoDeps`, `NoAutoDep<T>`, `NoAutoDepOpen`, `IAutoDepsProfile` marker, `AutoDepScope` enum)
- the `IoCTools.Generator.Shared.AutoDepsResolver` shared library (with its value-typed attribution output and MVID-keyed caching) and the sibling `IoCTools.Generator.Shared.InjectMigrationRewriter`
- codegen integration (attribution threading through `DependencySource` or a parallel `AutoDepAttribution` record, merge into `ConstructorEmitter`'s `[DependsOn<T>]` input, inheritance and `base()` chaining semantics)
- built-in `ILogger<T>` auto-detection
- cross-assembly transitive-scope reading
- code fix provider (powered by the shared rewriter) with the three migration branches and `[ExternalService]` mapping
- diagnostics IOC095-IOC105 with `HelpLinkUri` scheme
- `[Inject]` deprecation timeline through `IoCToolsInjectDeprecationSeverity`
- Sample and first-party package (`IoCTools.Testing`, `IoCTools.FluentValidation`, `IoCTools.Tools.Cli`) migration
- CLI changes: enhanced `graph`/`why`/`explain`/`evidence`/`doctor`, new `profiles` and `migrate-inject` subcommands, the cross-command `--hide-auto-deps` / `--only-auto-deps` flags (with `CommonAutoDepsOptions` plumbing across four parsers), `suppress` awareness of the new diagnostic codes, CLI attribution tags including `auto-transitive:<AssemblyName>` and `auto-builtin:ILogger`
- documentation overhaul: new `docs/auto-deps.md`, updates across `attributes.md`, `getting-started.md`, `migration.md`, `diagnostics.md`, `configuration.md`, `platform-constraints.md`, `cli-reference.md`, `testing.md`, `README.md`, `CLAUDE.md`
- test matrix including existing-behavior regression baseline, error-recovery, inheritance edge cases, kill-switch end-to-end, and cross-version tolerance

…into an executable phase structure.
