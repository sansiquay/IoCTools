# IoCTools v1.5.0

## What This Is

IoCTools is a .NET source generator library that simplifies dependency injection in .NET applications. It auto-discovers services via lifetime attributes, generates constructors, validates DI configuration at build time with 86 diagnostics (IOC001-IOC086), and includes a CLI for inspection/debugging. This milestone adds a test fixture generator, expands diagnostics, improves the CLI, and overhauls documentation.

## Core Value

Eliminate DI boilerplate — both in production code (service registration, constructors) and now in test code (mock declarations, SUT construction) — with zero runtime overhead through compile-time source generation.

## Requirements

### Validated

- ✓ Automatic service registration via `[Scoped]`, `[Singleton]`, `[Transient]` — existing
- ✓ Constructor generation with `[Inject]` fields and `[DependsOn]` attributes — existing
- ✓ Full inheritance chain support with proper base constructor calling — existing
- ✓ Comprehensive diagnostic system IOC001-IOC086 with configurable MSBuild severity — existing
- ✓ Selective interface registration with `[RegisterAs<T>]` and InstanceSharing — existing
- ✓ Configuration injection with `[InjectConfiguration]` — existing
- ✓ IHostedService detection and registration — existing
- ✓ CLI tool (`ioc-tools`) with services, fields, explain, graph, why, doctor, compare, profile, config-audit commands — existing
- ✓ Cross-assembly diagnostic validation — existing
- ✓ 1650+ tests passing across generator and CLI test suites — existing
- ✓ typeof() diagnostics (IOC090-094) detecting manual registration patterns — validated in Phase 02
- ✓ CLI improvements: JSON output, verbose debugging, color-coded output — validated in Phase 02
- ✓ CLI wildcard filtering, fuzzy type suggestions, service count — validated in Phase 02
- ✓ CLI suppress command for .editorconfig diagnostic suppression — validated in Phase 02
- ✓ Test fixture generation with `Mock<T>` fields, `CreateSut()` factories, and typed helpers — validated in Phase 03
- ✓ IoCTools.Testing package with `Cover<T>` attribute and Moq 4.20.72 dependency — validated in Phase 03
- ✓ Test fixture analyzer diagnostics (TDIAG-01 through TDIAG-05) for manual mock/SUT boilerplate detection — validated in Phase 03
- ✓ HelpLinkUri on all diagnostic descriptors with anchor links to docs/diagnostics.md — validated in Phase 04
- ✓ Diagnostic IDE categories (Lifetime, Dependency, Configuration, Registration, Structural, Testing) — validated in Phase 04
- ✓ IServiceProvider/CreateScope() pattern suggestions in IOC012/013 — validated in Phase 04
- ✓ Enhanced config error messages with examples for IOC016-019 — validated in Phase 04
- ✓ Full inheritance path shown in IOC015 diagnostic — validated in Phase 04
- ✓ Multi-page documentation structure with /docs/ directory — validated in Phase 04
- ✓ Getting started guide (30-second, 5-minute, conceptual sections) — validated in Phase 04
- ✓ Attributes reference with examples for all 15+ attributes — validated in Phase 04
- ✓ Diagnostics reference (docs/diagnostics.md) with 99 diagnostics, category navigation, severity badges — validated in Phase 04
- ✓ CLI reference (docs/cli-reference.md) documenting all 11 commands — validated in Phase 04
- ✓ IoCTools.Testing package documentation (docs/testing.md) — validated in Phase 04
- ✓ Platform constraints documentation (docs/platform-constraints.md) — validated in Phase 04
- ✓ Migration guide (docs/migration.md) for manual DI, Autofac, StructureMap, DryIoc — validated in Phase 04
- ✓ CHANGELOG.md following Keep a Changelog format — validated in Phase 04
- ✓ Centralized RegisterAsAllAttribute checks using AttributeTypeChecker — validated in Phase 01
- ✓ ReportDiagnosticDelegate pattern adopted in validators — validated in Phase 01
- ✓ CS8603 null reference warnings resolved in sample code — validated in Phase 01
- ✓ Code comments explaining InstanceSharing.Separate default behavior — validated in Phase 01

### Active

*All requirements validated. No active requirements remaining.*

### Out of Scope

