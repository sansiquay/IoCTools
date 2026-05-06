# IoCTools.Testing Fixture Generation vNext Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build vNext generated/scaffolded service-test fixtures so IoCTools.Testing removes constructor/mock/logger/options/config boilerplate while preserving explicit business assertions.

**Architecture:** Keep fixture generation compile-time only. Add a shared fixture-member planner used by generator tests, diagnostics, and CLI projections so generated shape, diagnostics, and scaffold evidence agree. Add CLI test scaffold/evidence workflows without changing production DI generation.

**Tech Stack:** C#, Roslyn source generators/analyzers, xUnit, Moq, Microsoft.Extensions.Configuration/Options/Logging, existing IoCTools.Tools.Cli command infrastructure.

---

## Scope Guard

- Do not rewrite production DI generation.
- Do not introduce runtime reflection fixture construction.
- Do not add Moq/test dependencies to production projects.
- Do not generate business assertions.
- Do not replace Delta/Keel semantic harnesses.
- Do not hard-code Delta-specific `TestClock`.
- Do not add compatibility shims around deprecated injection markers.
- Keep current `[Cover<T>]` behavior compatible.

## Preconditions

- Baseline tests pass or current failures are documented:
  - `dotnet test IoCTools.Testing.Tests`
  - `dotnet test IoCTools.Generator.Tests --filter TestFixture`
  - `dotnet test IoCTools.Tools.Cli.Tests --filter Test`
- Confirm `docs/superpowers/specs/2026-05-04-ioc-tools-testing-fixture-generation-vnext-design.md` remains accepted source of truth.
- Confirm no downstream Delta/Keel edits are part of this implementation phase.

## File Map

Generator:

- `IoCTools.Testing/IoCTools.Testing/Generator/Pipeline/TestFixturePipeline.cs`
- `IoCTools.Testing/IoCTools.Testing/CodeGeneration/FixtureEmitter.cs`
- `IoCTools.Testing/IoCTools.Testing/CodeGeneration/FluentValidationFixtureHelper.cs`
- `IoCTools.Testing/IoCTools.Testing/Analysis/ConstructorReader.cs`
- `IoCTools.Testing/IoCTools.Testing/Utilities/TypeNameUtilities.cs`

Diagnostics:

