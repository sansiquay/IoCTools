# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [1.10.0] - 2026-05-25

### Added (additive, object-shaped `--json` outputs)
- **Receipt headers (`schema_version`, `generated_at`) on all `--json` outputs.** Every `--json` payload is now wrapped in an agent-receipt envelope with a `schema_version` string (starts at `"1.0"`) and an ISO8601 UTC `generated_at` timestamp (`yyyy-MM-ddTHH:mm:ssZ`), emitted as the first two top-level fields in that order. For surfaces whose payload was already a JSON object (e.g. `evidence --json`, `suppress --json`, `validator-graph --why --json`, `doctor --json`, `explain --json`, `profile --json`, `profiles --json`, `config-audit --json`, `why --json`, `test scaffold --json`, `graph --json` / `graph --format json`), the two headers are merged in at the top of the existing object. Consumers that ignore unknown top-level keys: zero impact. The envelope is centralized in `OutputContext.WriteJson` / `OutputContext.SerializeWithReceiptHeaders`, so future `--json` surfaces inherit the headers for free. `schema_version` is the envelope contract version; bump only when envelope shape (not payload shape) changes. Format hygiene: `generated_at` is now formatted with `CultureInfo.InvariantCulture` so the timestamp shape is locked against culture-dependent locales.

### Changed — BREAKING for array-shaped `--json` outputs
- **Top-level JSON arrays on `services --json`, `validators --json`, and `validator-graph --json` are now wrapped under a `data` field** so the receipt envelope can stay a JSON object. Pre-change shape was `[ ... ]`; new shape is:

  ```json
  {
    "schema_version": "1.0",
    "generated_at": "2026-05-24T12:34:56Z",
    "data": [ ... ]
  }
  ```

  This is a breaking shape change for any consumer that was reading a top-level array from these three surfaces. Update consumers to read `.data[]` instead of `[]`. No other `--json` surface is affected by this wrap (all others were already object-shaped pre-change). Recommend a minor version bump.

### Internal
- **`ideas.md` SHIPPED entries removed from backlog.** PR #16 converted four shipped items into `— SHIPPED` markers, but codex backlog audits kept re-recommending them because they still lived in the implementation-backlog sections. Removed the IOC032 + `InstanceSharing.Shared` awareness section, the `HelpLinkUri`/category-grouping bullets under Diagnostic UX, and the Inheritance-Aware Service Intent (1.6.3-dev.2) section. Each removed claim was re-verified against HEAD (`RedundantConfigurationValidator.GetRegisterAsInstanceSharing == "Shared"` skip; 115 descriptors with `HelpLinkUri` via `AutoDepsHelpBase`/`MigrationHelpBase`; `IoCTools.*` namespaced categories; `ServiceDiscovery.InheritsFromIoCToolsManagedBase` wired into `ServiceClassPipeline`) before deletion. No code, public API, or generated output is affected.

## [1.9.1] - 2026-05-23

