## Mission

Continue IoCTools work with full repo access. Keep tests green while iterating on dependency-set/config/lifetime features and CLI roadmap alignment.

## Current State (2025-11-21)

- Repo: `/Users/nathan/Documents/projects/IoCTools`.
- All tests currently pass: `dotnet test --no-build` → 1154 generator tests + CLI tests (only NET6 EOL warnings). Keep it that way.
- Dependency sets are implemented and recursive; analyzers/diagnostics extended (IOC049–IOC056). CLI README updated with proposed commands (`explain`, `graph`, `why`, `doctor`, `compare`, `profile`, `config-audit`).
- Key new tests: `DependencySetTests.cs`, `DependencySetInheritanceTests.cs`, `AdditionalDependencySetEdgeTests.cs`, plus config overlap/suggestion tests.
- Suggestion heuristics (IOC053–IOC055) now consider config and services via `DependencySetSuggestionValidator` (uses `GetDependencyKey`). Nullable warnings (CS8602) remain there.
- Config overlap validator uses set-aware collection; IOC056 warns on options+primitive overlaps (including via sets).
- README aligned with features (recursive sets, config-inclusive suggestions, IOC056). CLI section expanded with planned commands.

## Objectives / Next Steps

Short-term (pick any):
1) Implement CLI commands from README (explain/graph/why/doctor/compare/profile/config-audit) minimally by reusing generator artifacts; keep tests green.
2) Clean nullable warnings in `DependencySetSuggestionValidator.cs` (~lines 120–135) without changing behavior.
3) Add tests combining config + service for IOC053/IOC054 suggestions and shared-base IOC055 when config overlaps; ensure collisions/lifetime checks still behave.
4) Verify `GetDependencyKey` continues to include config (field name + type) and is used consistently in suggestions.
5) Leave perf tests alone unless they fail; thresholds were already relaxed.

Longer-term ideas (future): code actions, graph exports, doctor auto-fixes, config-audit vs appsettings, MSBuild graph dump.

## Files to Know

- README.md — feature/CLI docs, diagnostics table.
- IoCTools.Abstractions/IDependencySet.cs — marker.
- IoCTools.Abstractions/Annotations/DependsOn.cs & DependsOnConfigurationAttribute.cs — interfaces allowed; params member names/keys.
- IoCTools.Generator/Analysis/DependencyAnalyzer.cs — dependency collection & set expansion.
- IoCTools.Generator/Analysis/DependencySetExpander.cs — recursive flattening.
- IoCTools.Generator/Generator/Diagnostics/Validators/
  - DependencySetValidator.cs (metadata-only, cycles, registration misuse).
  - DependencySetSuggestionValidator.cs (IOC053–IOC055; config-aware; nullable warnings outstanding).
  - ConfigurationRedundancyValidator.cs (IOC046, set-aware).
- IoCTools.Generator/Utilities/AttributeParser.cs — parses memberNames/config keys (case-insensitive arrays).
- Tests: DependencySetTests.cs, DependencySetInheritanceTests.cs, AdditionalDependencySetEdgeTests.cs, ConfigurationInjectionTests.cs (IOC056), DependencyRedundancyTests.cs.

## Pitfalls / Gotchas

- Always keep tests passing; run `dotnet test --no-build` (fast). Expect NETSDK1138 warnings.
- IOC051 collisions can emit multiple diagnostics; tests assert count ≥ 1.
- External deps: ensure implementations exist if not marked external; in tests ITimer impl is `[RegisterAs<ITimer>]` singleton.
- Config keys are part of dependency keys for suggestions; don’t regress.
- Use `apply_patch`; no direct file writes.

## Useful Commands

- `dotnet test --no-build`
- `rg -n "DependencySet" IoCTools.Generator.Tests`
- `rg -n "DependencySetSuggestionValidator" IoCTools.Generator`
- `rg -n "IOC05" IoCTools.Generator/IoCTools.Generator`

## Key Domain Facts

- Diagnostics: IOC049–052 (sets metadata/cycles/collisions/registration), IOC053–055 (DRY suggestions include config), IOC056 (options+primitive overlap including via sets).
- Collections: only `IReadOnlyCollection<T>` supported for multi-impl; others warn IOC045.
- Config overlap: IOC046; config collected from sets/inheritance.
