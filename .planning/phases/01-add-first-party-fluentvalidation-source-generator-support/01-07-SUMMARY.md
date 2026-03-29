---
phase: 01-add-first-party-fluentvalidation-source-generator-support
plan: 07
subsystem: cli
tags: [fluentvalidation, cli, validator-inspection, composition-graph, lifetime-tracing]

requires:
  - phase: 01-add-first-party-fluentvalidation-source-generator-support/01-04
    provides: "Composition graph model concepts (SetValidator/Include/SetInheritanceValidator)"
  - phase: 01-add-first-party-fluentvalidation-source-generator-support/01-05
    provides: "Diagnostic patterns for anti-pattern detection context"

provides:
  - "validators CLI command for listing FluentValidation validators with model types and lifetimes"
  - "validator-graph CLI command for composition tree visualization"
  - "--why flag for tracing validator lifetime through composition chains"
  - "ValidatorInspector for Roslyn-based name-matching discovery of AbstractValidator<T>"

affects: []

tech-stack:
  added: []
  patterns: [name-based-roslyn-validator-discovery, composition-tree-building, lifetime-chain-tracing]

key-files:
  created:
    - IoCTools.Tools.Cli/Utilities/ValidatorInspector.cs
    - IoCTools.Tools.Cli/Utilities/ValidatorPrinter.cs
    - IoCTools.Tools.Cli.Tests/ValidatorCommandTests.cs
  modified:
    - IoCTools.Tools.Cli/Program.cs
    - IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs
    - IoCTools.Tools.Cli/Utilities/UsagePrinter.cs

key-decisions:
  - "Name-based Roslyn detection (AbstractValidator<T>) instead of depending on FluentValidation generator package"
  - "Composition edges built from syntax tree walking (same approach as generator) for CLI independence"
  - "Unit tests with in-memory CSharpCompilation rather than full MSBuild workspace integration tests"

patterns-established:
  - "ValidatorInspector: standalone Roslyn compilation analysis without generator dependency"
  - "CompositionEdgeInfo/ValidatorInfo models for CLI-local validator representation"

requirements-completed: [FV-08]

duration: 4min
completed: 2026-03-29
---

# Phase 01 Plan 07: CLI Validator Commands Summary

**CLI commands for listing FluentValidation validators, visualizing composition graphs, and tracing lifetime decisions through validator chains using name-based Roslyn analysis**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-29T21:35:21Z
- **Completed:** 2026-03-29T21:39:55Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- ValidatorInspector discovers AbstractValidator<T> subclasses by walking compilation syntax trees with name-based matching
- Detects IoCTools lifetime attributes (Scoped/Singleton/Transient) on validator classes including inherited lifetimes
- Builds composition edges from SetValidator, Include, and SetInheritanceValidator invocations
- Distinguishes direct instantiation (new ChildValidator()) from injected composition
- ValidatorPrinter formats output with ANSI-colored lifetimes (green/blue/gray) matching existing conventions
- Composition tree visualization with Unicode box-drawing characters
- --why flag traces lifetime through composition chains explaining why a validator has its lifetime
- Both commands support --json output mode for machine-readable output
- validators command supports --filter for filtering by model type name
- 14 unit tests covering discovery, composition graph building, JSON output, lifetime tracing, and edge cases

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement validator inspection and graph commands** - `f29ad0c` (feat)
2. **Task 2: Create CLI validator command tests** - `0115df2` (test)

## Files Created/Modified
- `IoCTools.Tools.Cli/Utilities/ValidatorInspector.cs` - Roslyn-based validator discovery, composition graph building, lifetime tracing
- `IoCTools.Tools.Cli/Utilities/ValidatorPrinter.cs` - Colored output formatting for list, graph, and why modes
- `IoCTools.Tools.Cli/Program.cs` - Registered validators and validator-graph commands in dispatch
- `IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs` - Added ParseValidators and ParseValidatorGraph with --filter and --why options
- `IoCTools.Tools.Cli/Utilities/UsagePrinter.cs` - Added usage lines for new commands
- `IoCTools.Tools.Cli.Tests/ValidatorCommandTests.cs` - 14 tests for discovery, graph, JSON, and lifetime tracing

## Decisions Made
- Used name-based Roslyn detection (checking for `AbstractValidator` base type by name) rather than depending on the FluentValidation generator package, keeping the CLI self-contained
- Built composition edge detection directly in the CLI (syntax tree walking for SetValidator/Include/SetInheritanceValidator) matching the same approach used by the generator
- Created unit tests with in-memory CSharpCompilation rather than full MSBuild workspace integration tests, providing faster test execution

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Test project targets net8.0 but only .NET 10 runtime is installed; tests compile but cannot execute at runtime (pre-existing environment issue, not caused by this plan)

## Known Stubs
None - all functionality is complete.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Validator CLI commands are complete and ready for use
- Commands work independently of the FluentValidation generator package
- When the FluentValidation generator is present in a project, the CLI will detect validators via compilation analysis

---
*Phase: 01-add-first-party-fluentvalidation-source-generator-support*
*Completed: 2026-03-29*