- `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/TestFixtureAnalyzer.cs`
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/TestFixtureDiagnostics.cs`
- Any existing diagnostic registration/configuration files used by `TestFixtureAnalyzer`.

CLI:

- `IoCTools.Tools.Cli/Program.cs`
- `IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs`
- `IoCTools.Tools.Cli/Utilities/EvidenceModels.cs`
- `IoCTools.Tools.Cli/Utilities/EvidencePrinter.cs`
- New CLI services for fixture scaffold/evidence planning.

Tests:

- `IoCTools.Testing.Tests/BasicServiceFixtureTests.cs`
- `IoCTools.Testing.Tests/InheritanceFixtureTests.cs`
- `IoCTools.Testing.Tests/ConfigurationFixtureTests.cs`
- `IoCTools.Testing.Tests/GenericServiceFixtureTests.cs`
- `IoCTools.Testing.Tests/FluentValidationFixtureTests.cs`
- `IoCTools.Generator.Tests/TestFixtureDiagnosticsTests.cs`
- `IoCTools.Tools.Cli.Tests/*Test*`
- New deterministic CLI temp/sample projects under existing `IoCTools.Tools.Cli.Tests/TestProjects`.

Docs:

- `docs/testing.md`
- `docs/cli-reference.md`
- `docs/diagnostics.md`
- `README.md` if testing section remains.
- Existing sample test files or new sample fixtures.

## Work Breakdown

### 1. Baseline and current behavior capture

- [ ] Run the baseline verification commands listed in Preconditions.
- [ ] Record any pre-existing failures in the implementation notes.
- [ ] Add helper test utilities if needed to retrieve generated fixture source by hint name.
- [ ] Confirm current generated source for a minimal `[Cover<T>]` service before changing emitter behavior.

### 2. Structural fixture output tests

- [ ] Update `BasicServiceFixtureTests.cs` to assert exact generated members:
  - [ ] `protected readonly Mock<IUserRepository> _mockUserRepository = new();`
  - [ ] `CreateSut()`
  - [ ] typed setup helper.
- [ ] Update `InheritanceFixtureTests.cs` to assert base and derived dependencies appear in generated members and constructor argument order.
- [ ] Update `ConfigurationFixtureTests.cs` to assert configuration and options helper source.
- [ ] Update `GenericServiceFixtureTests.cs` to assert generic mock field/helper names and valid `CreateSut()` source.
- [ ] Keep `FluentValidationFixtureTests.cs` structural tests passing and add regression coverage if member planner changes affect helper names.
- [ ] Add a regression test that generated source does not emit deprecated injection examples or members.

### 3. Fixture member planner

- [ ] Introduce a planner model for constructor parameters:
  - [ ] dependency type.
  - [ ] parameter name.
  - [ ] generated field name.
  - [ ] setup helper name.
  - [ ] constructor argument expression.
  - [ ] special role: normal, logger, options, configuration, time, validator.
- [ ] Move naming from one-parameter helpers to whole-fixture planning so collisions are visible.
- [ ] Implement deterministic collision disambiguation.
- [ ] Add collision diagnostics or planner warnings for unreadable/ambiguous generated APIs.
- [ ] Update `FixtureEmitter` to emit from planner output.
- [ ] Verify existing generated shape remains compatible where names do not collide.

### 4. Logger profile support

- [ ] Preserve default Moq logger behavior for `ILogger<T>`.
- [ ] Add explicit logger profile setting for `NullLogger<T>`.
- [ ] Generate predictable constructor argument expression for each logger profile.
- [ ] Add source tests for both logger profiles.
- [ ] Ensure required namespaces/usings are emitted only when needed.

### 5. Options and configuration helpers

- [ ] Add options helper model for:
  - [ ] `IOptions<T>.Value`.
  - [ ] `IOptionsSnapshot<T>.Value`.
  - [ ] `IOptionsSnapshot<T>.Get(name)`.
  - [ ] `IOptionsMonitor<T>.CurrentValue`.
  - [ ] `IOptionsMonitor<T>.Get(name)`.
- [ ] Add `Use{OptionsName}(TOptions value)` helpers.
- [ ] Keep `Configure{OptionsName}(Action<TOptions>)` helpers.
- [ ] Harden `IConfiguration` helper behavior for indexer and typed lookup.
- [ ] Add structural tests for generated helper members and setup statements.

### 6. Clock/time abstraction hooks

- [ ] Add role detection for `TimeProvider`.
- [ ] Add profile metadata hook for named time abstractions such as downstream `IClock`.
- [ ] Keep default fallback as normal `Mock<T>`.
- [ ] Add tests proving no Delta-specific `TestClock` name is hard-coded.
- [ ] Add tests proving profile hook changes constructor argument expression predictably.

### 7. Diagnostics alignment

- [ ] Update `TDIAG-01` to report only manual mocks matching covered service dependencies.
- [ ] Update `TDIAG-02` to detect simple helper methods that construct the covered service, not only direct object creation in test bodies.
- [ ] Update `TDIAG-03` to handle ambiguity deterministically:
  - [ ] report only strong single matches.
  - [ ] skip or emit review diagnostic for multiple matches.
- [ ] Implement `TDIAG-04` emission for missing generated constructor info.
- [ ] Implement `TDIAG-05` emission for non-partial `[Cover<T>]` classes.
- [ ] Add fixture naming collision diagnostic.
- [ ] Add analyzer emission tests, not descriptor-only tests.
- [ ] Add codefix only for bounded cases:
  - [ ] add `partial` for `TDIAG-05`.
  - [ ] add `[Cover<T>]` for unambiguous `TDIAG-03`.

### 8. CLI scaffold

- [ ] Add parser support for `ioc-tools test scaffold`.
- [ ] Add options:
  - [ ] `--project`
  - [ ] `--test-project`
  - [ ] `--type`
  - [ ] `--framework`
  - [ ] `--mocking`
  - [ ] `--assertions`
  - [ ] `--output`
  - [ ] `--dry-run`
  - [ ] `--json`
  - [ ] `--force`
- [ ] Resolve service type from production project compilation.
- [ ] Refuse ambiguous type names unless fully qualified.
- [ ] Infer test namespace from test project conventions.
- [ ] Generate partial class with `[Cover<T>]`.
- [ ] Generate `CreateSut_ShouldConstruct` only.
- [ ] Prevent overwrite unless `--force`.
- [ ] Implement dry-run and JSON output.
- [ ] Add CLI tests with deterministic temp projects.
- [ ] Verify generated scaffold compiles in a sample test project.

### 9. Evidence CLI fixture mode

- [ ] Extend existing `ioc-tools evidence` with fixture migration mode.
- [ ] Add JSON model fields:
  - [ ] service type.
  - [ ] test class.
  - [ ] classification.
  - [ ] matched constructor dependencies.
  - [ ] manual mocks.
  - [ ] manual SUT construction.
  - [ ] logger/options/config/time patterns.
  - [ ] expected fixture members.
  - [ ] warnings.
- [ ] Detect safe migration, partial migration, semantic harness, unknown/manual review.
- [ ] Suppress aggressive suggestions for harness signals such as `WebApplicationFactory`, `Harness`, `InMemory`, `Lease`, `Observability`, scheduler/time lifecycle proof.
- [ ] Add CLI tests for text and JSON output.

### 10. Docs and samples

- [ ] Update `docs/testing.md`:
  - [ ] generated fixture member contract.
  - [ ] logger profiles.
  - [ ] options/config/time helpers.
  - [ ] scaffold workflow.
  - [ ] migration evidence workflow.
  - [ ] scope guard against semantic harness replacement.
- [ ] Update `docs/cli-reference.md` with `test scaffold` and fixture evidence options.
- [ ] Update `docs/diagnostics.md` with aligned TDIAG behavior and any new diagnostic IDs.
- [ ] Update README testing section if present.
- [ ] Update or add sample fixture files.
- [ ] Ensure new docs use canonical `[DependsOn]`, `[DependsOnConfiguration]`, and `[DependsOnOptions]` examples.

## Verification Commands

Run after relevant task groups and before completion:

- `dotnet test IoCTools.Testing.Tests`
- `dotnet test IoCTools.Generator.Tests --filter TestFixture`
- `dotnet test IoCTools.Tools.Cli.Tests --filter Test`
- `dotnet build IoCTools.Sample`
- `dotnet pack`

Additional CLI verification after scaffold work:

- `dotnet test IoCTools.Tools.Cli.Tests --filter Scaffold`
- `dotnet test IoCTools.Tools.Cli.Tests --filter Evidence`
- Run scaffold dry-run against deterministic test project and assert JSON shape.

## Exit Criteria

- All verification commands pass or failures are documented as unrelated pre-existing failures.
- Current `[Cover<T>]` users keep compiling.
- Generated fixture source has structural tests for mocks, logger, options, config, time hook, FluentValidation, inheritance, generics, and collision behavior.
- Diagnostics emit for common manual-boilerplate patterns and no longer over-report unrelated manual mocks.
- CLI scaffold has dry-run, JSON, force/overwrite protection, and ambiguity refusal.
- Evidence CLI classifies safe/partial/semantic/unknown migration candidates.
- Docs and README reflect vNext behavior and scope guard.
- No production project references Moq/test packages.
- No Delta or Keel files modified.
