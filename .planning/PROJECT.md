# IoCTools

## What This Is

IoCTools is a .NET source generator library that simplifies dependency injection in .NET applications. It auto-discovers services via lifetime attributes (`[Scoped]`, `[Singleton]`, `[Transient]`), generates constructors, validates DI configuration at build time with 102 diagnostics (IOC001-IOC094, IOC100-IOC102, TDIAG-01..05), generates test fixtures, supports FluentValidation validator discovery, and includes a CLI for inspection/debugging.

## Core Value

Eliminate DI boilerplate — both in production code (service registration, constructors) and in test code (mock declarations, SUT construction) — with zero runtime overhead through compile-time source generation.

## Requirements

### Validated

- ✓ Automatic service registration via `[Scoped]`, `[Singleton]`, `[Transient]` — v1.0
- ✓ Constructor generation with `[Inject]` fields and `[DependsOn]` attributes — v1.0
- ✓ Full inheritance chain support with proper base constructor calling — v1.0
- ✓ Comprehensive diagnostic system IOC001-IOC086 with configurable MSBuild severity — v1.0
- ✓ Selective interface registration with `[RegisterAs<T>]` and InstanceSharing — v1.0
- ✓ Configuration injection with `[InjectConfiguration]` — v1.0
- ✓ IHostedService detection and registration — v1.0
- ✓ Cross-assembly diagnostic validation — v1.0
- ✓ HelpLinkUri on all diagnostic descriptors with docs/diagnostics.md anchors — v1.5.0
- ✓ Diagnostic IDE categories (Lifetime, Dependency, Configuration, Registration, Structural, Testing) — v1.5.0
- ✓ typeof() diagnostics (IOC090-094) detecting manual registration patterns — v1.5.0
- ✓ CLI: JSON output, verbose debugging, color-coded output, wildcard filtering, fuzzy suggestions — v1.5.0
- ✓ CLI suppress command for .editorconfig diagnostic suppression — v1.5.0
- ✓ Test fixture generation with `Mock<T>` fields, `CreateSut()` factories, typed helpers — v1.5.0
- ✓ IoCTools.Testing package with `Cover<T>` attribute and Moq integration — v1.5.0
- ✓ Test fixture diagnostics (TDIAG-01..05) — v1.5.0
- ✓ Multi-page documentation (getting-started, attributes, diagnostics, CLI, testing, migration, config, platform-constraints) — v1.5.0
- ✓ FluentValidation source generator with validator discovery and registration refinement — v1.5.0
- ✓ Composition graph builder (SetValidator/Include/SetInheritanceValidator) — v1.5.0
- ✓ Anti-pattern diagnostics IOC100-102 (direct instantiation, lifetime mismatch) — v1.5.0
- ✓ Test fixture FluentValidation helpers (SetupValidationSuccess/Failure) — v1.5.0
- ✓ CLI validator inspection and graph commands — v1.5.0

### Active

*No active requirements. Use `/gsd:new-milestone` to plan next work.*

### Out of Scope

- NSubstitute / FakeItEasy support — Moq-only for now, revisit based on user demand
- CodeFixProvider for diagnostics — requires separate analyzer package, high complexity
- Diagnostic for 20+ dependencies — validate threshold first, defer
- Progress indicators for CLI — most ops complete in 1-5 seconds

## Context

**Architecture:**
- Five NuGet packages: `IoCTools.Abstractions` (netstandard2.0), `IoCTools.Generator` (netstandard2.0), `IoCTools.Tools.Cli` (net8.0 tool), `IoCTools.Testing` (net8.0), `IoCTools.FluentValidation` (netstandard2.0)
- Generator uses Roslyn `IIncrementalGenerator` with three pipelines: service discovery, constructor emission, diagnostics
- IoCTools.Testing provides `Cover<T>` attribute for test fixture generation with Moq integration
- IoCTools.FluentValidation provides validator discovery, composition graph analysis, and anti-pattern detection via partial method hook
- CLI (`ioc-tools`) with 13 commands: services, fields, explain, graph, why, doctor, compare, profile, config-audit, suppress, validators, validator-graph

**Stats:**
- ~100,303 lines of C# code
- 102 diagnostics (IOC001-094, IOC100-102, TDIAG-01..05)
- 11 documentation files, 3,420 lines
- ~1,814 tests across 4 test suites
- CI: GitHub Actions with auto-publish to NuGet

## Constraints

- **netstandard2.0**: Generator, Abstractions, and FluentValidation must maintain netstandard2.0 target — no records, init-only properties, required members
- **IoCTools.Testing target**: Can target net8.0+ since it's test-project-only
- **Moq dependency**: IoCTools.Testing depends on Moq 4.20.72
- **Source generator limitations**: Generated code must work within Roslyn constraints (no runtime reflection)

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Moq-only for test fixtures | Concrete generated code, Moq dominant in .NET ecosystem | ✓ Good |
| Separate IoCTools.Testing package | Test deps must not leak into production packages | ✓ Good |
| Multi-page docs in /docs/ | Content exceeded single-doc readability threshold | ✓ Good |
| Partial method hook for FV | Silent removal when unimplemented, C# 3.0+ compatible | ✓ Good |
| Name-based type detection for FV | Avoids requiring FluentValidation package reference in generator | ✓ Good |
| IOC100+ numbering for FV diagnostics | Avoids collision with IOC001-094 and TDIAG-01..05 | ✓ Good |
| Composition edges in ValidatorClassInfo | Pipeline coherence over separate pipeline | ✓ Good |
| Manual diagnostic catalog over reflection | Explicit control, no runtime overhead | ✓ Good |

## Current State

**Shipped v1.5.0** (2026-03-29): Test fixture generation, typeof() diagnostics, CLI improvements, documentation overhaul, FluentValidation source generator, and gap closure phases.

**Tech debt:**
- `IoCTools.FluentValidation.csproj` PackageProjectUrl/RepositoryUrl reference `nate123456` instead of `nathan-p-lane`
- CS8602 nullable warning in FixtureEmitter.cs line 147

**Next Milestone:** TBD — Use `/gsd:new-milestone` to plan next work.

---
*Last updated: 2026-03-29 after v1.5.0 milestone completion*
