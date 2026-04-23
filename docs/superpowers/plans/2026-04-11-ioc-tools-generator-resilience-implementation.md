# IoCTools Generator Resilience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to execute this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the remaining silent-failure and degraded-output paths in the generator so IoCTools does not quietly lose registrations, interfaces, or constructor state when analysis hits exceptional conditions.

**Architecture:** The generator should preserve correctness over convenience. Where current code catches and suppresses failures, replace silent degradation with one of two intentional behaviors: either emit a visible diagnostic and stop generating the affected output, or rethrow into the existing diagnostic/reporting path so the failure is observable. Focus on the still-live fragile points rather than code paths already hardened in the recent `1.5.0` work.

**Tech Stack:** Roslyn incremental generators, diagnostic descriptors and reporting helpers, xUnit, FluentAssertions, netstandard2.0-compatible generator code.

---

### Task 1: Lock in the bad current behavior with focused failure-path tests

**Files:**
- Modify: `IoCTools.Generator.Tests/GeneratorStabilityTests.cs`
- Modify: `IoCTools.Generator.Tests/GeneratorStabilityImprovements.cs`
- Modify: `IoCTools.Generator.Tests/ConfigurationInjectionDiagnosticsTests.cs`

- [ ] Add a failing test for `InterfaceDiscovery`-driven interface loss so an exception no longer results in an empty success path.
- [ ] Add a failing test for constructor generation when symbol binding degrades and the generator currently keeps going with incomplete state.
- [ ] Prefer narrow regression tests that assert a visible diagnostic, error result, or explicit failure signal rather than text snapshots only.
- [ ] Run the targeted stability tests and confirm the failure-path expectations are red before implementation.

Run:

```bash
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter "FullyQualifiedName~GeneratorStability|FullyQualifiedName~ConfigurationInjectionDiagnosticsTests" --logger "console;verbosity=minimal"
```

### Task 2: Make `InterfaceDiscovery` observable instead of silently empty

**Files:**
- Modify: `IoCTools.Generator/IoCTools.Generator/Utilities/InterfaceDiscovery.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticsRunner.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/*`

- [ ] Remove the current "return empty list" resilience behavior when interface analysis throws.
- [ ] Route the failure through an existing internal reporting path or add a narrowly scoped diagnostic descriptor if no suitable one exists.
- [ ] Simplify the duplicate interface traversal while touching this file so resilience work does not preserve unnecessary double-walk logic.
- [ ] Ensure downstream registration logic can distinguish "no interfaces exist" from "interface discovery failed".

### Task 3: Harden constructor-generation degraded paths

**Files:**
- Modify: `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorGenerator.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Generator/ConstructorEmitter.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Diagnostics/DiagnosticUtilities.cs`

- [ ] Replace null-symbol continuation with an intentional failure mode that is visible to the user.
- [ ] If symbol resolution fails, stop generating the affected constructor rather than producing partial output with missing configuration or dependency state.
- [ ] Keep the implementation netstandard2.0-safe and consistent with the generator's existing exception/reporting model.
- [ ] Re-check neighboring constructor generation catch/fallback paths to ensure this phase does not leave another silent variant behind.

### Task 4: Re-run broad generator validation and record the new posture

**Files:**
- Modify: `docs/diagnostics.md`
- Modify: `CHANGELOG.md`

- [ ] Document any new or newly exposed diagnostic behavior caused by resilience hardening.
- [ ] Run the full generator suite after targeted tests pass.
- [ ] Run the full solution suite to catch downstream changes in CLI or FluentValidation behavior.
- [ ] Confirm `git diff --check` remains clean.

Run:

```bash
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --logger "console;verbosity=minimal"
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.sln --logger "console;verbosity=minimal" -m:1
git diff --check
```

## Acceptance Criteria

- interface-discovery failures are observable and no longer degrade silently to zero interfaces
- constructor-generation symbol failures do not produce quietly incomplete generated output
- resilience behavior is backed by regression tests
- user-facing diagnostics/docs explain the new failure posture where needed