- NSubstitute / FakeItEasy support — Moq-only for v1, revisit based on user demand
- CodeFixProvider for diagnostics — requires separate analyzer package, high complexity
- Diagnostic for 20+ dependencies — validate threshold first, defer
- Progress indicators for CLI — most ops complete in 1-5 seconds
- Test assertion standardization — 271 usages, internal-only, low impact

## Context

**Existing Architecture:**
- Four NuGet packages: `IoCTools.Abstractions` (netstandard2.0), `IoCTools.Generator` (netstandard2.0), `IoCTools.Tools.Cli` (net8.0 tool), `IoCTools.Testing` (net8.0)
- Generator uses Roslyn `IIncrementalGenerator` with three pipelines: service discovery, constructor emission, diagnostics
- IoCTools.Testing package provides `Cover<T>` attribute for test fixture generation with Moq integration
- The generator has full knowledge of the dependency graph — test fixture generator leverages this

**Real-World Test Patterns (from Delta project analysis):**
- Services tested with constructor-based setup, `Mock<T>` field declarations at class level
- SUT constructed manually: `new Handler(mock1.Object, mock2.Object, mock3.Object)`
- Factory methods used for complex handler instantiation (e.g., `CreateService()`)
- TestBuilder pattern for domain objects (fluent interface)
- Pain points: mock field declaration repetition, constructor parameter wiring, setup duplication across test classes
- Moq is the primary mocking framework with `new Mock<T>()` initialization

**CI/CD:**
- GitHub Actions with `alirezanet/publish-nuget` auto-publishing on version change
- CI recently fixed to properly restore/build CLI test projects

**Current State:**
- **Version:** v1.5.0 (shipped 2026-03-21)
- **LOC:** ~100,303 lines of C# code
- **Packages:** 4 NuGet packages (Abstractions, Generator, Tools.Cli, Testing)
- **Diagnostics:** 102 total (IOC001-IOC094, IOC100-IOC102, TDIAG-01-TDIAG-05)
- **Documentation:** 11 files, 3,420 lines across README.md, CHANGELOG.md, /docs/ directory
- **Tests:** 1650+ passing across all test suites
- **All v1.5.0 requirements validated**

## Constraints

- **netstandard2.0**: Generator and Abstractions must maintain netstandard2.0 target for broad compatibility — no records, init-only properties, required members
- **IoCTools.Testing target**: Can target net8.0+ since it's test-project-only
- **Moq dependency**: IoCTools.Testing will take a dependency on Moq — version should align with common usage (latest stable)
- **Source generator limitations**: Generated test fixtures must work within Roslyn source generator constraints (no runtime reflection)

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Moq-only for test fixtures | Keeps generated code concrete, avoids abstraction gymnastics, Moq is dominant in .NET ecosystem | ✓ Implemented — IoCTools.Testing with Moq 4.20.72 |
| Separate IoCTools.Testing package | Test dependencies (Moq, xUnit) must not leak into production packages | ✓ Implemented — Separate analyzer package, no transitive dependencies |
| Evaluate docs structure before committing to multi-page | Don't over-engineer docs if single-doc still works; but prepare to migrate if content exceeds reasonable size | ✓ Implemented — Multi-page /docs/ structure (3,420 lines) |

## Current State

**Shipped v1.5.0:** Test fixture generation with IoCTools.Testing package, typeof() diagnostics (IOC090-094), CLI improvements, and documentation overhaul.

**Phase 05 complete:** Fixed solution build failure (MSB5004 duplicate project), added IOC100-102 FluentValidation entries to CLI diagnostic catalog, corrected HelpLinkUri username, suppressed RS2008 analyzer warning.

**Phase 06 complete:** FluentValidation documentation integration — added IOC100-102 to diagnostics reference with anchors matching HelpLinkUri, documented CLI `validators` and `validator-graph` commands, documented FluentValidation test fixture helpers, updated README.md and CHANGELOG.md.

**Next Milestone:** TBD — Use `/gsd:new-milestone` to plan next work.

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd:transition`):
1. Requirements invalidated? -> Move to Out of Scope with reason
2. Requirements validated? -> Move to Validated with phase reference
3. New requirements emerged? -> Add to Active
4. Decisions to log? -> Add to Key Decisions
5. "What This Is" still accurate? -> Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-03-29 after Phase 05 completion*