### Fixed
- Generated config binding now uses InvariantCulture for DateTime/DateTimeOffset/TimeSpan default-value parsing, preventing locale-dependent parse failures on non-en-US hosts ([#7], PR [#8]).

### Internal
- **`ideas.md` backlog pruned for shipped items.** Removed the IOC032 + `InstanceSharing.Shared` awareness section (already shipped in `RedundantConfigurationValidator.ValidateRegisterAsMatchesImplementedInterfaces` — `Shared` attributes are skipped, so `[RegisterAs<I1, I2>(InstanceSharing.Shared)]` no longer triggers a false-positive IOC032). Removed the "Add HelpLinkUri to all 87 diagnostic descriptors" bullet — all 115 current descriptors set `HelpLinkUri` either directly or via the `AutoDepsHelpBase` / `MigrationHelpBase` constants. Removed the "Use specific categories for IDE grouping" bullet — descriptors already use the target namespaced categories (`IoCTools.AutoDeps`, `.Configuration`, `.Dependency`, `.Lifetime`, `.Registration`, `.Structural`, `.Testing`, `.Usage`). Corrected the CS8603 sample-code count to match the current state (6 occurrences across four sample files, not 3 in `MultiInterfaceExamples.cs`) so the next person to pick up that bullet does not chase a stale repro. No code, public API, or generated output is affected.

### Packaging
- **`PackageReleaseNotes` refreshed on the five 1.9.0 coherence-rebuild packages.** `IoCTools.Abstractions`, `IoCTools.Generator`, `IoCTools.Tools.Cli`, `IoCTools.FluentValidation`, and `IoCTools.Generator.Analyzer` were all version-bumped to 1.9.0 alongside `IoCTools.Testing` / `IoCTools.Testing.Abstractions`, but their `PackageReleaseNotes` properties still advertised the v1.8.0 `[InjectConfiguration]` null-fallback change as the headline. NuGet feed consumers landing on the 1.9.0 detail pages for these packages now see an accurate "version-coherence rebuild — no functional changes in this package; 1.9.0 feature work landed in `IoCTools.Testing[.Abstractions]`" note plus the carried-forward 1.8.0 behavior change. `IoCTools.Testing` and `IoCTools.Testing.Abstractions` already had correct 1.9.0 notes and are unchanged. No code, public API, or generated output is affected.

### Documentation
- **README "What's New" section refreshed to v1.9.0.** Previously anchored at v1.7.3, two minor releases behind. The top-level README now leads with the 1.9.0 `Cover<T>(ConcreteHandling = ForceMock)` opt-out (including the virtual-public-methods constraint surfaced in the post-1.9.0 re-audit), the `TestFixturePipeline` namespace-exactness fix, the `Cover<T>.Logger` symbol-name parsing change, and the `FixtureEmitter.CurrentOptionsProfile` mutable-static removal; carries forward the 1.8.0 `[InjectConfiguration]` null-fallback behavior change; condenses 1.7.x detail to a "highlights" tail. CHANGELOG.md remains the canonical full history. Closes #10.
- **Surfaced fixture evidence quickstart in consumer-facing READMEs.** Both `IoCTools.Tools.Cli/README.md` and `IoCTools.Testing.Abstractions/README.md` now list the verified `ioc-tools evidence --project <tests.csproj> --test-fixtures --production-project <prod.csproj>` invocation alongside the existing `[Cover<T>]` quickstart, so Delta/Keel/Daedalus adopters discover the migration scanner without having to dig through `docs/testing.md`. `docs/testing.md` § CLI Fixture Evidence gains a before/after Moq-vs-`[Cover<T>]` snippet and a hard "do not replace compile-time fixture generation with runtime scanning/reflection" doctrine note — the reflection pathway defeats TDIAG diagnostics, IDE navigation, and the compile-time failure mode `[Cover<T>]` exists to eliminate; file an IoCTools issue rather than working around the generator. CLI signature verified against `dotnet ioc-tools` usage banner (`evidence --project <csproj> --test-fixtures [--production-project <csproj>] [--settings appsettings.json]`).
- **Clarified `ConcreteHandling.ForceMock` requires virtual public methods on the concrete target type.** The 1.9.0 release notes implied broader applicability than reality. Moq can only intercept `virtual`/`abstract` instance methods on classes, so `ForceMock` fixtures targeting a concrete with non-virtual public methods (the C# default — and the dominant Delta shape: `[Scoped] partial class` with sealed-by-default methods) compile cleanly, then `Setup(...)` silently no-ops and the real method body runs against default backing fields, typically throwing `NullReferenceException` at runtime. XML doc comments on `CoverAttribute<>.ConcreteHandling` and `ConcreteHandling.ForceMock` updated; `docs/testing.md` gained a "Concrete Handling Modes" section documenting the constraint and recommending interface extraction for sealed concretes. Empirical basis: workbench re-audit `docs/cover-migration-iocstools-1.9.0-reaudit-2026-05-17.md` (0 of 11 audited Delta candidates unblocked by `ForceMock` alone).

## [1.9.0] - 2026-05-17

### Added
- **`[Cover<T>(ConcreteHandling = ConcreteHandling.ForceMock)]` opt-out for the auto-concrete promotion path.** Previously, every concrete-class constructor dependency with an accessible parameterless constructor was silently materialized as a real instance (`ParameterRole.ConcreteInstance`) with a `Configure{Dep}(Action<T>)` helper. Tests whose SUT composes a concrete collaborator from port mocks lost depth-2/3 mock coverage as a result — the dominant driver of the empirically observed `[Cover<T>]` migration BAIL rate on Delta. The new `ConcreteHandling` named argument on `CoverAttribute<TService>` (enum: `Auto` (default, preserves prior behavior), `ForceMock`) lets a test opt every non-special concrete dependency into the standard `Mock<T>` substitution. `Auto` remains the default, so existing fixtures are unchanged.

### Changed
- **`TestFixturePipeline` syntax predicate tightened.** The pipeline used to run the semantic model on every `TypeDeclarationSyntax` in the compilation and then filter by attribute. It now syntactically pre-filters to types whose attribute lists name `Cover` / `CoverAttribute` (covering `[Cover<T>]`, `[CoverAttribute<T>]`, and the fully-qualified form), eliminating semantic-model invocations on the entire production codebase referenced by a test project. IDE responsiveness improvement; no consumer-visible behavior change beyond the namespace-match fix below.
- **`TestFixturePipeline` namespace match now exact instead of substring.** Previously `a.AttributeClass?.ToDisplayString().Contains("IoCTools.Testing")` would false-positive on consumer namespaces containing the substring (e.g. `MyCorp.IoCTools.Testing.Extensions.CoverAttribute<T>`), causing a duplicate fixture to be emitted under the wrong attribute resolution. The check is now an exact comparison against `IoCTools.Testing.Annotations`. Consumers who happened to name their own attribute `CoverAttribute<T>` inside an unrelated namespace are no longer incorrectly picked up.
- **`Cover<T>.Logger` named argument now parsed by enum-member symbol name** rather than raw int value, so future reorderings of `FixtureLoggerProfile` enum members cannot silently misclassify. Falls back to the prior integer comparison defensively.

### Fixed
- **`FixtureEmitter.CurrentOptionsProfile` mutable-static state removed.** Roslyn incremental generators must be pure functions of their inputs; a mutable static property breaks the generator cache and can race when multiple compilations run concurrently in the same generator-host AppDomain (long-lived IDE sessions, multi-project solutions). The property was unread outside its own declaration in production code; it has been deleted. No consumer-visible behavior change.

## [1.8.0] - 2026-05-12

### Changed
- **`[InjectConfiguration]` / `[DependsOnConfiguration<T>]` optional complex-type sections now fall back to `new T()` when the section is absent, instead of forwarding `null` through the null-forgiving operator.** Previously the generator emitted `configuration.GetSection("X").Get<T>()!`, which silently assigned `null` to the field when the section was absent in `IConfiguration` — every subsequent dereference NPE'd. This was observed end-to-end in Delta's `DeadLetterDiagnosticsProjector`, which crashed when its diagnostics section was missing from `appsettings.*.json`. The generator now emits `configuration.GetSection("X").Get<T>() ?? new T()` for complex-type fields whose type has an accessible parameterless constructor (concrete classes/structs). Required sections (`Required = true`, the default) still throw `InvalidOperationException` when absent — fail-fast semantics are unchanged. Interfaces, abstract classes, and types without a parameterless constructor continue to emit the `!` suppression (unchanged), since `new T()` is not a valid fallback for them. Collection bindings (`List<T>`, `Dictionary<,>`, arrays, …) and primitive `GetValue<T>` bindings are unchanged. Consumers who previously relied on `_field == null` to detect an absent optional section will now observe a default-constructed instance instead; this is a behavior change and the rationale for the minor version bump rather than a patch.

## [1.7.3] - 2026-05-06

### Fixed
- **IOC997 crash on array `TypedConstant` in `DependsOn`-family attribute constructor args.** `TestFixtureAnalyzer.AddTypedConstantDependency` previously called `.Value` on an Array-kinded `TypedConstant`, which throws `"TypedConstant is an array. Use Values property."` This occurs when a custom `DependsOn`-prefixed attribute is called with multiple `typeof()` arguments — the C# compiler stores them as a single array `TypedConstant`. The fix reorders the guard: array-kind check and recursive `.Values` processing now happen first; the `.Value as ITypeSymbol` access is only reached for non-array constants. `AttributeParser.GetDependsOnOptionsFromAttribute` and `GetNamingConventionOptionsFromAttribute` also now guard against array-kinded constructor arguments to prevent the same crash from the production generator pipeline.
- **TDIAG04 false positive for services with `[GeneratedCode("IoCTools")]` constructor.** `HasConstructorGenerationIntent` returned `false` for services that have no direct IoCTools lifetime attribute but whose constructor was generated by IoCTools in a prior compilation pass — e.g. Keel `LoggedHandler<T>` subclasses. The fix adds a final check in `HasConstructorGenerationIntent` that detects non-static, non-implicit constructors tagged `[GeneratedCode("IoCTools", ...)]`, treating them as evidence of IoCTools intent. Legitimate TDIAG04 errors are unchanged.

## [1.7.2] - 2026-05-06

### Fixed
- **`[Cover<T>]` / `CoverAttribute<>` / `FixtureLoggerProfile` source compatibility restored.** `IoCTools.Testing` now injects `<Using Include="IoCTools.Testing.Annotations" />` as a global using via its `build/` and `buildTransitive/` MSBuild `.targets` file. Consumers using `IoCTools.Testing` with `PrivateAssets="all"` (the standard pattern) and no explicit `using IoCTools.Testing.Annotations;` directive no longer receive CS0246. No explicit `IoCTools.Testing.Abstractions` reference required.
- **IOC997 scalar `TypedConstant` params parsing crash fixed.** `TestFixtureAnalyzer.AddTypedConstantDependency` now checks `value.Kind != TypedConstantKind.Array` before iterating `value.Values`. Previously, a single `nameof()` argument to a `params string[]` attribute parameter was stored by the C# compiler as a scalar `TypedConstant`, causing `InvalidOperationException: TypedConstant is not an array` which surfaced as IOC997. Multi-arg array paths are unchanged.
- **TDIAG08 severity downgraded from `Warning` to `Info`.** Legitimate manual construction of IoCTools-managed services exists (e.g. surface tests with bespoke stubs). `Info` means TDIAG08 is purely advisory and will not block compilation in projects with `TreatWarningsAsErrors=true`. Consumers who want strict mode can escalate via `<IoCToolsTestingDiagnosticSeverity>Warning</IoCToolsTestingDiagnosticSeverity>`.

## [1.7.1] - 2026-05-06

### Fixed
- **FixtureEvidence.TestsProject CLI evidence corpus build failure:** removed accidental `IoCTools.Testing` analyzer reference (only `IoCTools.Testing.Abstractions` is needed for `[Cover<T>]` examples); removed unused `ProductionPreferenceHelperTests` helper that incorrectly combined IoCTools dependencies with a manual constructor.
- **Note:** the `v1.7.0` tag was created and immediately withdrawn after CI revealed this build failure; no `1.7.0` packages were published to NuGet.

## [1.7.0] - 2026-05-06

### Added
- **`AnalysisScope` model and `DiagnosticGate`.** Each diagnostic now declares whether it fires in production projects, test projects, or both. `IOC081`/`IOC082`/`IOC086` are reclassified as `AnalysisScope.Production` — test projects are automatically exempt via the `IsTestProject` MSBuild property forwarded through `CompilerVisibleProperty`. No naming heuristics; detection is the standard Roslyn signal from `Microsoft.NET.Test.Sdk`.
- **Test fixture generator v2 (`IoCTools.Testing`).** `FixtureMemberPlanner` separates member planning from code emission. Generated fixtures now: enable nullable context; emit `new` modifier on derived fields that hide inherited members; support `GetRequiredSection` and binder-style configuration reads; treat `IClock` as fixture-provided; classify `ILogger<T>` and concrete dependency helpers correctly.
- **IOC073** (Info) — open-generic `IHostedService` implementers skipped at registration; omission is observable.
- **IOC066** (Info/Warning) — inaccessible `IHostedService` implementers skipped; reason surfaced in diagnostic.
- **IOC110** (Warning, `IoCToolsLifetimeValidationSeverity`) — fires when an interface has multiple IoCTools-managed implementations with different lifetimes and only *some* violate the dependency rule. `DependencyLifetimeResolver` now returns all candidate impls deterministically (sorted by full type name) and distinguishes attribute-declared vs implicit lifetimes in diagnostic messages. Replaces the previous non-deterministic single-impl selection that caused IOC012 to fire in Rider but silently pass in CLI.

### Changed
- **`IoCTools.Tools.Cli` multitargets `net9.0` and `net10.0`.** Existing .NET 9 global-tool consumers are not broken. Workflows install SDK `8.0.x`, `9.0.x`, and `10.0.x`.

### Fixed
- **IOC032 no longer fires on `[RegisterAs<...>(InstanceSharing.Shared)]`.** The redundancy check now parses the `InstanceSharing` argument and exempts `Shared`-mode registrations, which bridge multiple interfaces to one concrete instance. `Separate`-mode and bare `[RegisterAs]` continue to emit IOC032 unchanged.
- **`IoCTools.Generator.targets` now ships in `buildTransitive/` as well as `build/`.** Consumers receiving the generator transitively no longer silently drop the `IsTestProject` property forwarding, which broke the production-only diagnostic scope gate.
- **IOC086 no longer fires on three legitimate manual-registration shapes:** explicit factory lambdas (`sp => ...`), `TryAddEnumerable(ServiceDescriptor.X<T, TImpl>(...))`, and registrations whose implementation type lives in an IoCTools-unaware assembly. IOC081/IOC082 are similarly suppressed for the `TryAddEnumerable` and `IHostedService` bridge shapes.
- **`[RegisterAs<T>]` and `[RegisterAsAll]` now compose with `IHostedService`.** Previously the generator silently dropped companion-interface registrations when `IHostedService` was also implemented. The concrete class is now registered once at the declared lifetime, companion interfaces are bridged via `GetRequiredService<TImpl>()`, and `IHostedService` is bridged to the same instance.
- **Open-generic `IHostedService` implementers no longer produce CS0246.** Registration is skipped (IOC073 emitted) instead of emitting an unresolvable open-generic type reference.
- **Inaccessible `IHostedService` implementers no longer produce CS0122.** The selector now walks the containing-type chain and skips emission when any link is below `internal` (IOC066 emitted).
- **IOC081/IOC082/IOC086 carve-outs extended.** The `services.Replace(...)` carve-out (IOC081, landed in 1.6.1) now also covers IOC082 and IOC086. The `IHostedService` companion-interface bridge shape (`AddSingleton<IHostedService>(sp => sp.GetRequiredService<TImpl>())`) is suppressed for IOC081/IOC082.
- **CLI `dotnet` host-path resolver now walks `PATH`.** `ProjectContext` previously fell back to the bare string `"dotnet"` when `DOTNET_HOST_PATH` was unset, which silently failed MSBuild loading in some global-tool installations. The resolver now searches each `PATH` entry for the `dotnet` executable before throwing a descriptive `InvalidOperationException`.

### Packaging
- All seven packages normalized to `1.7.0` (no pre-release suffix).
- `IoCTools.Tools.Cli` multitargets `net9.0;net10.0`; workflows install SDK `8.0.x`, `9.0.x`, `10.0.x`.

## [1.6.1] - 2026-04-29

### Fixed
- **IOC081 no longer fires on `services.Replace(ServiceDescriptor.X<T>(...))`** — the canonical override pattern. Previously the diagnostic flagged any `ServiceDescriptor.{Lifetime}<T>(...)` factory call as a duplicate when IoCTools auto-registered `T`, even when it was wrapped in `IServiceCollection.Replace(...)` — the explicit "swap this implementation" call shape that test suites use to substitute fakes for the real registration. The validator now walks the ancestor chain when it sees a `ServiceDescriptor.X<T>()` invocation; if the wrapping call is `IServiceCollection.Replace(...)`, the inner factory is treated as an override, not a duplicate registration.

## [1.6.0] - 2026-04-22

### Added
- **Auto-deps feature** — universal, profile-scoped, and transitive dependency injection without per-service declaration
  - `[assembly: AutoDep<T>]` for universal closed-type auto-deps
  - `[assembly: AutoDepOpen(typeof(T<>))]` for single-arity open-generic auto-deps
  - Built-in auto-detection of `Microsoft.Extensions.Logging.ILogger<T>` when referenced — zero-config logger injection
  - Profile system: `IAutoDepsProfile` marker interface, `[assembly: AutoDepIn<TProfile, T>]`, `[assembly: AutoDepsApply<TProfile, TBase>]`, `[assembly: AutoDepsApplyGlob<TProfile>("pattern")]`, `[AutoDeps<TProfile>]`
  - Transitive scope via `AutoDepScope.Transitive` — libraries ship opinionated defaults to consumers
  - Opt-out ladder: `[NoAutoDeps]`, `[NoAutoDep<T>]`, `[NoAutoDepOpen(typeof(T<>))]`
- **11 new diagnostics** covering `[Inject]` deprecation, stale opt-outs, profile validation, constraint violations, invalid glob patterns, and redundant attachments. IDs: IOC095-IOC099, IOC103-IOC108 (IOC100-IOC102 remained assigned to `IoCTools.FluentValidation`; the three `AutoDepOpen`-validation diagnostics that were originally planned for those IDs were renumbered to IOC106-IOC108 to avoid a suppression collision).
- **Roslyn code fix** for IOC095 via new `IoCTools.Generator.Analyzer` package — IDE lightbulb migrates `[Inject]` fields to `[DependsOn<T>]`
- **`ioc-tools profiles` subcommand** — introspect auto-deps profiles, their contributions, matches, and attachment sources
- **`ioc-tools migrate-inject` subcommand** — headless bulk `[Inject]` → `[DependsOn<T>]` migration for CI and non-IDE workflows. Respects IOC095 suppressions: fields under `[SuppressMessage("IoCTools.Usage", "IOC095", ...)]` (field- or class-level) or inside a `#pragma warning disable IOC095` region are left untouched, so deliberate demo/fixture `[Inject]` patterns survive the bulk pass.
- **CLI enhancements** — `graph`/`why`/`explain`/`evidence` support `--hide-auto-deps` / `--only-auto-deps` filters; `doctor` adds three auto-deps preflight checks; source attribution surfaced across all inspection commands
- **MSBuild properties** — `IoCToolsAutoDepsDisable`, `IoCToolsAutoDepsExcludeGlob`, `IoCToolsAutoDepsReport`, `IoCToolsAutoDetectLogger`, `IoCToolsInjectDeprecationSeverity`

### Changed
- **`[Inject]` is deprecated** in favor of `[DependsOn<T>]`. The attribute is marked `[Obsolete]` and emits IOC095 (warning severity in 1.6; upgrades to error in 1.7; removed in 2.0). A Roslyn code fix + `ioc-tools migrate-inject` CLI ship alongside the deprecation.
- `IoCTools.Sample` migrated off `[Inject]` except for a dedicated `InjectDeprecationExamples.cs` demonstrating IOC095 behavior

### Fixed
- `AttributeParser.GetDependsOnOptionsFromAttribute` now preserves memberName slot alignment for sparse named args. Previously `[DependsOn<T1, T2>(memberName2: "_x")]` misaligned `"_x"` to slot 0 after list compaction.

### Known issues
- IOC095 diagnostic ID was previously used by `OpenGenericSharedInstanceFallsBackToSeparate` in 1.5.1. The 1.6 reassignment (IOC095 = `[Inject]` deprecation) ships with both descriptors registered under the same ID. Consumers with existing IOC095 suppressions should review — the suppression now applies to the deprecation warning instead of the open-generic fallback notice.

## [1.5.1] - 2026-04-12

### Added
- first real public `1.5.x` release across `IoCTools.Abstractions`, `IoCTools.Generator`, `IoCTools.Tools.Cli`, `IoCTools.Testing`, and `IoCTools.FluentValidation`
- official support for the common open-generic attribute path that corresponds to `typeof(IFoo<>), typeof(Foo<>)`
- IOC095 warning for open-generic `InstanceSharing.Shared` requests that must fall back to separate registrations in `Microsoft.Extensions.DependencyInjection`
- tag-driven release workflow that packs and publishes all five package artifacts coherently

### Changed
- generator analysis failures now surface `IOC093` instead of silently degrading output
- README, CLI docs, attribute docs, diagnostics docs, migration docs, and sample messaging now describe the real `1.5.1` public release posture
- `[Inject]` and `InjectConfiguration` guidance is explicitly compatibility-only in `1.5.1`; new code should use `[DependsOn]`, `[DependsOnConfiguration]`, and `[DependsOnOptions]`

### Fixed
- invalid open-generic implementation factory aliases are no longer emitted; IoCTools now falls back to valid direct open-generic registrations
- package metadata, repository links, and release automation are aligned for the public `1.5.1` line

## [1.5.0] - 2026-03-21

### Added
- typeof() registration diagnostics (IOC090-IOC094) for build-time validation of manual DI registrations
- IoCTools.Testing package with `[Cover<T>]` attribute for auto-generated test fixtures (Mock<T> fields, CreateSut(), setup helpers)
- Test fixture analyzer diagnostics (TDIAG-01 through TDIAG-05) suggesting fixture usage
- CLI improvements: `--json` output mode, `--verbose` debugging, color-coded severity output
- CLI `evidence` command for correlated project/type/service review packets
- CLI evidence artifact fingerprints and structured baseline compare deltas for machine-readable review receipts
- CLI wildcard filtering and fuzzy suggestions for type names
- CLI `config-audit` command for detecting missing configuration keys
- CLI `suppress` command for generating .editorconfig diagnostic suppression recipes with structured JSON metadata
- FluentValidation source generator support (IoCTools.FluentValidation) with validator discovery, composition graphs, and anti-pattern diagnostics (IOC100-IOC102)
- CLI `validators` and `validator-graph` commands for inspecting FluentValidation validators
- FluentValidation-aware test fixture helpers (`SetupValidationSuccess`/`SetupValidationFailure`)

### Changed
- Enhanced IOC012/IOC013/IOC087 messages with IServiceProvider/CreateScope() suggestions
- IOC015 now shows full inheritance path (A -> B -> C format) for lifetime mismatches
- All 94+ diagnostics now have HelpLinkUri pointing to docs/diagnostics.md#iocXXX
- Diagnostic messages reference individual lifetime attributes ([Scoped], [Singleton], [Transient])
- `validator-graph --json` and `validator-graph --why --json` now emit structured machine-readable contracts
- `[Inject]` and `InjectConfiguration` are now documented as compatibility-only in `1.5.0`; new code guidance points to `[DependsOn]`, `[DependsOnConfiguration]`, and `[DependsOnOptions]`
- `IoCTools.Tools.Cli` packages now include a NuGet readme, and analyzer-style packages include matching placeholder `lib` assets to satisfy NuGet packaging expectations

### Fixed
- CS8603 null reference warnings in sample code
- Exception handling in ConstructorGenerator, InterfaceDiscovery, and ServiceRegistrationGenerator

### Diagnostic
- 5 new typeof() diagnostics (IOC090-IOC094)
- 5 new test fixture diagnostics (TDIAG-01 through TDIAG-05)

[Unreleased]: https://github.com/sansiquay/IoCTools/compare/v1.9.1...HEAD
[1.9.1]: https://github.com/sansiquay/IoCTools/compare/v1.9.0...v1.9.1
[1.9.0]: https://github.com/sansiquay/IoCTools/compare/v1.8.0...v1.9.0
[1.8.0]: https://github.com/sansiquay/IoCTools/compare/v1.7.3...v1.8.0
[#7]: https://github.com/sansiquay/IoCTools/issues/7
[#8]: https://github.com/sansiquay/IoCTools/pull/8
[1.7.3]: https://github.com/sansiquay/IoCTools/compare/v1.7.2...v1.7.3
[1.7.2]: https://github.com/sansiquay/IoCTools/compare/v1.7.1...v1.7.2
[1.7.1]: https://github.com/sansiquay/IoCTools/compare/v1.6.1...v1.7.1
[1.7.0]: https://github.com/sansiquay/IoCTools/compare/v1.6.1...6b7e7be41ba2e9d26f5717644dc7f3bc376a7486
[1.6.1]: https://github.com/sansiquay/IoCTools/compare/v1.6.0...v1.6.1
[1.6.0]: https://github.com/sansiquay/IoCTools/compare/v1.5.1...v1.6.0
[1.5.1]: https://github.com/sansiquay/IoCTools/compare/v1.5.0...v1.5.1
[1.5.0]: https://github.com/sansiquay/IoCTools/compare/v1.4.0...v1.5.0
