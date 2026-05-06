# IoCTools.Testing Fixture Fat-Cut Refinements Plan

## Goal

Make generated service-test fixtures reduce more setup boilerplate while keeping arrange blocks explicit and readable. The feature should make the common test shape:

1. Configure constructor dependency behavior.
2. Access a ready SUT.
3. Assert business behavior.

The generated fixture must not hide semantic harness logic, integration setup, or business assertions.

## Scope Guard

- Do not rewrite production DI generation.
- Do not introduce runtime reflection fixture construction.
- Do not generate domain-specific helpers.
- Do not auto-generate business assertions.
- Do not remove `CreateSut()`.
- Do not force Moq into production projects.
- Do not add new `[Inject]` examples.
- Do not convert Delta or Keel tests in this phase.

## Preconditions

- Existing IoCTools.Testing vNext fixture generation remains green.
- Existing `[Cover<T>]` behavior remains supported.
- CLI scaffold/evidence tests from fixture vNext remain green.
- Work happens in IoCTools only.

## File Map

- `IoCTools.Testing/IoCTools.Testing/CodeGeneration/FixtureEmitter.cs`
- `IoCTools.Testing/IoCTools.Testing/Analysis/FixtureMemberPlanner.cs`
- `IoCTools.Testing.Tests/*FixtureTests.cs`
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Validators/TestFixtureAnalyzer.cs`
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/TestFixtureDiagnostics.cs`
- `IoCTools.Generator.Tests/TestFixtureDiagnosticsTests.cs`
- `IoCTools.Tools.Cli/TestScaffoldRunner.cs`
- `IoCTools.Tools.Cli.Tests/TestScaffoldCommandTests.cs`
- `docs/testing.md`
- `docs/cli-reference.md`
- `docs/diagnostics.md`

## Design

### 0. Analyzer Scope Follow-Up

IoCTools analyzers must respect project type:

- Test-project diagnostics: `TDIAG01`-`TDIAG08`.
- Production-project diagnostics: DI graph completeness, manual registration, lifetime policy, style, redundancy, auto-deps profile, and opportunity rules.
- Both: code-generation correctness and malformed attribute rules that can break generated code in any project.

Test projects are allowed to define fakes, partial harnesses, manual registrations, and incomplete DI graphs. They should see generated-fixture guidance without production-composition noise.

### 1. Lazy SUT Property

Generate:

```csharp
private ServiceType? _sut;

protected ServiceType Sut => _sut ??= CreateSut();
```

Rules:

- `CreateSut()` remains public and unchanged for explicit multi-instance tests.
- `Sut` is protected, get-only, lazy, and cached per test class instance.
- `Sut` is generated after fields and before `CreateSut()` unless existing style strongly prefers another order.
- Tests configure generated helpers before first `Sut` access.
- Generic and inherited services must still compile.

### 2. Mock Accessors Instead Of Field-First API

For normal mock dependencies, prefer readable accessors:

```csharp
protected Mock<IDependency> DependencyMock { get; } = new();

protected void SetupDependency(Action<Mock<IDependency>> configure) =>
    configure(DependencyMock);
```

Rules:

- Avoid public/protected underscore fields in new output if compatibility can be preserved safely.
- If changing field shape is too breaking, keep existing fields and add accessors as the new preferred API.
- `CreateSut()` should use the generated member deterministically.
- Naming collisions must remain deterministic and diagnosed.

### 3. Config/Options/Time Provider Helpers

Configuration:

```csharp
protected void ConfigureConfiguration(params (string Key, object? Value)[] values)
protected void ConfigureConfiguration(Func<string, object?> valueProvider)
```

Rules:

- Indexed access and `GetValue<string>(key)` must both resolve through the same provider.
- Tuple overload is convenience; function overload is extensibility.

Options:

```csharp
protected TOptions UseFooOptions(TOptions value)
protected TOptions ConfigureFooOptions(Action<TOptions> configure)
protected TOptions ConfigureFooOptions(string name, Action<TOptions> configure) // snapshots only
```

Rules:

- Helpers return configured options instance.
- `IOptions<T>` and `IOptionsSnapshot<T>` wire `.Value`.
- `IOptionsSnapshot<T>` named overload wires `.Get(name)`.
- `IOptionsMonitor<T>` wires `.CurrentValue` and `.Get(It.IsAny<string>())`.

Time:

```csharp
protected TimeProvider TimeProvider { get; private set; } = TimeProvider.System;
protected void UseTimeProvider(TimeProvider timeProvider)
```

Rules:

- Only for `TimeProvider` constructor dependencies in this phase.
- Do not hard-code Delta-specific clocks.
- Keep `IClock` and other abstractions profile-driven future work unless existing vNext already has a profile hook.

### 4. Logger Defaults

- CLI scaffold should default service-test shells to the `NullLogger<T>` profile when the test does not request logger verification.
- Moq logger profile remains available and predictable.
- Generated fixture output tests must assert both profiles.

### 5. Setup-After-SUT Diagnostic

Add analyzer coverage for obvious same-method misuse:

```csharp
var sut = Sut;
ConfigureFooOptions(...); // diagnostic
SetupRepository(...);     // diagnostic
```

