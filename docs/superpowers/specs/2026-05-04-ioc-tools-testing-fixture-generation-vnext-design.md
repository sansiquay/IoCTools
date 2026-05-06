# IoCTools.Testing Fixture Generation vNext Design

Date: 2026-05-04

## Goal

Turn existing `IoCTools.Testing` fixture generation into a practical downstream boilerplate reduction system for Delta/Keel-style service tests while preserving readable behavior assertions and real semantic harnesses.

This is a testability/provability feature. It should make the correct unit-test shape easier than hand-maintained constructor/mocking boilerplate. It must not replace semantic harnesses, integration tests, scheduler/time proof, observability proof, or real-instance proof.

## Scope Guard

In scope:

- Generated fixture members for IoCTools-managed service constructor dependencies.
- CLI scaffold for partial test shells that opt into `[Cover<T>]`.
- CLI migration evidence for existing manual test setup.
- Diagnostics that tie manual boilerplate to the service dependency shape.
- Structural tests that assert generated source shape.
- Documentation and sample updates for downstream adoption.

Out of scope:

- Production DI generation rewrites.
- Runtime reflection fixture construction.
- Generated business assertions.
- Tests that only assert mocks were called unless that interaction is the behavior under test.
- Production projects referencing Moq or test packages.
- Global forced mocking framework without an explicit selected profile.
- Compatibility shims around deprecated injection markers. Canonical examples use `[DependsOn]`, `[DependsOnConfiguration]`, and `[DependsOnOptions]`.
- Delta/Keel adoption changes before IoCTools feature tests pass.

## Existing Capability Inventory

### Current fixture generation

Evidence from current implementation:

- `IoCTools.Testing/IoCTools.Testing/Generator/Pipeline/TestFixturePipeline.cs` accepts `partial` classes with `[Cover<T>]`, extracts the covered service symbol, and dedupes by test class.
- `IoCTools.Testing/IoCTools.Testing/CodeGeneration/FixtureEmitter.cs` emits:
  - partial test class augmentation.
  - `protected readonly Mock<T>` fields for constructor parameters.
  - `public TService CreateSut() => new(...)`.
  - typed setup helpers: `Setup{Dependency}(Action<Mock<T>> configure)`.
  - `ConfigureIConfiguration(Func<string, object?> valueProvider)` for `IConfiguration`.
  - `Configure{OptionsType}(Action<TOptions> configureOptions)` for options dependencies.
  - FluentValidation helpers when FluentValidation is referenced.
- `IoCTools.Testing/IoCTools.Testing/Analysis/ConstructorReader.cs` reads constructor parameters by preferring constructors marked with `GeneratedCodeAttribute`, otherwise falling back to the longest non-static constructor.
- `ConstructorReader` currently recognizes `IConfiguration`, `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>` by type name.
- `IoCTools.Testing/IoCTools.Testing/Utilities/TypeNameUtilities.cs` derives member names from type names, for example `IUserRepository` to `_mockUserRepository` and `SetupUserRepository`.

### Current diagnostics

Current descriptor inventory in `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/TestFixtureDiagnostics.cs`:

| ID | Severity | Intent | Current behavior |
| --- | --- | --- | --- |
| `TDIAG-01` | Info | Manual `Mock<T>` field duplicates generated fixture member | Analyzer emits for manual mock fields in a fixture-bearing test class. Current limitation: does not first verify that the mock type matches the covered service dependency set. |
| `TDIAG-02` | Info | Manual `new Service(...)` can use generated `CreateSut()` | Analyzer detects object creation matching covered service type in the test class source. Current limitation: only catches resolvable object creation syntax in the class. |
| `TDIAG-03` | Info | Test class could add `[Cover<T>]` | Analyzer builds a manual mock type set and suggests the first service whose constructor dependencies are all present. Current limitation: first-match behavior; no ambiguity report. |
| `TDIAG-04` | Error | `[Cover<T>]` service has no generated constructor | Descriptor and docs exist. Current implementation evidence did not find an emitting branch in `TestFixtureAnalyzer`. |
| `TDIAG-05` | Error | `[Cover<T>]` class is not partial | Descriptor and docs exist. Current implementation evidence did not find an emitting branch in `TestFixtureAnalyzer`. |

### Existing limitations

