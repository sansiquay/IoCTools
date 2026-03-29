---
phase: 01-code-quality-diagnostic-ux
plan: 02
subsystem: code-quality
tags: [source-generator, refactoring, exception-handling, diagnostics, nullability]

# Dependency graph
requires: []
provides:
  - Centralized RegisterAsAllAttribute checking via AttributeTypeChecker
  - Shared ReportDiagnosticDelegate pattern for testable validators
  - OOM/SOF-filtered exception handling at all generator catch sites
  - CS8603-free sample code
  - InstanceSharing.Separate documentation
affects: [testing, documentation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "AttributeTypeChecker.IsAttribute() for all attribute type comparisons"
    - "ReportDiagnosticDelegate for validator testability decoupling from SourceProductionContext"
    - "OOM/SOF exception filter with re-throw to caller-level diagnostic emitters"

key-files:
  created: []
  modified:
    - IoCTools.Generator/IoCTools.Generator/Utilities/AttributeTypeChecker.cs
    - IoCTools.Generator/IoCTools.Generator/Utilities/GeneratorDiagnostics.cs
    - IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorGenerator.cs
    - IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator.RegistrationCode.cs
    - IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator.RegisterAs.cs
    - IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/CircularDependencyValidator.cs
    - IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/ConditionalServiceValidator.cs
    - IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/DependencyUsageValidator.cs
    - IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/MissedOpportunityValidator.cs
    - IoCTools.Abstractions/Annotations/RegisterAsAttribute.cs
    - IoCTools.Sample/Services/MultiInterfaceExamples.cs

key-decisions:
  - "DiagnosticRules kept as adapter layer between SourceProductionContext and ReportDiagnosticDelegate validators"
  - "Exception sites re-throw rather than return empty string, delegating error reporting to caller-level catch handlers"

patterns-established:
  - "AttributeTypeChecker.IsAttribute() for all attribute type comparisons -- no more ad-hoc .Name == checks"
  - "ReportDiagnosticDelegate for validators that only report diagnostics (no AddSource)"
  - "catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException) { throw; } at inner sites"

requirements-completed: [QUAL-01, QUAL-02, QUAL-03, QUAL-04, QUAL-05]

# Metrics
duration: 10min
completed: 2026-03-21
---

# Phase 01 Plan 02: Code Quality Summary

**Centralized 14 RegisterAsAllAttribute checks through AttributeTypeChecker, tightened exception handling with OOM/SOF filters, adopted ReportDiagnosticDelegate in 4 validators, eliminated CS8603 warnings, and documented InstanceSharing.Separate defaults**

## Performance

- **Duration:** 10 min
- **Started:** 2026-03-21T17:03:47Z
- **Completed:** 2026-03-21T17:14:08Z
- **Tasks:** 3
- **Files modified:** 23

## Accomplishments
- All 14 ad-hoc RegisterAsAllAttribute checks (10 `.Name ==` and 4 inline FQN) centralized through AttributeTypeChecker
- Both bare `catch(Exception)` blocks replaced with OOM/SOF-filtered handlers that re-throw to caller-level IOC995/IOC992/IOC999 emitters
- ReportDiagnosticDelegate shared across CircularDependencyValidator, ConditionalServiceValidator, DependencyUsageValidator, and MissedOpportunityValidator
- CS8603 warnings fully eliminated from MultiInterfaceExamples.cs (4 instances across 3 methods)
- InstanceSharing.Separate documented in RegisterAsAttribute XML docs, generator code comments, and sample examples

## Task Commits

Each task was committed atomically:

1. **Task 1: Centralize RegisterAsAllAttribute checks** - `8ad4a9e` (refactor)
2. **Task 2: Tighten exception handling and adopt ReportDiagnosticDelegate** - `451a675` (refactor)
3. **Task 3: Fix CS8603 and add InstanceSharing.Separate comments** - `6652e66` (fix)

## Files Created/Modified
- `IoCTools.Generator/IoCTools.Generator/Utilities/ServiceRegistrationScan.cs` - Centralized attribute checks
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/BaseConstructorCallBuilder.cs` - Centralized attribute checks
- `IoCTools.Generator/IoCTools.Generator/Utilities/DiagnosticScan.cs` - Centralized attribute checks
- `IoCTools.Generator/IoCTools.Generator/Generator/ConstructorEmitter.cs` - Centralized attribute checks
- `IoCTools.Generator/IoCTools.Generator/Analysis/TypeAnalyzer.cs` - Centralized attribute checks
- `IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticsRunner.cs` - Centralized attribute checks + delegate call sites
- `IoCTools.Generator/IoCTools.Generator/Generator/Pipeline/ServiceClassPipeline.cs` - Centralized attribute checks
- `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/MissedOpportunityValidator.cs` - Centralized checks + ReportDiagnosticDelegate
- `IoCTools.Generator/IoCTools.Generator/Generator/RegistrationSelector.cs` - Centralized attribute checks
- `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/DependencySetValidator.cs` - Centralized constant reference
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorGenerator.cs` - OOM/SOF exception filter
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator.RegistrationCode.cs` - OOM/SOF exception filter
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator.RegisterAs.cs` - Removed private delegate + InstanceSharing comments
- `IoCTools.Generator/IoCTools.Generator/Utilities/GeneratorDiagnostics.cs` - Shared ReportDiagnosticDelegate
- `IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticRules.cs` - Adapter calls through delegate
- `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/DependencyUsageValidator.cs` - ReportDiagnosticDelegate
- `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/CircularDependencyValidator.cs` - ReportDiagnosticDelegate
- `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/ConditionalServiceValidator.cs` - ReportDiagnosticDelegate
- `IoCTools.Abstractions/Annotations/RegisterAsAttribute.cs` - InstanceSharing param XML docs
- `IoCTools.Sample/Services/MultiInterfaceExamples.cs` - CS8603 fixes with nullable return types
- `IoCTools.Sample/Services/RegisterAsExamples.cs` - InstanceSharing.Separate usage comment

## Decisions Made
- DiagnosticRules kept as adapter layer between SourceProductionContext and delegate-based validators, rather than changing DiagnosticRules method signatures to use ReportDiagnosticDelegate directly
- Exception sites re-throw to caller-level handlers rather than returning empty strings, ensuring errors always surface as build diagnostics

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed additional CS8603 warnings in MultiInterfaceExamples.cs**
- **Found during:** Task 3 (CS8603 fix)
- **Issue:** Plan identified 1 CS8603 instance but there were 4 total (GetValueOrDefault, GetUserAsync, CreateUserAsync, GetDataAsync)
- **Fix:** Added nullable return types to interface declarations and implementations for all 4 methods
- **Files modified:** IoCTools.Sample/Services/MultiInterfaceExamples.cs
- **Verification:** `dotnet build 2>&1 | grep CS8603` returns 0 matches
- **Committed in:** 6652e66 (Task 3 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Auto-fix was necessary to fully resolve CS8603 warnings. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Code quality improvements complete, ready for Phase 2+ work
- All 1650 tests pass with zero regressions
- Full solution builds with zero errors and zero CS8603 warnings

---
*Phase: 01-code-quality-diagnostic-ux*
*Completed: 2026-03-21*