Rules:

- Only diagnose generated fixture helper calls after `Sut` has been accessed earlier in the same method body.
- Do not attempt interprocedural analysis.
- Keep severity warning/info consistent with existing TDIAG style.
- If analyzer access to generated helper names is too costly, implement only `Sut` then known prefixes: `Setup*`, `Configure*`, `Use*`.

### 6. Cover<T> Opportunity Diagnostic

Add analyzer coverage that finds test classes manually constructing IoCTools-owned service types that could use `[Cover<T>]`.

Target examples:

```csharp
private ServiceType CreateSut() => new(...);

[Fact]
public void Test()
{
    var sut = new ServiceType(...);
}
```

Rules:

- Only suggest `[Cover<T>]` when the constructed type is an IoCTools-managed service type with source-generator constructor metadata available.
- Prefer warning severity so downstream users see actionable migration opportunities.
- Diagnostic message should name the service type and explain that generated fixtures can replace manual constructor/mock boilerplate.
- Avoid diagnostics for semantic harnesses where the test class already uses real fakes/harness infrastructure and the target type is not a simple IoCTools service construction.
- Do not diagnose if the class already has `[Cover<T>]`.
- Do not require exact mocking-framework detection for v1; construction of an eligible service in a test class is enough if metadata proves IoCTools ownership.
- Code fix is optional. Design it as adding `[Cover<ServiceType>]` and `partial`, but implement only if the existing codefix path is already cheap.
- JSON/evidence CLI can share classification language, but analyzer must work independently.

### 7. Tests And Docs

Fixture output tests must assert exact generated source fragments for:

- `Sut` lazy property.
- Mock accessor and setup helper.
- `ConfigureConfiguration` tuple and function overloads.
- Options helpers returning values.
- Snapshot named options helper.
- Monitor options helper.
- `UseTimeProvider`.
- NullLogger and Moq logger profiles.
- FluentValidation helpers still generated.
- Generic and inherited constructor dependencies still supported.
- No `[Inject]` examples in new docs except migration/deprecation context.

Docs must show the slim test shape:

```csharp
[Cover<InputResolutionService>]
public sealed partial class InputResolutionServiceTests
{
    [Fact]
    public async Task ResolveAsync_StoreInput_ResolvesFromRepository()
    {
        SetupStoreItemRepository(mock => mock
            .Setup(x => x.GetByPatternAsync("config.*", It.IsAny<int>(), "test-ns", null))
            .ReturnsAsync(storeItems));

        var result = await Sut.ResolveAsync(inputs, automationId, "test-ns");

        result.Should().ContainKey("config.apiKey");
    }
}
```

## Checkbox Tasks

- [x] Add failing fixture output tests for `Sut`, accessor helpers, config/options/time helpers, and logger profile defaults.
- [x] Add failing CLI scaffold tests for `Sut`-first example style and default NullLogger profile.
- [x] Add failing analyzer tests for setup-after-`Sut` access.
- [x] Add failing analyzer tests for manual construction of IoCTools-owned services that should use `[Cover<T>]`.
- [x] Add analyzer scope tests for production-only suppression in test projects.
- [x] Add analyzer scope tests proving TDIAG diagnostics are test-project only.
- [x] Update `FixtureEmitter` to emit lazy `Sut`.
- [x] Add mock accessors while preserving `CreateSut()` compatibility.
- [x] Add tuple configuration helper.
- [x] Change options helpers to return configured options instances.
- [x] Add `UseTimeProvider` for `TimeProvider` dependencies.
- [x] Update scaffold output and JSON warnings if logger profile defaults changed.
- [x] Add or update TDIAG descriptor/analyzer for setup-after-`Sut`.
- [x] Add or update TDIAG descriptor/analyzer for `[Cover<T>]` opportunities in manual service tests.
- [x] Categorize IoCTools analyzer diagnostics by production/test/both scope.
- [x] Suppress production-only validators in test projects.
- [x] Update docs: `docs/testing.md`, `docs/cli-reference.md`, `docs/diagnostics.md`.
- [x] Run focused tests.
- [x] Run full requested verification set.
- [x] Run 5.5 xhigh review and fix any concrete gaps.

## Verification Commands

```bash
dotnet test IoCTools.Testing.Tests -c Debug
dotnet test IoCTools.Generator.Tests -c Debug --filter TestFixture
dotnet test IoCTools.Tools.Cli.Tests -c Debug --filter "Scaffold|Evidence|Test"
dotnet build IoCTools.Sample -c Debug
dotnet pack
```

## Exit Criteria

- Existing fixture generation behavior remains supported.
- New fixtures allow normal tests to use `Sut` without explicit `CreateSut()` calls.
- Arrange-time helpers stay explicit and readable.
- Generated source tests assert actual emitted member shape.
- CLI scaffold emits slim test class shells that compile.
- Diagnostics catch obvious setup-after-`Sut` misuse.
- Diagnostics warn when an eligible IoCTools-owned service test manually constructs a service and could use `[Cover<T>]`.
- Docs show `Sut` as the preferred test path and `CreateSut()` as explicit escape hatch.
- No production project references Moq.
- Delta/Keel adoption remains deferred.