- Logger behavior is not profile-aware. `ILogger<T>` is currently treated like any other constructor dependency and receives `Mock<ILogger<T>>`; there is no predictable `NullLogger<T>` profile.
- Options helpers only set `.Value`. `IOptionsSnapshot<T>.Get(name)` and `IOptionsMonitor<T>.CurrentValue` / `.Get(name)` are not fully modeled.
- `IConfiguration` helper only sets string lookup shape. Typed values and indexed keys need a deterministic helper contract.
- Clock/time abstractions are not recognized. Delta uses `IClock` and `TestClock`; Keel uses time-driven tests. The fixture system should provide hooks, not hard-code downstream test clocks.
- Naming collision handling is implicit and unsafe. `TypeNameUtilities` can produce duplicate `_mock{Name}` members for distinct types with the same simple name or nested generic collisions.
- Existing smoke tests often assert only that generated trees are non-empty.
- Diagnostics are partly descriptor-only (`TDIAG-04`, `TDIAG-05`) and partly too broad (`TDIAG-01`).
- CLI has `evidence`, but no test fixture scaffold workflow.

### Docs and implementation agreement

Matches:

- `docs/testing.md` promises mock fields, `CreateSut()`, typed setup helpers, config helpers, and FluentValidation helpers; current `FixtureEmitter` implements those broad areas.
- `docs/cli-reference.md` documents a top-level `evidence` command; `IoCTools.Tools.Cli/Program.cs` and `CommandLineParser.cs` wire it.
- `CLAUDE.md` says IoCTools now targets test-code boilerplate: mock declarations and SUT construction. Current `IoCTools.Testing` aligns with that product frame.

Disagreements or nuance:

- Public docs and README mention `TDIAG-01` through `TDIAG-05`, but current analyzer evidence only found emission for `TDIAG-01`, `TDIAG-02`, and `TDIAG-03`.
- `docs/testing.md` describes some config behavior in attribute terms, while the fixture generator works from constructor parameter types after generation.
- Current docs imply practical boilerplate elimination, but test coverage does not prove the emitted member shape except in FluentValidation-specific tests.

## Downstream Boilerplate Inventory

### Delta evidence

Scanned `/Users/dev/Documents/projects/delta` recursively. Summary:

- C# files scanned: 2330.
- Files matching test-boilerplate signals: 278.
- Pattern totals: 498 manual `Mock<T>` fields, 2 `Substitute.For`, 2 direct `CreateSut() => new`, 379 `NullLogger<T>` uses, 1 `Options.Create(`, 195 `TestClock` uses.

Required representative tests:

| File | Evidence | Classification |
| --- | --- | --- |
| `Delta/tests/Delta.Application.Tests/Unit/BoundedContexts/Automations/Services/InputResolutionServiceTests.cs` | 544 lines, 14 tests, 3 `Mock<T>`, direct `CreateSut => new InputResolutionService(...)` with 4 args, 1 `NullLogger`, no options, no test clock. Behavior assertions remain clear. | Good target for generated fixture. Generated mocks/logger/SUT remove setup boilerplate while tests keep domain assertions. |
| `Delta/tests/Delta.Application.Tests/Unit/BoundedContexts/Workspaces/Services/DataPlaneAuthorityGateTests.cs` | 478 lines, 18 tests, 3 `Mock<T>`, no direct `CreateSut` pattern, 2 `NullLogger`, helper `CreateGate`, mocks for `IWorkspaceRepository`, `IAuthorityDenialLogSink`, `IClock`. | Good partial target. Generated mocks/logger/clock hook help, but semantic `CreateGate` or authorization-specific helpers may remain explicit. |

Additional high-boilerplate candidates:

| File | Evidence | Classification |
| --- | --- | --- |
| `Delta/tests/Delta.Application.Tests/Unit/BoundedContexts/Automations/Services/AutomationAccessServiceTests.cs` | 2586 lines, 63 tests, 12 mocks, 14 `NullLogger`, 14 `TestClock`; provider-backed setup with `CreateProvider` and `CreateSut`. | Good pilot target after core fixture hardening. Needs profile clock hook and careful preservation of provider/lock semantics. |
| `Delta/tests/Delta.Application.Tests/Unit/Commands/ApplyAutomationsManifestCommandHandlerTests.cs` | 1903 lines, 41 tests, 9 mocks, 23 `NullLogger`; helper `CreateHandler`. | Good target for generated mocks/logger/SUT; explicit manifest builders remain. |
| `Delta/tests/Delta.Application.Tests/Unit/BoundedContexts/Automations/IntegrationEventHandlers/AutomationLifecycleIntegrationEventHandlersTests.cs` | 581 lines, 14 tests, 13 mocks, 5 `NullLogger`; lifecycle-specific helpers remain. | Partial target. Fixture can remove dependency declarations; lifecycle setup helpers remain semantic. |

