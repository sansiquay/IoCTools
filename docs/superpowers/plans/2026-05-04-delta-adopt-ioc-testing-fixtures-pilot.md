# Delta IoCTools.Testing Fixture Adoption Pilot Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pilot IoCTools.Testing generated fixtures in Delta after vNext fixture generation passes IoCTools feature tests.

**Architecture:** Defer all Delta edits until IoCTools.Testing vNext is released or locally referenced. Convert only constructor/mock/logger/time setup that the generated fixture proves structurally. Keep domain builders and behavior assertions explicit.

**Tech Stack:** Delta xUnit tests, Moq, IoCTools.Testing `[Cover<T>]`, Delta service-test conventions.

---

## Scope Guard

- Do not modify Delta during IoCTools vNext implementation.
- Do not convert semantic harnesses, provider proof, scheduler proof, authority lease proof, or integration tests.
- Do not replace business assertions.
- Do not generate mock-call assertions unless interaction is the behavior.
- Do not hard-code Delta `TestClock` into IoCTools. Delta time support must come through a profile hook.

## Preconditions

- IoCTools vNext fixture generation tests pass:
  - `dotnet test IoCTools.Testing.Tests`
  - `dotnet test IoCTools.Generator.Tests --filter TestFixture`
  - `dotnet test IoCTools.Tools.Cli.Tests --filter Test`
- Delta test project can reference the vNext IoCTools.Testing package or local package.
- Fixture evidence CLI can classify safe/partial/semantic candidates.
- Generated fixture output for each pilot service is structurally verified before deleting manual setup.

## Pilot Candidates

| Candidate | Current evidence | Expected deletion | Keep explicit | Pilot class |
| --- | --- | --- | --- | --- |
| `Delta/tests/Delta.Application.Tests/Unit/BoundedContexts/Automations/Services/InputResolutionServiceTests.cs` | 544 lines, 14 tests, 3 mocks, direct `CreateSut => new InputResolutionService(...)` with 4 args, 1 `NullLogger`. | About 27 lines, 3 mocks, manual logger/SUT wiring. | Input builders, store/env/secrets behavior assertions. | Safe migration. |
| `Delta/tests/Delta.Application.Tests/Unit/BoundedContexts/Workspaces/Services/DataPlaneAuthorityGateTests.cs` | 478 lines, 18 tests, 3 mocks, helper `CreateGate`, 2 `NullLogger`, `IClock`. | About 20 lines, 3 mocks, logger/clock setup. | Authority setup helpers, denial/allow assertions, exception shape. | Partial migration. |
| `Delta/tests/Delta.Application.Tests/Unit/BoundedContexts/Automations/Services/AutomationAccessServiceTests.cs` | 2586 lines, 63 tests, 12 mocks, 14 `NullLogger`, 14 `TestClock`, provider-backed `CreateProvider`/`CreateSut`. | About 160 lines if profile hooks are mature. | Provider/lock harness, access semantics, time-sensitive behavior. | Later partial migration. |
| `Delta/tests/Delta.Application.Tests/Unit/Commands/ApplyAutomationsManifestCommandHandlerTests.cs` | 1903 lines, 41 tests, 9 mocks, 23 `NullLogger`, helper `CreateHandler`. | Medium/high setup deletion. | Manifest builders and command semantics. | Partial migration. |
| `Delta/tests/Delta.Application.Tests/Unit/BoundedContexts/Automations/IntegrationEventHandlers/AutomationLifecycleIntegrationEventHandlersTests.cs` | 581 lines, 14 tests, 13 mocks, 5 `NullLogger`, lifecycle helpers. | Remove repeated handler dependency declarations. | Lifecycle summaries, registry entries, event semantics. | Partial migration. |

## Deferred/Excluded Delta Patterns

- Tests where `TestClock` manipulation is the behavior under test.
- Provider-backed tests until generated fixture profile hooks are proven.
- Runtime adapter tests that are mostly logger-heavy and may not benefit from full fixture adoption:
  - `Delta/tests/Delta.Infrastructure.Tests/Unit/Services/Automations/NodeNpxAutomationRuntimeAdapterTests.cs`
  - `Delta/tests/Delta.Infrastructure.Tests/Unit/Services/Automations/PythonUvAutomationRuntimeAdapterTests.cs`
- Any test whose value is real process, file-system, host, or runtime interaction proof.

## Adoption Steps

- [ ] Run `ioc-tools evidence --test-fixtures --json` on Delta test project.
- [ ] Confirm the five pilot candidates classify as safe or partial migration.
- [ ] For `InputResolutionServiceTests`, scaffold/convert first:
  - [ ] Add `partial` and `[Cover<InputResolutionService>]`.
  - [ ] Remove duplicate mock fields only after generated member names are confirmed.
  - [ ] Replace manual `CreateSut` with generated `CreateSut`.
  - [ ] Keep input/domain builders and all behavior assertions.
  - [ ] Run the file's test class.
- [ ] For `DataPlaneAuthorityGateTests`, convert only setup declarations first:
  - [ ] Add `partial` and `[Cover<DataPlaneAuthorityGate>]`.
  - [ ] Preserve `CreateGate` if it encodes authority semantics beyond constructor wiring.
  - [ ] Use generated clock/profile hook only if explicit behavior remains clear.
- [ ] Stop and evaluate readability before high-boilerplate candidates.
- [ ] Convert `ApplyAutomationsManifestCommandHandlerTests` only if handler fixture shape is clear and manifest builders remain explicit.
- [ ] Convert `AutomationLifecycleIntegrationEventHandlersTests` only as partial migration.
- [ ] Defer `AutomationAccessServiceTests` until provider-backed fixture use is proven.

## Verification

Delta-side commands to run during adoption:

- Targeted test class command for each converted file.
- Full affected test project command after each candidate.
- Fixture evidence command before and after conversion, comparing lines/mocks removed.

IoCTools-side guard before adoption:

- `dotnet test IoCTools.Testing.Tests`
- `dotnet test IoCTools.Generator.Tests --filter TestFixture`
- `dotnet test IoCTools.Tools.Cli.Tests --filter Test`

## Exit Criteria

- At least one safe Delta candidate uses `[Cover<T>]` and generated fixtures.
- Deleted lines are constructor/mock/logger setup, not behavior assertions.
- Behavior assertions remain readable.
- No semantic harness is converted.
- Evidence output before/after shows reduced manual mocks/SUT construction.
- Delta test results match pre-conversion behavior.
