---
phase: 03-test-fixture-generation
plan: 01
subsystem: testing
tags: [source-generator, moq, test-fixtures, roslyn, net8.0]

# Dependency graph
requires: []
provides:
  - IoCTools.Testing.Abstractions package with CoverAttribute<TService>
  - IoCTools.Testing source generator with Moq dependency
  - Solution configuration for 8 projects (added 2)
affects: [03-02-fixture-pipeline, 03-03-diagnostics]

# Tech tracking
tech-stack:
  added: [Moq 4.20.72, Microsoft.CodeAnalysis.CSharp 4.5.0, Microsoft.CodeAnalysis.Analyzers 3.3.4]
  patterns: [analyzer-package-structure, net8.0-targeting]

key-files:
  created:
    - IoCTools.Testing.Abstractions/IoCTools.Testing.Abstractions.csproj
    - IoCTools.Testing.Abstractions/Annotations/CoverAttribute.cs
    - IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj
    - IoCTools.Testing/IoCTools.Testing/IoCTools.TestingGenerator.cs
  modified:
    - IoCTools.sln

key-decisions:
  - "IoCTools.Testing targets net8.0 (not netstandard2.0) since it's test-project-only"
  - "Moq 4.20.72 direct dependency - concrete, no abstraction layer"
  - "Separate Testing.Abstractions package isolates test attributes from production code"

patterns-established:
  - "Analyzer package pattern: IncludeBuildOutput=false, DevelopmentDependency=true"
  - "Generic attribute pattern: CoverAttribute<TService> where TService : class"
  - "Class-only targeting: AttributeUsage(AttributeTargets.Class, AllowMultiple = false)"

requirements-completed: [TEST-01]

# Metrics
duration: 3min
completed: 2026-03-21T19:17:14Z
---

# Phase 03 Plan 01: Test Fixture Package Structure Summary

**IoCTools.Testing package foundation with CoverAttribute<TService>, generator skeleton, and Moq 4.20.72 dependency**

## Performance

- **Duration:** 3 min (160 seconds)
- **Started:** 2026-03-21T19:14:34Z
- **Completed:** 2026-03-21T19:17:14Z
- **Tasks:** 3
- **Files modified:** 5

## Accomplishments

- Created IoCTools.Testing.Abstractions targeting net8.0 with CoverAttribute<TService> for test fixture marking
- Created IoCTools.Testing source generator with Moq 4.20.72 dependency and analyzer packaging configuration
- Added both projects to solution with proper GUIDs and all build configurations (Debug/Release, AnyCPU/x64/x86)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create IoCTools.Testing.Abstractions project with CoverAttribute** - `5edfcff` (feat)
2. **Task 2: Create IoCTools.Testing generator project with Moq dependency** - `430f29c` (feat)
3. **Task 3: Add both projects to solution and verify build** - `fed2467` (feat)

## Files Created/Modified

- `IoCTools.Testing.Abstractions/IoCTools.Testing.Abstractions.csproj` - Project file targeting net8.0 with NuGet packaging configured
- `IoCTools.Testing.Abstractions/Annotations/CoverAttribute.cs` - Generic attribute marking test classes for fixture generation
- `IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj` - Source generator project with Moq 4.20.72 dependency
- `IoCTools.Testing/IoCTools.Testing/IoCTools.TestingGenerator.cs` - IIncrementalGenerator stub (Initialize empty, awaiting 03-02)
- `IoCTools.sln` - Added 2 projects with GUIDs and build configurations

## Decisions Made

- **net8.0 targeting**: Both IoCTools.Testing projects target net8.0 (not netstandard2.0) since test projects don't need broad framework compatibility
- **Moq dependency**: Direct dependency on Moq 4.20.72 without abstraction layer - keeps generated code concrete and simple
- **Analyzer packaging**: Followed IoCTools.Generator pattern with IncludeBuildOutput=false, DevelopmentDependency=true
- **Separate abstractions package**: CoverAttribute lives in IoCTools.Testing.Abstractions so production code never references test-specific attributes

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed README.md relative path**
- **Found during:** Task 1 (IoCTools.Testing.Abstractions build)
- **Issue:** Initial ../../README.md path went up two levels, pointing to wrong directory
- **Fix:** Changed to ../README.md (one level up from project root)
- **Files modified:** IoCTools.Testing.Abstractions/IoCTools.Testing.Abstractions.csproj
- **Committed in:** 5edfcff (Task 1 commit)

**2. [Rule 3 - Blocking] Fixed README.md path for generator project**
- **Found during:** Task 2 (IoCTools.Testing build)
- **Issue:** ../../../README.md path went up three levels, pointing to wrong directory
- **Fix:** Changed to ../../README.md (two levels up from nested project directory)
- **Files modified:** IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj
- **Committed in:** 430f29c (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 blocking - file path corrections)
**Impact on plan:** Both auto-fixes were necessary for builds to succeed. No scope creep.

## Issues Encountered

- NU5128 warning for analyzer-only package (expected, no lib folder needed for analyzers)
- Solution build shows pre-existing errors in IoCTools.Sample (intentional diagnostic examples), not related to new projects

## Verification

- CoverAttribute is public with `[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]`
- IoCTools.Testing.csproj has Moq 4.20.72 PackageReference
- Both projects have Version 1.5.0 and GeneratePackageOnBuild=true
- IoCTools.Testing has IncludeBuildOutput=false and DevelopmentDependency=true
- Solution contains 8 projects (was 6, added 2)
- Both new projects build successfully with 0 errors

## Next Phase Readiness

- CoverAttribute<TService> available for test class annotation
- Generator stub ready for pipeline implementation in 03-02
- Moq dependency available for generated code to reference Mock<T>
- No blockers - ready for test fixture pipeline implementation

## Self-Check: PASSED

All created files exist and all commits verified.

---
*Phase: 03-test-fixture-generation*
*Plan: 01*
*Completed: 2026-03-21*