Delta patterns:

- Good target: service tests with constructor dependency mocks, logger wiring, simple `CreateSut`/`CreateHandler`, and domain builders that should remain explicit.
- Diagnostics/codefix target: classes with manual mocks matching constructor dependencies; classes with manual service construction matching a covered type.
- CLI scaffold target: new service tests where no manual test shell exists yet.
- Out of scope or explicit: complex provider harnesses until profile hooks exist; time-sensitive behavior where `TestClock` setup is the behavior; mock verification where interaction is the assertion.

### Keel evidence

Scanned `/Users/dev/Documents/projects/keel/tests` and `templates`.

High-level counts:

- 342 Keel test files scanned.
- 0 hits for `private readonly Mock<`.
- 0 hits for `CreateSut() => new`.
- Sparse simple boilerplate: `Substitute.For` in 3 files, `NullLogger<T>` in 1, `Options.Create(` in 16, `new TestClock` in 4.
- Heavy semantic signals: `InMemory` in 31 files, `Lease` in 13, `Observability` in 11, `Harness` in 4, `WebApplicationFactory` in 2, `TestHost` in 2.

Simple Keel candidates:

| File | Evidence | Classification |
| --- | --- | --- |
| `templates/keel-app/tests/KeelApp.Application.Tests/Greetings/CreateGreetingHandlerTests.cs` | 53 lines, 2 tests, 4 `Substitute.For`. | CLI scaffold only unless a NSubstitute profile is explicitly selected later. |
| `templates/keel-app/tests/KeelApp.Application.Tests/Greetings/GetGreetingHandlerTests.cs` | 51 lines, 2 tests, 3 `Substitute.For`. | CLI scaffold only. Good template adoption sample after Moq-only vNext proves out. |
| `templates/keel-app/tests/KeelApp.Domain.Tests/Greetings/GreetingTests.cs` | 35 lines, 2 tests, 2 `Substitute.For`. | Low-value candidate; likely leave explicit or scaffold-only. |

Explicit Keel exclusions:

- `tests/Keel.AspNetCore.Tests/EndpointIntegrationTests.cs`: `WebApplicationFactory`; full host integration.
- `tests/Keel.Infrastructure.Tests/Pipeline/AuthorityPlacementLeaseRenewalObservabilityTests.cs`: harness, in-memory store/catalog, observability assertions.
- `tests/Keel.Pipeline.Tests/ScheduledRequestSchedulerTests.cs`: time-driven scheduler behavior with `TestClock`.
- `tests/Keel.Pipeline.Tests/PostCommitRequestStagerLifecycleTests.cs`: temporal lifecycle behavior.
- `tests/Keel.Infrastructure.Tests/Pipeline/AuthorityPlacementLeaseRuntimeTests.cs`: lease tracking/handoff semantics.
- `tests/Keel.Infrastructure.Tests/Pipeline/InMemoryAuthorityPlacementStoreTests.cs`: in-memory store expiry/fence-token semantics.
- Scheduler integration tests under Hangfire/Quartz: external scheduler proof.

Keel adoption stance:

- Do not bulk-convert Keel.
- Pilot only template-style/simple handler tests, and only after explicit mocking-framework profile support or a Moq-compatible template decision.
- Main Keel corpus is semantic-heavy and should remain explicit.

### IoCTools test evidence

Weak/non-structural tests:

- `IoCTools.Testing.Tests/BasicServiceFixtureTests.cs`: smoke tests assert generated trees exist and no blocking diagnostics.
- `IoCTools.Testing.Tests/InheritanceFixtureTests.cs`: same smoke pattern.
- `IoCTools.Testing.Tests/ConfigurationFixtureTests.cs`: same smoke pattern.
- `IoCTools.Testing.Tests/GenericServiceFixtureTests.cs`: same smoke pattern.
- `IoCTools.Generator.Tests/TestFixtureDiagnosticsTests.cs`: descriptor metadata only; no analyzer emission coverage.

