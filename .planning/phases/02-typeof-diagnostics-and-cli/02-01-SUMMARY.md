---
phase: 02-typeof-diagnostics-and-cli
plan: 01
subsystem: diagnostics
tags: [typeof, source-generator, diagnostics, validation]

# Dependency graph
requires: []
provides:
  - IOC090 diagnostic for typeof() registrations that could use IoCTools attributes
  - IOC091 diagnostic for typeof() registrations duplicating IoCTools
  - IOC092 diagnostic for typeof() registrations with lifetime mismatch
  - IOC094 diagnostic for open generic typeof() registrations
  - typeof() argument parsing in ManualRegistrationValidator
  - ServiceDescriptor factory method detection (Scoped/Singleton/Transient)
affects: [02-02-cli-improvements]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - typeof() expression syntax parsing with TypeOfExpressionSyntax
    - Open generic detection via OmittedTypeArgumentSyntax
    - SemanticModel.GetTypeInfo for type symbol extraction

key-files:
  created: []
  modified:
    - IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs
    - IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/ManualRegistrationValidator.cs
    - docs/diagnostics.md

key-decisions:
  - "typeof() diagnostics share IoCToolsManualSeverity MSBuild knob with existing manual registration diagnostics"
  - "ServiceDescriptor factory methods (Scoped/Singleton/Transient) detected alongside Add{Lifetime} extension methods per D-02"
  - "new ServiceDescriptor constructor form NOT detected per D-03 decision"

patterns-established:
  - "TypeOfExpressionSyntax pattern: GetTypeInfo on typeOfExpr.Type NOT on typeOfExpr itself"
  - "Open generic detection: GenericNameSyntax with OmittedTypeArgumentSyntax in TypeArgumentList"

requirements-completed: [DIAG-01, DIAG-02, DIAG-03, DIAG-04, DIAG-05]

# Metrics
duration: 12min
completed: 2026-03-21
---

# Phase 02 Plan 01: typeof() Diagnostics Summary

**typeof() registration diagnostics (IOC090-094) with open generic detection and ServiceDescriptor factory method support**

## Performance

- **Duration:** 12 min
- **Started:** 2026-03-21T18:00:00Z
- **Completed:** 2026-03-21T18:12:00Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Four new diagnostic descriptors (IOC090, IOC091, IOC092, IOC094) defined in RegistrationDiagnostics.cs
- ManualRegistrationValidator extended to detect typeof()-based registrations
- Support for both `AddScoped(typeof())` and `ServiceDescriptor.Scoped(typeof())` patterns
- Open generic typeof() detection emitting IOC094 at Info severity
- Documentation updated in docs/diagnostics.md

## Task Commits

Each task was committed atomically:

1. **Task 1: Add IOC090-094 diagnostic descriptors** - `587736e` (feat)
2. **Task 2: Extend ManualRegistrationValidator with typeof() detection** - `fbe8242` (feat)

**Plan metadata:** (to be added)

## Files Created/Modified

- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs` - Added four new DiagnosticDescriptor fields (IOC090-094)
- `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/ManualRegistrationValidator.cs` - Extended with typeof() detection logic, ServiceDescriptor factory method detection, and helper functions
- `docs/diagnostics.md` - Added documentation entries for IOC090, IOC091, IOC092, and IOC094

## Decisions Made

1. **typeof() diagnostics share IoCToolsManualSeverity MSBuild knob** - The descriptors use base severities (Warning/Warning/Error/Info) and the existing DiagnosticConfiguration mechanism handles MSBuild overrides automatically.

2. **ServiceDescriptor factory methods detected per D-02** - Both `AddScoped(typeof())` extension method pattern AND `ServiceDescriptor.Scoped(typeof())` static factory method pattern are detected.

3. **new ServiceDescriptor constructor NOT detected per D-03** - The `new ServiceDescriptor(typeof(...), typeof(...), ServiceLifetime.X)` constructor form is intentionally not detected to avoid scope creep.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- typeof() diagnostics complete and ready for testing integration
- ManualRegistrationValidator patterns established for similar extensions
- Ready for 02-02-cli-improvements which may reference these new diagnostics

---
*Phase: 02-typeof-diagnostics-and-cli*
*Plan: 01*
*Completed: 2026-03-21*

## Self-Check: PASSED

- [x] SUMMARY.md created at `.planning/phases/02-typeof-diagnostics-and-cli/02-01-SUMMARY.md`
- [x] Task 1 commit exists: `587736e`
- [x] Task 2 commit exists: `fbe8242`
- [x] Final metadata commit exists: `3c01da4`
- [x] Generator builds with 0 errors
- [x] All IOC090-094 descriptors present in RegistrationDiagnostics.cs
- [x] typeof() detection logic present in ManualRegistrationValidator.cs
- [x] docs/diagnostics.md updated with all four new diagnostics
