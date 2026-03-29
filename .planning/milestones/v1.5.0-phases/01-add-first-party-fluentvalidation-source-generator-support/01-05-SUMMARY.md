---
phase: 01-add-first-party-fluentvalidation-source-generator-support
plan: 05
subsystem: diagnostics
tags: [fluentvalidation, source-generator, roslyn, diagnostics, IOC100, IOC101]

# Dependency graph
requires:
  - phase: 01-add-first-party-fluentvalidation-source-generator-support/01-03
    provides: "Validator discovery pipeline and registration emitter"
provides:
  - "DirectInstantiationValidator (IOC100) for detecting new ChildValidator() anti-pattern"
  - "CompositionLifetimeValidator (IOC101) for captive dependency detection"
  - "ValidatorDiagnosticsPipeline wired into FluentValidationGenerator"
  - "CompositionEdge and CompositionType stub types for plan 04 integration"
affects: ["01-04-composition-graph-builder", "01-06", "01-07"]

# Tech tracking
tech-stack:
  added: []
  patterns: ["CompositionEdge-based validator diagnostic pattern", "Action<Diagnostic> callback for testable validators"]

key-files:
  created:
    - "IoCTools.FluentValidation/IoCTools.FluentValidation/Diagnostics/Validators/DirectInstantiationValidator.cs"
    - "IoCTools.FluentValidation/IoCTools.FluentValidation/Diagnostics/Validators/CompositionLifetimeValidator.cs"
    - "IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/Pipeline/ValidatorDiagnosticsPipeline.cs"
    - "IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/CompositionGraph/CompositionEdge.cs"
    - "IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/CompositionGraph/CompositionType.cs"
    - "IoCTools.FluentValidation.Tests/DiagnosticTests.cs"
    - "IoCTools.FluentValidation.Tests/CompositionGraphTests.cs"
  modified:
    - "IoCTools.FluentValidation/IoCTools.FluentValidation/Models/ValidatorClassInfo.cs"
    - "IoCTools.FluentValidation/IoCTools.FluentValidation/Diagnostics/FluentValidationDiagnosticDescriptors.cs"
    - "IoCTools.FluentValidation/IoCTools.FluentValidation/FluentValidationGenerator.cs"
    - "IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj"

key-decisions:
  - "Created CompositionEdge/CompositionType stubs for plan 04 integration rather than blocking on parallel execution"
  - "Used Action<Diagnostic> callback pattern for testable validators (matching IoCTools convention)"
  - "Only flag Singleton->Scoped/Transient as IOC101 (standard IoCTools captive dependency pattern)"
  - "DirectInstantiationValidator checks both discovered validators and types with [Inject] attributes"

patterns-established:
  - "Validator diagnostic pattern: static Validate method with Action<Diagnostic> callback for unit testability"
  - "CompositionEdge-based diagnostic detection: iterate edges, cross-reference allValidators array"

requirements-completed: [FV-06]

# Metrics
duration: 4min
completed: 2026-03-29
---

# Phase 01 Plan 05: Anti-Pattern Diagnostics Summary

**IOC100/IOC101 diagnostic validators for detecting direct instantiation and lifetime mismatch anti-patterns in FluentValidation composition chains**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-29T21:15:54Z
- **Completed:** 2026-03-29T21:20:22Z
- **Tasks:** 2
- **Files modified:** 11

## Accomplishments
- Implemented DirectInstantiationValidator (IOC100) detecting `new ChildValidator()` when child is DI-managed, with dependency chain reporting per D-14
- Implemented CompositionLifetimeValidator (IOC101) detecting Singleton parent composing Scoped/Transient child validators
- Wired ValidatorDiagnosticsPipeline into FluentValidationGenerator with try/catch safety
- Created CompositionEdge and CompositionType stub types for plan 04 integration
- Added 14 new tests (7 diagnostic + 7 composition graph) all passing

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement DirectInstantiationValidator and CompositionLifetimeValidator** - `d76034e` (feat)
2. **Task 2: Wire diagnostics pipeline and create tests** - `a1891e6` (test)

## Files Created/Modified
- `IoCTools.FluentValidation/.../Diagnostics/Validators/DirectInstantiationValidator.cs` - IOC100 detection for direct instantiation anti-pattern
- `IoCTools.FluentValidation/.../Diagnostics/Validators/CompositionLifetimeValidator.cs` - IOC101 detection for lifetime mismatch
- `IoCTools.FluentValidation/.../Generator/Pipeline/ValidatorDiagnosticsPipeline.cs` - Wires validators into incremental pipeline
- `IoCTools.FluentValidation/.../Generator/CompositionGraph/CompositionEdge.cs` - Composition relationship model (stub for plan 04)
- `IoCTools.FluentValidation/.../Generator/CompositionGraph/CompositionType.cs` - Enum for composition types
- `IoCTools.FluentValidation/.../Models/ValidatorClassInfo.cs` - Added CompositionEdges property
- `IoCTools.FluentValidation/.../Diagnostics/FluentValidationDiagnosticDescriptors.cs` - Updated message format placeholders
- `IoCTools.FluentValidation/.../FluentValidationGenerator.cs` - Attached diagnostics pipeline
- `IoCTools.FluentValidation.Tests/DiagnosticTests.cs` - 7 tests for IOC100/IOC101
- `IoCTools.FluentValidation.Tests/CompositionGraphTests.cs` - 7 tests for composition edge structure

## Decisions Made
- Created CompositionEdge/CompositionType as stubs since plan 04 (composition graph builder) runs in parallel. These define the contract that plan 04 will populate. Reconciliation at merge time.
- Used `Action<Diagnostic>` callback pattern for validators (matching existing IoCTools convention) for easy unit testing without SourceProductionContext.
- Only Singleton parent with shorter-lived child triggers IOC101, matching the standard IoCTools captive dependency detection approach (IOC012/IOC013 pattern).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Created CompositionEdge and CompositionType stub types**
- **Found during:** Task 1
- **Issue:** Plan 04 (composition graph builder) runs in parallel and hasn't created these types yet, but they're needed for the validators
- **Fix:** Created minimal CompositionEdge struct and CompositionType enum matching the interface spec from the plan
- **Files modified:** Generator/CompositionGraph/CompositionEdge.cs, Generator/CompositionGraph/CompositionType.cs
- **Verification:** Build succeeds, tests pass
- **Committed in:** d76034e

**2. [Rule 3 - Blocking] Added InternalsVisibleTo for test project**
- **Found during:** Task 2
- **Issue:** Test project couldn't access internal types (validators, CompositionEdge, ValidatorClassInfo)
- **Fix:** Added `<InternalsVisibleTo Include="IoCTools.FluentValidation.Tests"/>` to csproj
- **Files modified:** IoCTools.FluentValidation.csproj
- **Verification:** Tests compile and pass
- **Committed in:** a1891e6

---

**Total deviations:** 2 auto-fixed (2 blocking)
**Impact on plan:** Both auto-fixes necessary for compilation. No scope creep.

## Issues Encountered
None

## Known Stubs
- `CompositionEdge.cs` and `CompositionType.cs` are stub types that will be reconciled with plan 04's implementation at merge time
- `ValidatorClassInfo.CompositionEdges` defaults to empty - will be populated by CompositionGraphBuilder from plan 04

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Diagnostic validators are complete and tested
- Awaiting plan 04 merge to connect CompositionGraphBuilder which populates CompositionEdges on ValidatorClassInfo
- Once plan 04 merges, IOC100/IOC101 will fire automatically for real validator composition patterns

---
*Phase: 01-add-first-party-fluentvalidation-source-generator-support*
*Completed: 2026-03-29*