Stronger existing pattern:

- `IoCTools.Testing.Tests/FluentValidationFixtureTests.cs` already asserts generated fixture source text for helper names and validation setup. Use this style for all fixture shape tests.

## Classification Matrix

| Pattern | Class |
| --- | --- |
| Manual `Mock<T>` fields for constructor dependencies in service tests | Good target for fixture generation and diagnostics/codefix. |
| Manual `CreateSut() => new Service(...)` matching IoCTools constructor shape | Good target for fixture generation and diagnostics/codefix. |
| Logger-only constructor boilerplate | Good target when logger profile is explicit and predictable. |
| `IOptions<T>` / snapshot / monitor setup for simple options values | Good target for fixture generation. |
| `IConfiguration` key/value setup | Good target for fixture generation if typed lookup behavior is deterministic. |
| Domain builders such as manifests, lifecycle summaries, registry entries | Keep explicit semantic helpers. |
| Provider-backed harnesses that prove service-provider behavior | Partial target only; fixture can assist dependencies, but harness remains explicit. |
| Time-sensitive scheduler/lifecycle tests | Usually explicit semantic harness. Fixture may provide clock hook, not behavior automation. |
| Keel in-memory lease/authority/observability tests | Explicit semantic harness. |
| New test class shell for a known service | CLI scaffold. |
| Existing manual class that could add `[Cover<T>]` | Diagnostics/evidence/codefix. |
| Business assertions | Out of scope for generation. |
| Reflection-based runtime fixture construction | Out of scope. |

## Feature Area A - Stronger Generated Fixture Shape

### Requirements

Generated fixtures must support:

- Moq fields for constructor dependencies.
- `CreateSut()` using dependency objects.
- Typed setup helpers.
- Predictable logger handling.
- Full options helpers for `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>`.
- Consistent `IConfiguration` helper behavior for indexed keys and typed values.
- Profile-driven clock/time abstraction defaults.
- Existing FluentValidation helpers.
- Generic services and inherited constructor dependencies.
- Deterministic naming collision handling and diagnostics.

### Fixture member contract

Default Moq profile:

- For normal dependencies: `protected readonly Mock<TDependency> _mockDependency = new();`.
- For `ILogger<T>`:
  - Default vNext profile: `Mock<ILogger<T>>`, preserving current behavior.
  - Optional profile: `NullLogger<T>` member for teams that do not assert logs.
  - Output must be deterministic and structurally tested in both profiles.
- `CreateSut()` uses one helper per parameter so logger/options/config/time profiles can participate without special casing the constructor expression in many places.

Options helpers:

- `Use{OptionsName}(TOptions value)`.
- `Configure{OptionsName}(Action<TOptions> configure)`.
- For `IOptions<T>`: setup `.Value`.
- For `IOptionsSnapshot<T>`: setup `.Value` and `.Get(It.IsAny<string>())`.
- For `IOptionsMonitor<T>`: setup `.CurrentValue`, `.Get(It.IsAny<string>())`; change-token callbacks remain out of scope unless needed by a real downstream test.

Configuration helpers:

- Keep `ConfigureIConfiguration(Func<string, object?> valueProvider)`.
- Add deterministic handling for:
  - indexer `configuration[key]`.
  - `GetSection(key).Value`.
  - typed lookup patterns used by `GetValue<T>`.
- Prefer an internal generated dictionary-backed helper over loose mock callbacks when it yields more predictable behavior.

Clock/time abstraction:

- Recognize common abstractions by type:
  - `System.TimeProvider`.
  - `IClock` by fully qualified type if the project provides profile metadata.
  - other recognized testable time abstractions through profile config.
- Do not hard-code Delta `TestClock`.
- Provide a profile hook:
  - generated default dependency expression.
  - optional setup method name.
  - optional required using.
- If no profile matches, use normal `Mock<T>`.

Naming:

