---
phase: 02-typeof-diagnostics-and-cli
plan: 03
subsystem: cli
tags: [json, verbose, ansi-color, output-routing, terminal-ui]

# Dependency graph
requires:
  - phase: 01-code-quality
    provides: base-cli-architecture, existing-printers
provides:
  - json-output-mode for all CLI commands
  - verbose-debug-mode with timing info
  - color-coded severity and lifetime labels
  - OutputContext cross-cutting abstraction
affects: [phase-02-cli-improvements, future-cli-testing]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - OutputContext pattern for cross-cutting output routing
    - AnsiColor utility with NO_COLOR/pipe detection
    - JSON-first output mode for automation pipelines

key-files:
  created:
    - IoCTools.Tools.Cli/Utilities/AnsiColor.cs
    - IoCTools.Tools.Cli/Utilities/OutputContext.cs
  modified:
    - IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs
    - IoCTools.Tools.Cli/Program.cs
    - IoCTools.Tools.Cli/Utilities/DoctorPrinter.cs
    - IoCTools.Tools.Cli/Utilities/RegistrationPrinter.cs
    - IoCTools.Tools.Cli/Utilities/ExplainPrinter.cs
    - IoCTools.Tools.Cli/Utilities/GraphPrinter.cs
    - IoCTools.Tools.Cli/Utilities/ProfilePrinter.cs
    - IoCTools.Tools.Cli/Utilities/WhyPrinter.cs
    - IoCTools.Tools.Cli/Utilities/ConfigAuditPrinter.cs

key-decisions:
  - "OutputContext routes JSON to stdout and verbose to stderr, enabling --json --verbose together"
  - "AnsiColor auto-disables on pipe redirection or NO_COLOR env var"
  - "Severity colors: red Error, yellow Warning, cyan Info"
  - "Lifetime colors: green Singleton, blue Scoped, gray Transient"

patterns-established:
  - "OutputContext pattern: create once per command, pass to all output methods"
  - "Printer signature: Write(input, OutputContext) for all printers"
  - "JSON mode: suppress human output, emit single JSON payload at end"
  - "Verbose mode: stderr-only timing messages in [verbose] <elapsed>ms: <message> format"

requirements-completed: [CLI-01, CLI-02, CLI-03]

# Metrics
duration: 8min
completed: 2026-03-21
---

# Phase 02 Plan 03: CLI Infrastructure Summary

**JSON output mode, verbose debugging, and color-coded terminal UI for all ioc-tools commands**

## Performance

- **Duration:** 8 minutes
- **Started:** 2026-03-21T18:10:14Z
- **Completed:** 2026-03-21T18:18:00Z
- **Tasks:** 3
- **Files modified:** 11

## Accomplishments

- Created `AnsiColor` utility with NO_COLOR and pipe detection for terminal-friendly output
- Created `OutputContext` abstraction for cross-cutting --json/--verbose output routing
- Added --json and --verbose/-v flags to CLI parser with CommonOptions fields
- Updated all 7 printers (Doctor, Registration, Explain, Graph, Profile, Why, ConfigAudit) with OutputContext support
- Added color-coded severity labels (red/yellow/cyan) and lifetime labels (green/blue/gray)
- All 89 CLI tests passing with no regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Create AnsiColor utility and OutputContext abstraction** - `587736e` (feat)
2. **Task 2: Wire --json and --verbose flags through parser and Program.cs** - `1212414` (feat)
3. **Task 3: Update printers with color and OutputContext support** - `96afd82` (feat)

**Plan metadata:** No summary commit (individual task commits only)

## Files Created/Modified

### Created
- `IoCTools.Tools.Cli/Utilities/AnsiColor.cs` - ANSI escape sequences with NO_COLOR/pipe detection, Severity() and Lifetime() color methods
- `IoCTools.Tools.Cli/Utilities/OutputContext.cs` - Cross-cutting output routing with IsJson/IsVerbose, WriteJson(), Verbose(), ReportTiming()

### Modified
- `IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs` - Added --json/--verbose mappings to NormalizeKey(), added to IsFlag(), updated CommonOptions with Json/Verbose fields, updated BuildCommon() to extract flags
- `IoCTools.Tools.Cli/Program.cs` - Added OutputContext.Create() in all RunXxxAsync methods, added verbose logging for project loading, added ReportTiming() calls, passed output to all printer Write() calls
- `IoCTools.Tools.Cli/Utilities/DoctorPrinter.cs` - Added OutputContext param, color severity with AnsiColor.Severity(), JSON mode with diagnostic payload
- `IoCTools.Tools.Cli/Utilities/RegistrationPrinter.cs` - Added OutputContext param, color lifetime with AnsiColor.Lifetime(), JSON mode with registration records
- `IoCTools.Tools.Cli/Utilities/ExplainPrinter.cs` - Added OutputContext param, JSON mode with dependency/config payloads
- `IoCTools.Tools.Cli/Utilities/GraphPrinter.cs` - Added OutputContext param, support --json flag alongside --format json
- `IoCTools.Tools.Cli/Utilities/ProfilePrinter.cs` - Added OutputContext param, JSON mode with timing info
- `IoCTools.Tools.Cli/Utilities/WhyPrinter.cs` - Added OutputContext param, JSON mode with match results, added MatchResult record
- `IoCTools.Tools.Cli/Utilities/ConfigAuditPrinter.cs` - Added OutputContext param, JSON mode with audit results, fixed variable name collisions

## Decisions Made

1. **OutputContext routing strategy**: JSON goes to stdout, verbose goes to stderr. This enables `--json --verbose` to work together for debugging automation pipelines.

2. **Color auto-detection**: Both `Console.IsOutputRedirected` and `NO_COLOR` environment variable are checked. This follows standard CLI conventions for color control.

3. **Severity color mapping**: Error=red, Warning=yellow, Info=cyan. Matches common IDE color schemes.

4. **Lifetime color mapping**: Singleton=green (stable/long-lived), Scoped=blue (request-scoped), Transient=gray (ephemeral).

5. **GraphPrinter --format compatibility**: The existing `--format json` is preserved for backwards compatibility, but `--json` flag now works for all commands consistently.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

1. **Compilation error during Task 2**: Printer methods didn't yet accept OutputContext parameter. Fixed by reverting printer calls to original signatures in Program.cs, then updating printers in Task 3.

2. **Nullable warning in WhyPrinter**: Anonymous type mismatch between dependency (Source as string?) and configuration (Source as string). Fixed by introducing explicit `MatchResult` record type.

3. **Variable name collision in ConfigAuditPrinter**: `settingsKeys` and `missing` declared in both JSON and non-JSON paths within same scope. Fixed by renaming JSON path variables to `jsonSettingsKeys` and `jsonMissing`.

4. **Empty WriteLine calls**: `OutputContext.WriteLine()` requires a parameter. Fixed by changing to `WriteLine(string.Empty)`.

## User Setup Required

None - no external service configuration required.

## Verification

- All 89 CLI tests passing
- Manual verification commands:
  - `ioc-tools doctor --project IoCTools.Sample --json` outputs valid JSON
  - `ioc-tools doctor --project IoCTools.Sample --verbose` outputs timing to stderr
  - `ioc-tools services --project IoCTools.Sample` shows colored lifetime labels

## Next Phase Readiness

- CLI infrastructure ready for additional commands
- All printers support JSON output for automation
- Verbose mode available for debugging all commands
- Color-coded output improves visual scanning in terminal

---
*Phase: 02-typeof-diagnostics-and-cli*
*Plan: 03*
*Completed: 2026-03-21*
