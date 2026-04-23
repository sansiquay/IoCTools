# IoCTools Open Generic Registration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to execute this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the inconsistent open-generic registration story so IoCTools can intentionally support `services.Add*(typeof(IOpenGeneric<>), typeof(Impl<>))` patterns across generation, diagnostics, sample usage, CLI evidence, and docs.

**Architecture:** Treat this as a coherence phase, not a greenfield feature. The repo already understands generic service shapes in several generator tests, but the manual-registration diagnostics, sample, and product guidance still describe open generics as unsupported. The work should align analysis, code generation, diagnostics, sample scenarios, and documentation so one product truth exists.

**Tech Stack:** .NET 9 CLI/tests, Roslyn source generator analysis, Microsoft.Extensions.DependencyInjection registration code generation, xUnit, FluentAssertions, markdown docs.

---

### Task 1: Define the supported open-generic shape in tests before implementation

**Files:**
- Modify: `IoCTools.Generator.Tests/TypeOfRegistrationTests.cs`
- Modify: `IoCTools.Generator.Tests/DependsOnOnlyServiceRegistrationTests.cs`
- Modify: `IoCTools.Generator.Tests/AdvancedGenericEdgeCaseTests.cs`
- Modify: `IoCTools.Tools.Cli.Tests/CliEvidenceCommandTests.cs`

- [ ] Add failing generator tests for valid open-generic registrations covering scoped, singleton, and transient lifetimes.
- [ ] Add a failing diagnostics test proving valid `typeof(IOpenGeneric<>)` registrations do not emit IOC094 once fully supported.
- [ ] Add a CLI evidence test proving generated services and migration/evidence output surface the open-generic registration clearly.
- [ ] Run the targeted test slice and confirm the new expectations fail before implementation.

Run:

```bash
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter "FullyQualifiedName~TypeOfRegistrationTests|FullyQualifiedName~AdvancedGenericEdgeCaseTests|FullyQualifiedName~DependsOnOnlyServiceRegistrationTests" --logger "console;verbosity=minimal"
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~CliEvidenceCommandTests --logger "console;verbosity=minimal"
```

### Task 2: Implement open-generic generation and stop treating it as unsupported

**Files:**
- Modify: `IoCTools.Generator/IoCTools.Generator/Analysis/TypeAnalyzer.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator*.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Models/*Registration*.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/ManualRegistrationValidator.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs`

- [ ] Identify the current registration model path for open generic symbols and remove any assumption that a `typeof(...)` generic registration is automatically a missed IoCTools opportunity.
- [ ] Generate `services.AddScoped(typeof(IFoo<>), typeof(Foo<>))` style registrations when the discovered service model represents an open generic service mapping.
- [ ] Distinguish supported open-generic patterns from still-invalid patterns so diagnostics stay useful instead of disappearing entirely.
- [ ] Update IOC094 wording or severity posture so it only describes genuinely unsupported or redundant patterns after the new support lands.

### Task 3: Restore the disabled sample and document the product posture

**Files:**
- Modify: `IoCTools.Sample/Program.cs`
- Modify: `README.md`
- Modify: `docs/attributes.md`
- Modify: `docs/diagnostics.md`
- Modify: `docs/cli-reference.md`

- [ ] Re-enable the generic repository sample scenario currently called out as disabled.
- [ ] Add one concise doc example showing the intended open-generic attribute pattern and the resulting registration shape.
- [ ] Update any docs that still imply open generics are broadly unsupported.
- [ ] Ensure all new guidance continues the `DependsOn`-first, never-`Inject` posture from `1.5.x`.

### Task 4: Prove the feature across generator, CLI, and sample integration

**Files:**
- Verify existing and newly added tests

- [ ] Run the targeted generator and CLI suites again after implementation.
- [ ] Run the full solution test suite once targeted tests pass.
- [ ] Build or test the sample path if there is a stable sample verification command in the repo.
- [ ] Update release notes/changelog if the feature changes the public `1.5.x` story materially enough to warrant mention.

Run:

```bash
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --logger "console;verbosity=minimal"
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --logger "console;verbosity=minimal"
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.sln --logger "console;verbosity=minimal" -m:1
```

## Acceptance Criteria

- valid open-generic service mappings generate the correct DI registrations
- manual-registration diagnostics no longer label supported open-generic registrations as unsupported
- the sample no longer documents the generic repository scenario as disabled
- docs and CLI evidence present one consistent product story for open generics