- Use a single fixture-member planner that sees all constructor dependencies before emitting.
- Detect duplicate field, setup helper, options helper, config helper, and FluentValidation helper names.
- Deterministic disambiguation rule: append stable suffix from namespace/type path, then numeric suffix only as a final fallback.
- Emit a diagnostic if disambiguation would make generated API ambiguous or unreadable.

Non-requirements:

- No generated assertions.
- No runtime reflection.
- No production Moq references.
- No global mocking framework choice beyond explicit profile.

## Feature Area B - Test Class Scaffold CLI

### Naming decision

Use `ioc-tools test scaffold`.

Reason:

- User-facing concept is test authoring, not production service inventory.
- Future `ioc-tools test evidence` can exist, but vNext should extend current top-level `evidence` first to avoid breaking existing CLI shape.
- Implementation can add a `test` top-level command with `scaffold` subcommand while keeping current top-level commands unchanged.

### Inputs

- `--project <production csproj>`
- `--test-project <test csproj>`
- `--type <fully-qualified service type>`
- `--framework xunit|nunit|mstest`
- `--mocking moq`
- `--assertions fluentassertions|shouldly|none`
- `--output <path>` optional
- `--dry-run`
- `--json`
- `--force`

### Behavior

- Resolve the production service type from the production project compilation.
- Refuse ambiguous service type names unless fully qualified.
- Infer test namespace from the test project root namespace, folder conventions, and output path.
- Generate or preview a partial test class with `[Cover<T>]`.
- Include one smoke test: `CreateSut_ShouldConstruct`.
- Do not generate business assertions.
- Do not overwrite existing output unless `--force`.
- Dry-run prints planned file content in text mode or structured result in JSON mode.
- JSON result includes:
  - service type.
  - test class name.
  - output path.
  - dependencies discovered.
  - expected fixture members.
  - warnings.

### Smoke test policy

Default xUnit smoke shape:

- Arrange: none, unless fixture profile requires minimal config/options values.
- Act: `var sut = CreateSut();`
- Assert: construction result is non-null using selected assertion package or `Assert.NotNull`.

This test is a compile/proof check for fixture wiring, not a substitute for behavior tests.

## Feature Area C - Migration/Evidence CLI

### Naming decision

Extend existing `ioc-tools evidence` for vNext. Add a focused mode rather than introducing a second evidence command immediately.

Proposed options:

- `ioc-tools evidence --project <test csproj> --test-fixtures`
- `ioc-tools evidence --project <test csproj> --test-fixtures --json`
- `ioc-tools evidence --project <test csproj> --test-fixtures --production-project <csproj>` if constructor shape needs production compilation context.

Future alias:

- `ioc-tools test evidence` can delegate to the same implementation after the `test` command group exists.

### Detection

Evidence should detect:

- Manual `Mock<T>` fields matching constructor dependencies.
- Manual `CreateSut() => new Service(...)` or helper methods returning `new Service(...)`.
- Manual logger/null logger wiring.
- `Options.Create(...)` patterns.
- Test classes that should add `[Cover<T>]`.
- Semantic harness signals that should suppress aggressive migration suggestions:
  - `WebApplicationFactory`.
  - `TestHost`.
  - `Harness`.
  - `InMemory`.
  - `Lease`.
  - `Observability`.
  - explicit scheduler/time lifecycle tests.

### Classification output

Every candidate class receives one of:

- `safe-migration`: constructor dependency mocks and simple SUT factory match a service shape.
- `partial-migration`: generated fixture can remove declarations but semantic helpers stay.
- `semantic-harness`: not a fixture target.
- `unknown-review`: ambiguous service type, multiple possible targets, unsupported mocking framework, or insufficient constructor shape.

JSON mode is required and should include evidence spans, matched dependencies, generated member expectations, warnings, and classification reason.

## Feature Area D - Diagnostics/Codefix Alignment

### Required diagnostic improvements

- `TDIAG-01`: only report manual mocks that duplicate generated fields for the covered service dependency set.
- `TDIAG-02`: detect manual `CreateSut`, `CreateHandler`, or helper-returning `new Service(...)` when it constructs the covered service.
- `TDIAG-03`: report missing `[Cover<T>]` when manual mock set strongly matches one service constructor. If multiple services match, report ambiguity or skip with evidence.
- `TDIAG-04`: implement emission when `[Cover<T>]` references a service without IoCTools-generated constructor info.
- `TDIAG-05`: implement emission when `[Cover<T>]` class is not partial.
- Add diagnostic for generated fixture member name collision.
- Add diagnostic for service dependency shape unavailable or unsupported.

