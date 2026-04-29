# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

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
- **`ioc-tools migrate-inject` subcommand** — headless bulk `[Inject]` → `[DependsOn<T>]` migration for CI and non-IDE workflows
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

[Unreleased]: https://github.com/sansiquay/IoCTools/compare/v1.5.1...HEAD
[1.5.1]: https://github.com/sansiquay/IoCTools/compare/v1.5.0...v1.5.1
[1.5.0]: https://github.com/sansiquay/IoCTools/compare/v1.4.0...v1.5.0
