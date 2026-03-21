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

### Active

**Test Fixture Generation (new package: `IoCTools.Testing`)**
- [ ] Generate test fixture base classes that auto-declare `Mock<T>` fields for all service dependencies
- [ ] Generate SUT factory methods that wire mocks into the service constructor
- [ ] Generate typed mock setup helper methods for readable test arrangement
- [ ] Support Moq as the mocking framework (Moq-only, no abstraction layer)
- [ ] Ship as separate NuGet package `IoCTools.Testing` to isolate test-time dependencies
- [ ] Work with services that use `[Inject]`, `[DependsOn]`, inheritance, and configuration injection

**typeof() Diagnostics (IOC090-094)**
- [ ] Add typeof() argument parsing to ManualRegistrationValidator
- [ ] IOC090: typeof() interface-implementation registration could use IoCTools
- [ ] IOC091: typeof() registration duplicates IoCTools registration
- [ ] IOC092: typeof() registration lifetime mismatch
- [ ] IOC094: Open generic typeof() could use IoCTools attributes
- [ ] Integration tests and sample project examples for all typeof() diagnostics

**Diagnostic UX Improvements**
- [ ] Add HelpLinkUri to all diagnostic descriptors
- [ ] Use specific IDE categories (Lifetime, Dependency, Configuration, Registration, Structural)
- [ ] Suggest IServiceProvider/CreateScope() pattern in IOC012/013
- [ ] Better config error messages with examples for IOC016-019
- [ ] Show full inheritance path in IOC015 diagnostic

**CLI Improvements**
- [ ] --verbose flag for debugging (MSBuild diagnostics, generator timing, file paths)
- [ ] --json output mode for all commands
- [ ] Color-code diagnostic output by severity (red/yellow/cyan)
- [ ] Extend fuzzy type suggestions to all commands
- [ ] Add wildcard/regex support to FilterByType
- [ ] Add service count to profile command output
- [ ] Add .editorconfig recipe for suppressing IoCTools diagnostics

**Code Quality**
- [ ] Centralize RegisterAsAllAttribute checks using AttributeTypeChecker (20 inconsistent locations)
- [ ] Adopt ReportDiagnosticDelegate pattern in 3-4 more validators
- [ ] Resolve CS8603 null reference warnings in sample code (3 instances in MultiInterfaceExamples.cs)
- [ ] Add code comments explaining InstanceSharing.Separate default behavior

**Documentation Overhaul**
- [ ] Evaluate whether single-doc README can hold all features or needs multi-page structure
- [ ] If warranted, migrate to multi-page docs (getting started, attributes reference, diagnostics reference, CLI reference, test fixtures guide)
- [ ] Update all docs to cover v1.3.0 features completely
- [ ] Cross-reference netstandard2.0 constraints
- [ ] Document the new IoCTools.Testing package and test fixture generation

### Out of Scope

- NSubstitute / FakeItEasy support — Moq-only for v1, revisit based on user demand
- CodeFixProvider for diagnostics — requires separate analyzer package, high complexity
- Diagnostic for 20+ dependencies — validate threshold first, defer
- Progress indicators for CLI — most ops complete in 1-5 seconds
- Test assertion standardization — 271 usages, internal-only, low impact

## Context

**Existing Architecture:**
- Three NuGet packages: `IoCTools.Abstractions` (netstandard2.0), `IoCTools.Generator` (netstandard2.0), `IoCTools.Tools.Cli` (net8.0 tool)
- Generator uses Roslyn `IIncrementalGenerator` with three pipelines: service discovery, constructor emission, diagnostics
- The new `IoCTools.Testing` package will be a fourth package, likely targeting net8.0+ since test projects don't need netstandard2.0 compatibility
- The generator already has full knowledge of the dependency graph — the test fixture generator can leverage this

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

## Constraints

- **netstandard2.0**: Generator and Abstractions must maintain netstandard2.0 target for broad compatibility — no records, init-only properties, required members
- **IoCTools.Testing target**: Can target net8.0+ since it's test-project-only
- **Moq dependency**: IoCTools.Testing will take a dependency on Moq — version should align with common usage (latest stable)
- **Source generator limitations**: Generated test fixtures must work within Roslyn source generator constraints (no runtime reflection)

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Moq-only for test fixtures | Keeps generated code concrete, avoids abstraction gymnastics, Moq is dominant in .NET ecosystem | — Pending |
| Separate IoCTools.Testing package | Test dependencies (Moq, xUnit) must not leak into production packages | — Pending |
| Evaluate docs structure before committing to multi-page | Don't over-engineer docs if single-doc still works; but prepare to migrate if content exceeds reasonable size | — Pending |

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
*Last updated: 2026-03-21 after initialization*
