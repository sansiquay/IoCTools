---
phase: 01-code-quality-diagnostic-ux
plan: 01
subsystem: diagnostics
tags: [roslyn, diagnostics, helplink, ide-ux, source-generator]

# Dependency graph
requires: []
provides:
  - "HelpLinkUri on all 87 diagnostic descriptors"
  - "Specific IDE categories (IoCTools.Lifetime/Dependency/Configuration/Registration/Structural)"
  - "Enhanced IOC012/013/087 messages with CreateScope() suggestion"
  - "IOC015 inheritance path display in diagnostic messages"
  - "docs/diagnostics.md reference file with anchored entries for all 87 diagnostics"
affects: [documentation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "8-arg DiagnosticDescriptor constructor with helpLinkUri as 8th positional arg"
    - "Category naming: IoCTools.{Subcategory} matching descriptor file names"

key-files:
  created:
    - docs/diagnostics.md
  modified:
    - IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/LifetimeDiagnostics.cs
    - IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/StructuralDiagnostics.cs
    - IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs
    - IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/DependencyDiagnostics.cs
    - IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/ConfigurationDiagnostics.cs
    - IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/LifetimeDependencyValidator.cs

key-decisions:
  - "Used 'A -> B -> C' arrow format for inheritance path display in IOC015"
  - "Lean docs/diagnostics.md format with severity, category, cause, fix per diagnostic"

patterns-established:
  - "HelpLinkUri pattern: https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#iocXXX"
  - "IDE category naming: IoCTools.{Subcategory} with 5 subcategories matching descriptor files"

requirements-completed: [DUX-01, DUX-02, DUX-03, DUX-04, DUX-05]

# Metrics
duration: 8min
completed: 2026-03-21
---

# Phase 01 Plan 01: Diagnostic UX Summary

**HelpLinkUri, specific IDE categories, and enhanced messages added to all 87 diagnostics with docs/diagnostics.md reference file**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-21T17:03:33Z
- **Completed:** 2026-03-21T17:11:41Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments
- All 87 diagnostic descriptors updated with IoCTools.{Subcategory} categories and helpLinkUri values
- IOC012/013/087 descriptions now suggest IServiceProvider/CreateScope() as a fix option
- IOC015 message format includes full inheritance path display via {3} format arg
- IOC016-019 descriptions include configuration usage examples
- Created docs/diagnostics.md with 87 anchored entries matching helpLinkUri pattern

## Task Commits

Each task was committed atomically:

1. **Task 1: Add HelpLinkUri and IDE categories to all 87 diagnostic descriptors** - `74a04cc` (feat)
2. **Task 2: Update IOC015 emit sites for inheritance path and create docs/diagnostics.md** - `4c8e1b5` (feat)

## Files Created/Modified
- `docs/diagnostics.md` - New diagnostics reference with anchored entries for all 87 diagnostics
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/LifetimeDiagnostics.cs` - 10 descriptors with IoCTools.Lifetime category, helpLinkUri, enhanced IOC012/013/015/087 messages
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/StructuralDiagnostics.cs` - 13 descriptors with IoCTools.Structural category and helpLinkUri
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs` - 27 descriptors with IoCTools.Registration category and helpLinkUri
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/DependencyDiagnostics.cs` - 27 descriptors with IoCTools.Dependency category and helpLinkUri
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/ConfigurationDiagnostics.cs` - 10 descriptors with IoCTools.Configuration category, helpLinkUri, enhanced IOC016-019 messages
- `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/LifetimeDependencyValidator.cs` - IOC015 emit sites updated with inheritance path format arg

## Decisions Made
- Used "A -> B -> C" arrow format for inheritance path display (matches existing codebase conventions for dependency chain display)
- Lean docs/diagnostics.md format per D-03: code, severity, category, one-line cause, one-line fix
- Single docs/diagnostics.md file with anchors per D-02 (no per-diagnostic files)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

The parallel agent (plan 01-02) introduced build errors in other files (DiagnosticRules.cs, DependencyUsageValidator.cs). These errors are unrelated to plan 01-01's changes and do not affect the descriptor files or validator updated here. The parallel agent will resolve these in its own commits.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All diagnostic descriptors now have proper IDE integration (clickable help links, filterable categories)
- docs/diagnostics.md is ready to be expanded with detailed examples in Phase 4 (Documentation)
- The helpLinkUri pattern is established for any future diagnostics

---
*Phase: 01-code-quality-diagnostic-ux*
*Completed: 2026-03-21*