### Codefix policy

Implement codefixes only where bounded:

- Add `partial` for `TDIAG-05`.
- Add `[Cover<T>]` for unambiguous `TDIAG-03`, including using directive if needed.
- Avoid auto-removing manual mocks or `CreateSut` in vNext unless tests prove the rewrite is safe.

Manual-removal recommendations can be surfaced through diagnostics and evidence JSON first.

## Feature Area E - Fixture Output Tests

Replace or augment smoke assertions with structural generated-source assertions.

Required fixture source assertions:

- Mock fields for constructor dependencies.
- `CreateSut()` argument order and dependency object shape.
- Typed setup helpers.
- Logger profile behavior.
- Options helpers for `IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`.
- Configuration helpers for indexers and typed values.
- FluentValidation success/failure helpers continue to emit.
- Inheritance dependencies appear in the generated `CreateSut()`.
- Generic service/dependency names are valid and deterministic.
- Auto-deps interaction, especially auto-detected `ILogger<T>`.
- Collision disambiguation and collision diagnostics.
- No deprecated injection examples or emitted members in new tests/docs except migration/deprecation context.

Use `FluentValidationFixtureTests.cs` as the model: assert generated source contains exact expected members and omits unsupported helpers.

## Feature Area F - Downstream Adoption Pilot

No Delta or Keel source changes in vNext implementation.

### Delta target list

| Candidate | Expected benefit | Keep explicit |
| --- | --- | --- |
| `InputResolutionServiceTests` | Delete about 27 setup lines, remove 3 mocks, generated logger/SUT. | Domain input builders and behavior assertions. |
| `DataPlaneAuthorityGateTests` | Delete about 20 setup lines, remove 3 mocks, generated logger/clock hook. | Authority setup helpers and denial/allow assertions. |
| `AutomationAccessServiceTests` | Large benefit after profile hooks: remove 12 mocks and repeated logger/clock setup; estimated 160 lines. | Provider-backed harness and lock/access semantics. |
| `ApplyAutomationsManifestCommandHandlerTests` | Remove 9 mocks and logger wiring; estimated medium/high deletion. | Manifest builders and command behavior assertions. |
| `AutomationLifecycleIntegrationEventHandlersTests` | Remove repeated handler dependency mocks; partial migration. | Lifecycle summaries, registry entries, event semantics. |

### Keel target list

Only consider template/simple handler tests after profile decision:

- `templates/keel-app/tests/KeelApp.Application.Tests/Greetings/CreateGreetingHandlerTests.cs`
- `templates/keel-app/tests/KeelApp.Application.Tests/Greetings/GetGreetingHandlerTests.cs`
- `templates/keel-app/tests/KeelApp.Domain.Tests/Greetings/GreetingTests.cs`

Explicitly exclude authority lease renewal, observability, lifecycle, in-memory store, real host, and scheduler integration tests.

## Incremental Deliverables

1. Structural fixture test hardening.
2. Fixture member planner and naming collision handling.
3. Logger/options/config/time profile support.
4. Diagnostics emission alignment.
5. CLI `test scaffold` dry-run/JSON/write behavior.
6. Evidence CLI fixture migration mode.
7. Docs and samples.
8. Downstream pilot report only; no Delta/Keel edits.

## Acceptance Criteria

- Design document exists with objective evidence from IoCTools, Delta, and Keel.
- Existing `IoCTools.Testing` behavior remains supported.
- CLI scaffold command supports dry-run and JSON modes.
- Generated scaffold compiles in a deterministic sample test project.
- Fixture output tests assert actual generated source shape.
- Diagnostics cover common manual-boilerplate patterns and descriptor-only diagnostics are implemented or documented as future work.
- Documentation updates include:
  - `docs/testing.md`
  - `docs/cli-reference.md`
  - `docs/diagnostics.md`
  - README testing section if still present.
  - sample file updates.
- No production project receives Moq.
- No runtime reflection fixture construction.
- No new canonical docs use deprecated injection markers except migration/deprecation context.
- Downstream pilot plan exists and adoption is deferred until IoCTools feature tests pass.
