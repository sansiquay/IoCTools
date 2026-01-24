# IoCTools ↔ Delta Fix Carry‑Over Prompt

## Objective
Stabilize IoCTools analyzer rules and their behavior inside the neighbor repo **delta** (project reference to IoCTools.Generator). Remove false positives, especially around implicit lifetimes and cross-assembly dependencies, and get Delta building cleanly. Preserve analyzer correctness and add missing diagnostics where needed.

## Current Status
- IoCTools repo has local changes (not committed) touching generator rules, redundant/hosted lifetime handling, Mediator skips, and new tests. README updated for implicit lifetime behavior.
- Delta build currently fails on a real code error (HttpRequestRepository expects `DbSet<HttpRequestRecord>` that is missing in `DeltaDbContext`). Analyzer warnings in Delta spiked (IOC001 noise) after cross-assembly logic change.
- Previously Delta sat at ~6 warnings: IOC070 on two hosted processors with extra interfaces, IOC039 unused config fields, CA2024 x2. Goal is to return to this ballpark with correct diagnostics.

## Key Decisions/Behavior (must keep)
- **Implicit lifetime**: No IOC069/070/071 when lifetime is implicit default. Explicit `[Scoped]` is redundant with implicit default (IOC033). Default implicit lifetime is likely scoped; configurable by user.
- **Hosted services**: Pure hosted service ⇒ implicit lifetime only; explicit lifetime is redundant (IOC072). Hosted service + extra interfaces must specify lifetime (IOC070). Hosted services should not be forced to add lifetime otherwise.
- **Mediator handlers**: Skip analyzer diagnostics for types named as Mediator handlers (Mediator, not MediatR). Skip configured in `TypeSkipEvaluator` + `GeneratorStyleOptions`.
- **Framework deps**: `ILogger`, `IConfiguration`, options wrappers etc. must NOT trigger IOC001 missing-registration.
- **Cross-assembly skip**: Avoid IOC001/IOC002 when a dependency’s implementation lives in another assembly (clean architecture: Application doesn’t reference Infrastructure). Restore prior “skip cross-assembly dependencies” behavior to eliminate noise.
- **Severity expectations**: Missing dependency / circular dependency should be errors (not warnings). Manual registration that duplicates IoC Tools should be error-level. Info-level analyzers should be elevated to warning in Delta via global config if needed, but IoCTools should ship error severities for the three critical rules (missing deps, circular deps, manual registration clash) and drop redundant editorconfig entries.
- **Manual registration detection**: Add analyzer to detect manual service registration when IoCTools already handles it; treat as error. Double registration is breaking.

## Files Touched (IoCTools)
- `IoCTools.Generator/Generator/DiagnosticRules.cs` — cross-assembly logic (restore skip to reduce IOC001 noise).
- `IoCTools.Generator/Generator/Validation/RedundantConfigurationValidator.cs` — implicit/hosted lifetime suppression logic.
- `IoCTools.Generator/Generator/DiagnosticDescriptors.cs` — IOC072 wording (redundant lifetime for hosted).
- `IoCTools.Generator/Generator/TypeSkipEvaluator.cs` and `GeneratorStyleOptions.cs` — Mediator handler skips.
- Tests: `IoCTools.Generator.Tests/.../HostedServiceLifetimeTests.cs`, `MissingLifetimeSuggestionTests.cs`, `FrameworkDependencySkipTests.cs`.
- `README.md` — implicit lifetime is default; explicit `[Scoped]` redundant.

## Outstanding Work
1) **Delta build error**: Fix `HttpRequestRepository` expecting `DbSet<HttpRequestRecord>`; add DbSet or adjust repository to existing set in `DeltaDbContext` (path likely `../delta/src/Delta.Infrastructure/Data/DeltaDbContext.cs` and `HttpRequestRepository.cs`).
2) **Cross-assembly IOC001 noise**: Reintroduce/ensure skip when dependency assembly differs from consumer. Validate Delta warnings drop back to ~6.
3) **Severity changes**: Ship IoCTools with missing dep, circular dep, and manual-registration clash as **errors** by default; remove redundant editorconfig entries. In Delta, elevate info→warning globally if needed (prefer MSBuild property or ruleset) without forcing editorconfig churn.
4) **Manual reg analyzer**: If not present, add detection of manual service registration when IoCTools already registers; severity = error. Add tests.
5) **Hosted service redundancy**: Ensure IOC072 covers explicit lifetime on pure hosted; hosted+interfaces needs lifetime (IOC070). Keep tests green.
6) **Mediator skip / framework dep skip**: Confirm IOC001 not raised for Mediator handlers or framework abstractions.
7) **Investigate remaining ~6 warnings in Delta**: Verify each is valid; no false positives. Use Delta as smell test.

## Useful Commands
- IoCTools unit tests (targeted):
  - `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj -c Debug --filter "HostedServiceLifetimeTests|MissingLifetimeSuggestionTests|FrameworkDependencySkipTests"`
- Full IoCTools generator build: `dotnet build IoCTools.Generator/IoCTools.Generator.csproj -c Debug`
- Delta build (warnings not errors): `dotnet build ../delta/Delta/Delta.sln /p:TreatWarningsAsErrors=false /clp:Summary /nologo`
- Warning grep in Delta: `dotnet build ... /bl` then inspect, or `rg "IOC" -g"*.log" ../delta`

## Pitfalls / Gotchas
- Do NOT reintroduce implicit-lifetime warnings (IOC069/070/071) when default applies.
- Hosted services: explicit lifetime should warn as redundant; do not require lifetime for pure hosted. Hosted + extra interface still needs lifetime.
- Cross-assembly skip is crucial; otherwise huge IOC001 blast. Earlier change broke this.
- Mediator handler names: ensure skip uses “Mediator” not “MediatR”.
- Framework DI abstractions must be skipped for missing-registration diagnostics.
- Avoid editorconfig requirement for users; change analyzer defaults instead.
- Maintain ASCII, 4-space indent, file-scoped namespaces.

## Key Domain Facts to Remember
- Implicit default lifetime (likely scoped) — explicit `[Scoped]` redundant; default should not warn.
- Hosted services are added via special IoCTools path; lifetime attribute unnecessary and redundant.
- Delta references IoCTools.Generator directly; analyzer changes reflected immediately.
- Desired steady-state Delta warnings: ~6 (2×IOC070 hosted+interfaces, 2×CA2024, 2×IOC039 unused config).

## Next Steps for Agent
1) Open `DiagnosticRules.cs` and restore cross-assembly skip logic to silence IOC001 for external implementations.
2) Verify `RedundantConfigurationValidator` and `DiagnosticDescriptors` still align with hosted/implicit decisions; adjust if drift.
3) Add/verify manual registration clash analyzer at error severity with tests.
4) Fix Delta DbContext / HttpRequestRepository mismatch and rebuild Delta with warnings as errors off; confirm warning count and validity.
5) Run targeted IoCTools tests; if stable, run full suite. Keep README aligned.
6) If info→warning override needed for Delta, consider MSBuild property/Directory.Build.props; avoid editorconfig edits.

Keep diffs lean; avoid feature flags; remove dead code; use apply_patch for edits.
