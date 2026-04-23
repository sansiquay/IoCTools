# IoCTools Planning Reset And Next Work Design

Date: 2026-04-11
Status: Approved design checkpoint
Scope: Preserve the useful forward work from the legacy repo-root `.planning/` folder as living `docs/superpowers` artifacts, then remove the old planning tree entirely.

## Purpose

The repo-root `.planning/` tree accumulated a mix of genuinely useful architectural notes, stale milestone history, and GSD process exhaust.

The right reset is:

1. keep the good ideas
2. rewrite them as current superpowers docs rooted in the live codebase
3. delete the legacy planning folder so the repo has one clear planning surface

## Design Goals

- replace the repo-root `.planning/` folder with a smaller living set of `docs/superpowers` docs
- preserve only ideas that still map to real gaps in the source tree
- avoid carrying stale milestone history or outdated status text forward
- make each preserved idea executable through a focused implementation plan

## Non-Goals

- preserve historical GSD workflow artifacts
- migrate every old planning note verbatim
- keep `.planning/` as an archive folder inside the repo

## Preserved Work

The planning reset keeps three ideas because they still map to live code and product value:

1. close the open-generic registration gap across diagnostics, generation, sample usage, and docs
2. remove remaining silent-failure generator paths and make resilience behavior observable
3. clean package metadata and documentation ownership so current docs reflect the shipped repo state

## Evidence For Preservation

### Open Generic Registration Still Has A Product Gap

- `IoCTools.Sample/Program.cs` still documents the generic repository scenario as disabled
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs` still exposes IOC094 as a not-supported warning
- `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/ManualRegistrationValidator.cs` still short-circuits on `typeof(...)` open-generic registrations

The repo already has partial open-generic handling in tests and supporting analysis code, so the real need is a consolidation phase that makes the feature coherent end-to-end.

### Generator Resilience Still Has Silent Degradation Paths

- `IoCTools.Generator/IoCTools.Generator/Utilities/InterfaceDiscovery.cs` still catches exceptions and returns empty results
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorGenerator.cs` still has degraded fallback behavior when symbol binding fails

The generator should fail loudly enough to protect DI truth, not silently emit incomplete registrations or constructor state.

### Metadata And Doc Ownership Are Still Split

- `IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj` still points package metadata at the old repository owner
- the removed `.planning/PROJECT.md` had stale package targets, stale tech debt, and stale milestone state

The repo needs one current planning/documentation surface after the reset.

## Decisions

### 1. `docs/superpowers` Becomes The Only In-Repo Planning Surface

Forward-looking design and implementation planning now lives under:

- `docs/superpowers/specs`
- `docs/superpowers/plans`

The repo-root `.planning/` folder should be removed after the replacement docs exist.

### 2. Plans Must Be Narrow And Code-Anchored

Each retained idea gets its own implementation plan with:

- a single clear goal
- concrete files to change
- test and verification commands
- release posture where relevant

### 3. Historical Status Does Not Carry Forward

The new docs should not pretend milestone status, roadmap state, or tech debt lists from March remain current.
Only live gaps and next actions should survive the reset.

## Deliverables

The reset produces:

- one design doc for the planning reset
- one plan for open-generic registration support
- one plan for generator resilience hardening
- one plan for metadata and documentation hygiene
- removal of the repo-root `.planning/` folder

## Acceptance Criteria

- the useful next-work ideas from the old planning tree exist as current `docs/superpowers` docs
- no repo-root `.planning/` directory remains
- the new docs reference live source files and current release posture
- no stale roadmap or milestone language is reintroduced
