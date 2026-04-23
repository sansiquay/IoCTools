# Auto-Deps

Auto-deps let you declare dependencies once — at assembly scope, in a profile,
or via a library's transitive policy — and have the generator inject them into
every matching service without repeating `[Inject]` or `[DependsOn<T>]` per
class.

Auto-deps were introduced in **IoCTools 1.6.0** and are the canonical
replacement for the now-deprecated `[Inject]` attribute. For the migration
path off `[Inject]`, see [docs/migration.md](migration.md#migrating-from-15x-to-16x).

---

## Overview

### When to use auto-deps

Auto-deps are the right tool when a dependency is genuinely *ambient*:

- **Cross-cutting infrastructure** — `ILogger<T>`, `TimeProvider`, `IMetrics`,
  `ITracer`. Every service plausibly needs it; every service repeats the
  declaration.
- **Per-role defaults** — every controller wants `IMediator` and `IMapper`;
  every background service wants `IServiceScopeFactory`. A profile attaches
  those deps once to a role and every matching service receives them.
- **Library-shipped defaults** — a platform package ships opinionated defaults
  to every consuming application without every consumer re-declaring them.

### When *not* to use auto-deps

Do not declare a dependency as an auto-dep when it is specific to a handful of
services or when its absence would be a design smell rather than plumbing
noise. Business-logic deps (`IPaymentService`, `IReportRepository`) stay
explicit via `[DependsOn<T>]` so the service's collaborators remain visible at
a glance.

If you cannot explain why the dep is "ambient," keep it explicit.

---

## The `[Inject]` field is deprecated

Before diving into auto-deps, one top-level fact: the old `[Inject] private
readonly IFoo _foo;` field style is deprecated in 1.6 and removed in 2.0.
Every surface described below assumes `[DependsOn<T>]` is the source-of-truth
way to express an explicit dep, and auto-deps are the way to express an
ambient one.

The `IOC095` diagnostic surfaces the deprecation at build time and ships with
a Roslyn code fix plus an `ioc-tools migrate-inject` CLI command. See
[docs/migration.md](migration.md#migrating-from-15x-to-16x).

---

## Universal auto-deps

Declare an auto-dep at assembly scope and every service in the assembly
receives it:

```csharp
[assembly: AutoDep<TimeProvider>]
[assembly: AutoDep<IMetrics>]
[assembly: AutoDepOpen(typeof(ILogger<>))]
```

- `AutoDep<T>` — closed type. Every service gets a `T` parameter in its
  generated constructor.
- `AutoDepOpen(typeof(T<>))` — single-arity open generic. The generator closes
  the unbound type with the concrete service type at codegen time. Applied to
  `OrderService`, `AutoDepOpen(typeof(ILogger<>))` yields a parameter typed
  `ILogger<OrderService>`.

**Why `AutoDepOpen` exists as a separate attribute.** C# does not permit
unbound generics as type arguments to generic attributes, so
`AutoDep<ILogger<>>` is a language-level impossibility. `AutoDepOpen` is
named distinctly so the `typeof()` exception is obvious and narrowly scoped —
it is the only place in the auto-deps surface where `typeof()` appears.

Multi-arity unbound generics (`typeof(IFoo<,>)`) are rejected at build time
with `IOC106` because no universal "close with self" convention exists for
them.

---

## Built-in `ILogger<T>` auto-detection

`Microsoft.Extensions.Logging.ILogger<T>` is universal enough in modern .NET
that requiring every project to declare
`[assembly: AutoDepOpen(typeof(ILogger<>))]` would be a config tax that
defeats the feature's premise.

Instead, the generator detects the MEL `ILogger<T>` open generic in the
compilation's references and treats it as if a universal
`AutoDepOpen(typeof(ILogger<>))` had been declared — zero configuration
required.

**Detection rules**

- The match is exact and fully qualified:
  `Microsoft.Extensions.Logging.ILogger\`1` via
  `Compilation.GetTypeByMetadataName`. User-defined `ILogger<T>` types do not
  trigger detection. `Serilog.ILogger` (non-generic) does not trigger
  detection.
- If the MEL logger type is not discoverable in any referenced assembly, the
  generator behaves exactly as if this section did not exist. Projects that do
  not use logging are unaffected.

**Opt-outs, from narrow to broad**

- Per-service, shape-based:
  `[NoAutoDepOpen(typeof(ILogger<>))]` on the service. Recommended — it does
  not repeat the service's own type name, so class renames do not silently
  break the opt-out.
- Per-service, closed form: `[NoAutoDep<ILogger<MyService>>]`. Works, but
  requires repeating the service's type and silently breaks on rename.
- Per-service, nuclear: `[NoAutoDeps]` — disables *every* auto-dep for this
  service.
- Project-wide: MSBuild `<IoCToolsAutoDetectLogger>false</IoCToolsAutoDetectLogger>`
  — disables built-in detection but leaves universal `AutoDep<T>` /
  `AutoDepOpen` declarations intact.
- Project-wide, nuclear: MSBuild
  `<IoCToolsAutoDepsDisable>true</IoCToolsAutoDepsDisable>` — disables the
  entire feature.

In CLI output and the debug report, the detected logger appears as
`auto-builtin:ILogger` — distinct from `auto-universal` (declared assembly
attribute) so you can tell which is which.

Other candidates for auto-detection (`TimeProvider`, `IMemoryCache`,
`IServiceProvider`) are **not** built in — only `ILogger<T>` is near-universal
enough in the .NET ecosystem to earn zero-config treatment. Declare them
explicitly via `[assembly: AutoDep<T>]` if you want them.

---

## Profiles

A profile is a marker class implementing the empty `IAutoDepsProfile` marker
interface. Deps are added to the profile via assembly attributes:

```csharp
public sealed class ControllerDefaults : IAutoDepsProfile { }
public sealed class BackgroundDefaults : IAutoDepsProfile { }

[assembly: AutoDepIn<ControllerDefaults, IMediator>]
[assembly: AutoDepIn<ControllerDefaults, IMapper>]
[assembly: AutoDepIn<BackgroundDefaults, IServiceScopeFactory>]
```

The marker interface does three things:

1. Makes profile types discoverable by the generator.
2. Enables IntelliSense filtering on `TProfile` type arguments.
3. Gates diagnostic `IOC097` when a type used as `TProfile` forgets the
   marker.

Profile types must be **non-generic** in 1.6 — generic profile classes fire
`IOC104`. This keeps closure semantics tractable; generic profiles may arrive
in a later release.

### Profile attachment

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

`AutoDepsApply<TProfile, TBase>` matches both base-class inheritance and
interface implementation. The generator determines which at match time.

A single service may match multiple profiles. All matched profiles' deps are
unioned, deduplicated, and merged with the universal set.

---

## Cross-assembly transitivity

By default, every assembly-level auto-dep attribute applies only to services
declared in the declaring assembly. Two real use cases need cross-boundary
propagation — a library publishing opinionated defaults, and a shared project
carrying solution-wide policy — and both are supported via
`AutoDepScope.Transitive`:

```csharp
namespace IoCTools.Abstractions.Annotations;

public enum AutoDepScope
{
    Assembly,     // default — applies only to services in the declaring assembly
    Transitive    // also applies to services in consuming assemblies
}
```

`Scope` is a named property on `AutoDep<T>`, `AutoDepOpen`,
`AutoDepIn<TProfile, T>`, `AutoDepsApply<TProfile, TBase>`, and
`AutoDepsApplyGlob<TProfile>`:

```csharp
// Library "Acme.Platform" declares transitive intent
[assembly: AutoDep<ITracer>(Scope = AutoDepScope.Transitive)]
[assembly: AutoDepOpen(typeof(ILogger<>), Scope = AutoDepScope.Transitive)]
[assembly: AutoDepIn<ControllerDefaults, IMediator>(Scope = AutoDepScope.Transitive)]
[assembly: AutoDepsApply<ControllerDefaults, ControllerBase>(Scope = AutoDepScope.Transitive)]

// Consumer "Acme.WebApp" automatically inherits the above for its own services
// by virtue of referencing Acme.Platform. No additional declaration required.
```

### Conflict rules for transitive auto-deps

- **Consumer opt-outs always win.** `[NoAutoDep<T>]`, `[NoAutoDepOpen(...)]`,
  or `[NoAutoDeps]` on a service in the consuming assembly suppress any
  transitive auto-dep for that type/service, regardless of how many upstream
  assemblies declared it.
- **Multi-library union.** When multiple referenced assemblies each declare
  transitive auto-deps, their sets are silently unioned and deduplicated. No
  diagnostic fires on overlap.
- **Structural match still required.** `AutoDepsApply<TProfile, TBase>(Scope =
  Transitive)` propagates the *rule*, but the match against `TBase` is
  evaluated in the consuming compilation against services in that consumer.
  A service in B only receives the profile if it structurally inherits or
  implements `TBase`.
- **Glob match evaluates in the consumer.** `AutoDepsApplyGlob<TProfile>`
  with `Scope = Transitive` evaluates its glob against namespaces in the
  *consuming* assembly, not the declaring one. Library authors should prefer
  broad, convention-based patterns (e.g. `"*.Controllers.*"`) over
  assembly-specific ones.
- **Diagnostics fire across the boundary.** `IOC097`, `IOC099`, `IOC108`, and
  `IOC104` still surface in the consumer's compilation when a transitive
  attribute is invalid.

### Solution-wide policy via a shared project

For a team that wants one declaration across every project in a solution, the
idiomatic pattern is a tiny `MyCompany.DiConfig` project containing only
assembly attributes with `Scope.Transitive`, with every other project taking
a `<ProjectReference>` to it:

```
MyCompany.DiConfig/        // <-- only this contains the attributes
  DiConfig.csproj          //     references IoCTools.Abstractions
  AssemblyInfo.cs          //     [assembly: AutoDepOpen(typeof(ILogger<>), Scope = Transitive)]
                           //     [assembly: AutoDep<ITracer>(Scope = Transitive)]

MyCompany.WebApp/
  WebApp.csproj            // <ProjectReference Include="../MyCompany.DiConfig/..." />

MyCompany.Workers/
  Workers.csproj           // <ProjectReference Include="../MyCompany.DiConfig/..." />
```

This is the same pattern teams already use for `[assembly: InternalsVisibleTo]`
and shared analyzer packs. No new mechanism required — transitivity over a
common dependency is the answer. **MSBuild is not used for declaring
auto-deps**; the principle that MSBuild stays override-only is preserved.

### Cross-version compatibility

A 1.6 generator in consumer B may encounter referenced assemblies on older
`IoCTools.Abstractions` versions (1.5.x or earlier) that do not define
`AutoDepScope`, `Scope = Transitive`, or `NoAutoDepOpen`. Those references
simply contribute nothing to the transitive set and do not error. Symmetrically,
a 1.5.x generator cannot interpret a 1.6 library's transitive attributes; it
behaves as if they were not there. The two directions are compatible by
omission, not by explicit handshake.

---

## The opt-out ladder

```csharp
[NoAutoDeps]                             // disable all auto-deps for this service
[NoAutoDep<TimeProvider>]                // disable one specific closed-type auto-dep
[NoAutoDepOpen(typeof(ILogger<>))]       // disable any auto-dep derived from an open-generic shape
[Scoped] public partial class LegacyService { }
```

`NoAutoDepOpen(typeof(T<>))` is the twin of `AutoDepOpen` — it suppresses any
auto-dep derived from a matching open-generic shape, **regardless of source**.
That includes the built-in `ILogger<T>` detection, a local universal
`AutoDepOpen`, and any transitive `AutoDepOpen` from a referenced library.

For stale-opt-out detection, `IOC096` fires when `[NoAutoDep<T>]` or
`[NoAutoDepOpen(typeof(T<>))]` references a type or shape not actually in the
service's resolved auto-dep set (typo, or the opt-out outlived the declaration
it was guarding against).

---

## Resolution order

For any given service, the resolved auto-dep set is computed as follows:

1. **Start with the universal set.**
   - Built-in: if `Microsoft.Extensions.Logging.ILogger\`1` is discoverable in
     the compilation and `IoCToolsAutoDetectLogger` is not `false`, it is
     treated as an implicit `AutoDepOpen(typeof(ILogger<>))`.
   - Local: all `[assembly: AutoDep<T>]` and
     `[assembly: AutoDepOpen(typeof(T<>))]` declared in the service's own
     assembly.
   - Transitive: the same attributes declared with `Scope = Transitive` in any
     referenced assembly that transitively references `IoCTools.Abstractions`.
   - Open generics are closed to the service's concrete type at this step.
2. **Add profile contributions** for every profile attached to the service
   (via `AutoDepsApply`, `AutoDepsApplyGlob`, or `[AutoDeps<TProfile>]`).
   Profile contributions honor `Scope.Transitive` on their declarations.
3. **Subtract opt-outs** on the service: `[NoAutoDep<T>]` removes by closed
   type; `[NoAutoDepOpen(typeof(T<>))]` removes by open-generic shape.
4. **If `[NoAutoDeps]` is present**, the resolved set is emptied entirely.
5. **Reconcile against explicit `[DependsOn<T>]`** on the service — explicit
   always wins. Reconciliation is per type-argument slot:
   - Bare slot with no customization (no `memberNameN`, no
     attribute-wide `external: true`) is redundant with the auto-dep → emit
     `IOC098` info, generated constructor is identical either way.
   - Customized slot (per-slot `memberNameN`, or attribute-wide `external:
     true` which marks every slot in that attribute as customized) is a
     deliberate override → the auto-dep for that type is suppressed and the
     `DependsOn` slot is emitted as-is.
6. **Manual constructors skip the feature.** If the service has a user-authored
   constructor, the entire auto-dep set is skipped (same behavior the
   generator has always had for manual constructors).
7. **Partial classes union.** When a service is declared across multiple
   `partial` class files, every attribute on every partial participates in
   resolution — opt-outs, profile attachments, and `[DependsOn<T>]` alike.

The resolved set is merged into the same `[DependsOn<T>]` list
`ConstructorEmitter` already consumes, so auto-deps participate in all
existing diagnostics (`IOC001` no-implementation, `IOC003` lifetime
validation, etc.) without any new plumbing.

---

## Inheritance and `base()` chaining

When a service has a base class that is also an IoCTools service, the
generator chains `base(...)` with the base's dependencies. Auto-deps interact
with this one level at a time: each level of the hierarchy closes its
open-generic auto-deps against its own concrete type.

For `PremiumOrderService : OrderService` under
`AutoDepOpen(typeof(ILogger<>))`:

- `OrderService`'s generated constructor takes `ILogger<OrderService>` and
  stores it as `_logger`.
- `PremiumOrderService`'s generated constructor takes its own
  `ILogger<PremiumOrderService>` **and also** an `ILogger<OrderService>`
  parameter that is forwarded via `base(..., orderServiceLogger)` to satisfy
  the base ctor.

The derived-class constructor therefore has both its own logger and its
base's logger; the DI container injects two distinct `ILogger<T>` instances
because MS.DI closes the open-generic registration per resolution.

If you want only one logger, suppress the derived-level auto-dep via
`[NoAutoDepOpen(typeof(ILogger<>))]` on the derived class — which suppresses
the derived-level closure, not the base's. The derived class then receives
only `ILogger<OrderService>` forwarded from its base.

The "final concrete service type" is the concrete class symbol the generator
is producing a constructor for — never an interface from `[RegisterAs<T>]`.
A service with `[RegisterAs<IFoo>]` closed under
`AutoDepOpen(typeof(ILogger<>))` still resolves to
`ILogger<TheConcreteClass>`, not `ILogger<IFoo>`.

---

## Recipes

### Greenfield: idiomatic 1.6 setup

```csharp
// AssemblyInfo.cs or any file with assembly attributes
[assembly: AutoDep<TimeProvider>]
// ILogger<T> is auto-detected — no declaration needed

// DiProfiles/ControllerDefaults.cs
public sealed class ControllerDefaults : IAutoDepsProfile { }

[assembly: AutoDepIn<ControllerDefaults, IMediator>]
[assembly: AutoDepIn<ControllerDefaults, IMapper>]
[assembly: AutoDepsApply<ControllerDefaults, ControllerBase>]

// Controllers/OrderController.cs
[Scoped]
public partial class OrderController : ControllerBase
{
    [DependsOn<IPaymentService>]   // business-logic dep stays explicit
    // _timeProvider, _logger, _mediator, _mapper all auto-injected
}
```

### Migrating a large legacy codebase

1. Upgrade IoCTools packages to 1.6.0.
2. (Optional) Set
   `<IoCToolsInjectDeprecationSeverity>Info</IoCToolsInjectDeprecationSeverity>`
   to silence `IOC095` warnings during a short triage window.
3. Run `ioc-tools migrate-inject` at the solution root (add `--dry-run` first
   to preview diffs). The headless migration converts every `[Inject]` field
   to the equivalent `[DependsOn<T>]` surface, deleting fields whose type is
   already covered by an auto-dep in the target project.
4. Commit the mechanical conversion as one diff.
5. Remove the severity override from the csproj.
6. Iterate on auto-deps: promote cross-cutting repeats (`ILogger<T>` is
   already auto-detected; see also `TimeProvider`, `IMetrics`, `ITracer`) to
   `[assembly: AutoDep<T>]` declarations.

### Multi-team library ecosystem

A platform package ships opinionated defaults to every consuming app:

```csharp
// Acme.Platform assembly attributes
[assembly: AutoDepOpen(typeof(ILogger<>), Scope = AutoDepScope.Transitive)]
[assembly: AutoDep<ITracer>(Scope = AutoDepScope.Transitive)]
[assembly: AutoDepIn<ControllerDefaults, IMediator>(Scope = AutoDepScope.Transitive)]
[assembly: AutoDepsApply<ControllerDefaults, ControllerBase>(Scope = AutoDepScope.Transitive)]
```

Every consumer that references `Acme.Platform` inherits the policy without
re-declaring. A consumer that wants to opt out of a single transitive policy
uses the standard opt-out ladder on the affected service.

Library authors writing `Scope.Transitive` patterns should prefer **broad,
convention-based globs** over assembly-specific ones, because the glob
evaluates against consumer namespaces at generator-time.

---

## Diagnostics reference

Every auto-deps diagnostic links back to this document via `HelpLinkUri`.

<a id="ioc095"></a>
### IOC095 — `[Inject]` is deprecated

**Severity:** Warning (1.6) → Error (1.7) → Removed (2.0)

The field uses `[Inject]`, which is deprecated in favor of `[DependsOn<T>]`.
A Roslyn code fix and the `ioc-tools migrate-inject` CLI convert in bulk.
Severity can be modulated during migration via
`<IoCToolsInjectDeprecationSeverity>` (values: `Error`, `Warning`, `Info`,
`Hidden`). See [migration guide](migration.md#migrating-from-15x-to-16x).

<a id="ioc096"></a>
### IOC096 — Stale opt-out

**Severity:** Info

`[NoAutoDep<T>]` references a type that is not in the service's resolved
auto-dep set, or `[NoAutoDepOpen(typeof(T<>))]` references an open-generic
shape with no matching auto-dep derivation for this service. Usually a typo
or an opt-out that outlived the declaration it was guarding against.

<a id="ioc097"></a>
### IOC097 — Profile type missing `IAutoDepsProfile` marker

**Severity:** Warning

`AutoDepIn<TProfile, T>`, `AutoDepsApply<TProfile, TBase>`,
`AutoDepsApplyGlob<TProfile>`, or `[AutoDeps<TProfile>]` was given a
`TProfile` that does not implement `IAutoDepsProfile`. Add the marker to make
the profile type discoverable:

```csharp
public sealed class ControllerDefaults : IAutoDepsProfile { }
```

<a id="ioc098"></a>
### IOC098 — `[DependsOn<T>]` redundant with auto-dep

**Severity:** Info

The service's `[DependsOn<T>]` covers a type also supplied by an auto-dep
(built-in detection, local universal, transitive, or profile-sourced). The
generated constructor is identical either way; the message names the auto-dep
source (e.g. `auto-builtin:ILogger`, `auto-universal`,
`auto-transitive:<AssemblyName>`, `auto-profile:<Name>`) so you can pick the
right remediation.

This diagnostic does not fire when the auto-dep source is inactive — e.g.
`IoCToolsAutoDetectLogger=false` disables detection, so
`[DependsOn<ILogger<MyService>>]` is not redundant and `IOC098` is silent.

<a id="ioc099"></a>
### IOC099 — Profile attachment matches zero services

**Severity:** Info

`AutoDepsApply<TProfile, TBase>` or `AutoDepsApplyGlob<TProfile>` matches no
services in the assembly. Often a stale rule, a typo in the glob pattern, or
a base class that moved.

<a id="ioc106"></a>
### IOC106 — `AutoDepOpen` on multi-arity generic

**Severity:** Error

`AutoDepOpen` was given a multi-arity unbound generic like
`typeof(IFoo<,>)`. No universal "close with self" convention exists for
multi-arity generics — domain multi-arity generics
(`IRequestHandler<TRequest, TResponse>`, `IValidator<TSource, TDest>`) use
their type parameters for domain entities, not for the service type.

<a id="ioc107"></a>
### IOC107 — `AutoDepOpen` on non-generic

**Severity:** Error

`AutoDepOpen` was given a non-generic type. Use `AutoDep<T>` for closed
types.

<a id="ioc108"></a>
### IOC108 — `AutoDepOpen` closure violates constraints

**Severity:** Error

Closing the unbound generic over a matching service's concrete type would
violate the unbound's type-parameter constraints. The primary location is the
service declaration (where codegen would fail); secondary is the
`AutoDepOpen` assembly attribute.

<a id="ioc103"></a>
### IOC103 — Invalid glob pattern

**Severity:** Error

`AutoDepsApplyGlob<TProfile>` was given an invalid glob pattern. The
validator uses the same grammar as existing `IoCToolsIgnoredTypePatterns` /
`IoCToolsSkipAssignableTypes*` — empty strings, unterminated character
classes, and unsupported metacharacters all fire this.

<a id="ioc104"></a>
### IOC104 — Profile type is generic

**Severity:** Error

A profile type used in `AutoDepIn`, `AutoDepsApply`, `AutoDepsApplyGlob`, or
`AutoDeps` is generic. Profiles must be non-generic in 1.6.

<a id="ioc105"></a>
### IOC105 — Redundant profile attachment

**Severity:** Info

A service is attached to the same profile via more than one path (e.g.
`AutoDepsApplyGlob` match plus explicit `[AutoDeps<TProfile>]` on the class).
The attachment is deduped silently; the diagnostic surfaces the redundancy
so you can simplify.

---

## MSBuild overrides

MSBuild properties are override-only. They never declare auto-deps; they
modulate feature behavior at build time (CI gates, per-environment debugging,
escape hatches). Declarations live in assembly attributes.

| Property | Purpose |
|---|---|
| `IoCToolsAutoDepsDisable` | Boolean kill switch. When `true`, the entire auto-deps feature is a no-op. |
| `IoCToolsAutoDepsExcludeGlob` | Namespace glob (e.g. `*.Legacy.*`). Services in matching namespaces are treated as if they had `[NoAutoDeps]`. |
| `IoCToolsAutoDepsReport` | Boolean. When `true`, every generated constructor file includes a leading comment block listing resolved auto-deps and sources. |
| `IoCToolsAutoDetectLogger` | Boolean, default `true`. When `false`, disables built-in `ILogger<T>` auto-detection. Does not affect universal `AutoDep<T>` / `AutoDepOpen` declarations. |
| `IoCToolsInjectDeprecationSeverity` | Modulates `IOC095` severity (`Error`, `Warning`, `Info`, `Hidden`) within its allowed band. 1.6 permits lowering; 1.7 is raise-only. |

See [docs/configuration.md](configuration.md) for the full MSBuild property
reference.

---

## CLI integration

Every `ioc-tools` inspection subcommand surfaces auto-dep attribution. See
[docs/cli-reference.md](cli-reference.md) for full command docs. A quick
orientation:

- `ioc-tools graph <type>` — dep tree with source markers (`ℹ` universal,
  `▣` profile) and a legend. JSON output adds a `source` field per node.
- `ioc-tools why <type> <dep>` — per-dep attribution block with the
  contributing attribute's file/line and a `suppress here:` hint.
- `ioc-tools explain <type>` — prose narrative that includes auto-deps.
- `ioc-tools evidence` — registration-evidence artifact with auto-deps
  listed alongside explicit declarations, tagged by source.
- `ioc-tools doctor` — preflight: unregistered auto-dep types, stale profile
  attachments, dead profiles.
- `ioc-tools profiles` — new in 1.6. Lists profiles, their deps, and
  (optionally) their matches. Distinct from the existing singular `profile`
  subcommand, which does project-load benchmarking.
- `ioc-tools migrate-inject` — new in 1.6. Headless bulk `[Inject]` →
  `[DependsOn<T>]` migration. Same transform the IDE code fix uses.

The `--hide-auto-deps` and `--only-auto-deps` flags are available on `graph`,
`why`, `explain`, and `evidence`. Default is show-everything; hiding is an
explicit opt-in.

---

## Debug report

When `<IoCToolsAutoDepsReport>true</IoCToolsAutoDepsReport>`, every generated
constructor file gains a leading comment block:

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

The report is opt-in to avoid noise in normal builds.
