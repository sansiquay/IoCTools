---
phase: 02-typeof-diagnostics-and-cli
plan: 05
subsystem: cli
tags: [editorconfig, suppress, diagnostics, dotnet-cli]

# Dependency graph
requires:
  - phase: 02-03
    provides: doctor command and DiagnosticRunner infrastructure
provides:
  - ioc-tools suppress command for generating .editorconfig rules
  - DiagnosticCatalog with all 94 IoCTools diagnostics
  - SuppressPrinter for .editorconfig rule generation
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
  - Manual diagnostic catalog instead of runtime reflection
  - Severity and code filtering with OR logic for combined filters
  - Conflict detection for file appending

key-files:
  created:
    - IoCTools.Tools.Cli/Utilities/DiagnosticCatalog.cs
    - IoCTools.Tools.Cli/Utilities/SuppressPrinter.cs
  modified:
    - IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs
    - IoCTools.Tools.Cli/Program.cs
    - IoCTools.Tools.Cli/Utilities/UsagePrinter.cs

key-decisions:
  - "Manual catalog approach over reflection: Reflection across analyzer boundaries is unreliable at runtime; building a static catalog of all 94 diagnostics ensures the CLI can access diagnostic metadata without runtime dependencies on the generator assembly"
  - "Default severity filter is warning+info: Error diagnostics require explicit --codes flag to prevent accidentally suppressing build-blocking diagnostics"

patterns-established:
  - "Command parser extension pattern: Add ParseXxx method, XxxCommandOptions record, update NormalizeKey and IsFlag"
  - ".editorconfig generation pattern: Group by category, add inline comments, detect existing rules before appending"
  - "Live diagnostic mode: Run generator to collect actual firing diagnostics, filter catalog to match"

requirements-completed: [CLI-07]

# Metrics
duration: 12min
completed: 2026-03-21
---

# Phase 02 Plan 05: suppress Command Summary

**CLI suppress command generates .editorconfig rules for IoCTools diagnostics with severity/code filtering, live mode, and file appending with conflict detection**

## Performance

- **Duration:** 12 minutes
- **Started:** 2026-03-21T18:15:00Z
- **Completed:** 2026-03-21T18:27:00Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments

- Added `ioc-tools suppress` command for generating .editorconfig suppression rules
- Created DiagnosticCatalog with all 94 IoCTools diagnostics (IOC001-IOC094)
- Implemented --severity, --codes, --live, and --output flag support
- Integrated with DiagnosticRunner for live diagnostic detection
- Added conflict detection when appending to existing .editorconfig files

## Task Commits

Each task was committed atomically:

1. **Task 1: Create DiagnosticCatalog, SuppressPrinter, and ParseSuppress** - `eedf754` (feat)
2. **Task 2: Wire suppress command into Program.cs and usage** - `385b0df` (feat)

## Files Created/Modified

- `IoCTools.Tools.Cli/Utilities/DiagnosticCatalog.cs` - Static catalog of all 94 IoCTools diagnostics with ID, title, category, and default severity
- `IoCTools.Tools.Cli/Utilities/SuppressPrinter.cs` - Generates .editorconfig rules with filtering, grouping, and conflict detection
- `IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs` - Added ParseSuppress method, SuppressCommandOptions record, updated NormalizeKey and IsFlag
- `IoCTools.Tools.Cli/Program.cs` - Added suppress command case and RunSuppressAsync method with --live support
- `IoCTools.Tools.Cli/Utilities/UsagePrinter.cs` - Added suppress command to usage help

## Decisions Made

- **Manual catalog approach over reflection:** Reflection across analyzer boundaries is unreliable at runtime since the CLI references the generator as an analyzer. Building a static catalog of all 94 diagnostics ensures the CLI can access diagnostic metadata without runtime dependencies.
- **Default severity filter is warning+info:** Error diagnostics require explicit `--codes` flag to prevent accidentally suppressing build-blocking diagnostics. This is a safety feature to prevent users from accidentally disabling critical error checks.
- **OR logic for combined --severity and --codes:** When both flags are specified, diagnostics matching either filter are included. This provides flexibility for users who want specific error codes plus all warnings/infos.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed TypeFilterUtility.LastIndexOf overload**
- **Found during:** Task 1 (CLI build verification)
- **Issue:** `LastIndexOf` with `StringComparison` overload doesn't exist in the target framework, causing build failure
- **Fix:** Changed to parameterless `LastIndexOf('.')` and updated substring call to use length calculation
- **Files modified:** IoCTools.Tools.Cli/Utilities/TypeFilterUtility.cs
- **Verification:** CLI project builds successfully with 0 errors
- **Committed in:** Part of Task 1 (automatic linter fix)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** The TypeFilterUtility fix was a pre-existing bug that blocked the build. No impact on plan scope.

## Issues Encountered

- Initial Write tool failure for SuppressPrinter.cs: The first attempt to create SuppressPrinter.cs appeared to succeed but the file was not created. Recreated the file on second attempt.
- Missing using directive: SuppressPrinter.cs needed `using CommandLine;` to access SuppressCommandOptions. Added the using directive.

## User Setup Required

None - no external service configuration required.

## Verification

- CLI project builds successfully: `dotnet build IoCTools.Tools.Cli`
- All 89 CLI tests pass: `dotnet test IoCTools.Tools.Cli.Tests`
- Suppress command appears in usage help: `dotnet ioc-tools help`
- DiagnosticCatalog contains all 94 diagnostics across 5 categories

## Next Phase Readiness

- suppress command complete and ready for use
- No blockers or concerns
- Ready to proceed with remaining phase 02 plans

---
*Phase: 02-typeof-diagnostics-and-cli*
*Plan: 05*
*Completed: 2026-03-21*
