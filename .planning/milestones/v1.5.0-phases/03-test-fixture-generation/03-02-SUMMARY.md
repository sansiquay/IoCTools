---
phase: 03-test-fixture-generation
plan: 02
subsystem: testing
tags: [source-generator, test-fixtures, moq, roslyn, incremental-generator]

# Dependency graph
requires:
  - phase: 03-test-fixture-generation
    plan: 01
    provides: [IoCTools.Testing project structure, CoverAttribute, IoCToolsTestingGenerator stub]
provides:
  - Test fixture generator pipeline discovering [Cover<T>] test classes
  - Mock<T> field generation with inline initialization
  - CreateSut() factory method generation
  - Setup{Dependency} typed helper method generation
  - Configuration-specific helpers (IConfiguration, IOptions<T>)
affects: [03-03, sample-tests]

# Tech tracking
tech-stack:
  added: [Moq 4.20.72]
  patterns:
  - Roslyn IIncrementalGenerator pipeline pattern
  - Partial class augmentation for test fixtures
  - Mock field naming conventions (interface prefix stripping)
  - Configuration parameter detection and specialized helpers

key-files:
  created:
  - IoCTools.Testing/IoCTools.Testing/Models/TestClassInfo.cs
  - IoCTools.Testing/IoCTools.Testing/Utilities/TypeNameUtilities.cs
  - IoCTools.Testing/IoCTools.Testing/Generator/Pipeline/TestFixturePipeline.cs
  - IoCTools.Testing/IoCTools.Testing/Analysis/ConstructorReader.cs
  - IoCTools.Testing/IoCTools.Testing/CodeGeneration/FixtureEmitter.cs
  modified:
  - IoCTools.Testing/IoCTools.Testing/IoCToolsTestingGenerator.cs

key-decisions:
  - "Nullable struct handling: Use explicit (TestClassInfo?)null casts for pipeline filtering"
  - "GeneratedCodeAttribute detection: Prioritize generated constructor over manual constructors"
  - "Configuration parameter detection: String-based type name matching for IOptions<T> variants"
  - "Interface prefix stripping: Handle both I and II prefixes for clean mock naming"

patterns-established:
  - "Pipeline pattern: CreateSyntaxProvider -> Where -> Select -> Collect -> SelectMany for deduplication"
  - "Code generation pattern: StringBuilder with namespace, usings, class declaration, members, closing braces"
  - "Mock field naming: _mock{SimpleTypeName} with interface prefix stripping and generic flattening"
  - "Setup helper naming: Setup{SimpleTypeName} matching mock field naming"

requirements-completed: [TEST-02, TEST-03, TEST-04, TEST-05, TEST-06, TEST-07, TEST-08, TEST-09, TEST-10]

# Metrics
duration: 2min
completed: 2026-03-21T19:21:35Z
---

# Phase 03 Plan 02: Test Fixture Generator Summary

**Test fixture generator with Mock<T> field generation, CreateSut() factories, and typed Setup{Dependency} helpers for [Cover<T>] test classes**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-21T19:18:37Z
- **Completed:** 2026-03-21T19:21:35Z
- **Tasks:** 5
- **Files modified:** 6

## Accomplishments

- Created complete test fixture generator pipeline discovering `[Cover<T>]` test classes
- Implemented Mock<T> field generation with inline initialization (= new())
- Implemented CreateSut() factory method generating service constructor calls
- Implemented typed Setup{Dependency} helper methods for each constructor parameter
- Added configuration-specific helpers for IConfiguration and IOptions<T> parameters
- Established mock naming conventions with interface prefix stripping (IUserService -> _mockUserService)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create TestClassInfo model and TypeNameUtilities** - `70430a6` (feat)
2. **Task 2: Create TestFixturePipeline for [Cover<T>] discovery** - `1681909` (feat)
3. **Task 3: Create ConstructorReader for dependency extraction** - `93324b0` (feat)
4. **Task 4: Create FixtureEmitter for test fixture code generation** - `e3750e9` (feat)
5. **Task 5: Wire up pipeline and emitter in IoCToolsTestingGenerator** - `889c526` (feat)

**Auto-fix:** `569c9cb` (fix) - nullable reference warning

## Files Created/Modified

- `IoCTools.Testing/IoCTools.Testing/Models/TestClassInfo.cs` - Test class + service symbol pair struct
- `IoCTools.Testing/IoCTools.Testing/Utilities/TypeNameUtilities.cs` - Mock field and setup method naming helpers
- `IoCTools.Testing/IoCTools.Testing/Generator/Pipeline/TestFixturePipeline.cs` - [Cover<T>] discovery pipeline
- `IoCTools.Testing/IoCTools.Testing/Analysis/ConstructorReader.cs` - Constructor parameter extraction with GeneratedCodeAttribute detection
- `IoCTools.Testing/IoCTools.Testing/CodeGeneration/FixtureEmitter.cs` - Fixture code generation with Mock<T> fields, CreateSut(), and helpers
- `IoCTools.Testing/IoCTools.Testing/IoCToolsTestingGenerator.cs` - Main generator wiring pipeline to emitter

## Decisions Made

1. **Nullable struct casting in pipeline** - Use explicit `(TestClassInfo?)null` casts instead of returning null directly, enabling `.Where(x => x.HasValue)` filtering pattern consistent with ServiceClassPipeline
2. **GeneratedCodeAttribute detection priority** - Prioritize constructors with `[GeneratedCode]` attribute over manual constructors to ensure fixture uses the IoCTools-generated constructor with complete dependency wiring
3. **String-based configuration detection** - Use `ToDisplayString()` matching for IConfiguration/IOptions<T> detection rather than symbol comparison, providing reliable detection across different Microsoft.Extensions versions
4. **Interface prefix stripping for naming** - Strip both `I` and `II` prefixes from interface names (IUserRepository -> UserRepository) for cleaner mock field and setup method names

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed missing using directives**
- **Found during:** Task 1 (TypeNameUtilities creation)
- **Issue:** Missing `System.Linq` and `System` usings caused compilation errors (Select extension, StringComparison)
- **Fix:** Added `using System;` and `using System.Linq;` to TypeNameUtilities.cs
- **Files modified:** IoCTools.Testing/IoCTools.Testing/Utilities/TypeNameUtilities.cs
- **Verification:** Build succeeded after adding usings
- **Committed in:** `70430a6` (part of Task 1 commit)

**2. [Rule 1 - Bug] Fixed SyntaxKind.Modifiers usage**
- **Found during:** Task 2 (TestFixturePipeline creation)
- **Issue:** `m.IsKind(SyntaxKind.PartialKeyword)` doesn't exist, should use `m.Kind() == SyntaxKind.PartialKeyword`
- **Fix:** Changed `m.IsKind()` to `m.Kind() ==` for proper SyntaxToken comparison
- **Files modified:** IoCTools.Testing/IoCTools.Testing/Generator/Pipeline/TestFixturePipeline.cs
- **Verification:** Build succeeded after fix
- **Committed in:** `1681909` (part of Task 2 commit)

**3. [Rule 1 - Bug] Fixed nullable struct return handling**
- **Found during:** Task 2 (TestFixturePipeline creation)
- **Issue:** Cannot return null for non-nullable struct, need explicit nullable cast
- **Fix:** Changed all `return null;` to `return (TestClassInfo?)null;`
- **Files modified:** IoCTools.Testing/IoCTools.Testing/Generator/Pipeline/TestFixturePipeline.cs
- **Verification:** Build succeeded after fix
- **Committed in:** `1681909` (part of Task 2 commit)

**4. [Rule 1 - Bug] Fixed missing using directives in FixtureEmitter**
- **Found during:** Task 4 (FixtureEmitter creation)
- **Issue:** Missing `System.Collections.Generic`, `System.Collections.Immutable`, `System.Linq` caused compilation errors
- **Fix:** Added missing using directives to FixtureEmitter.cs
- **Files modified:** IoCTools.Testing/IoCTools.Testing/CodeGeneration/FixtureEmitter.cs
- **Verification:** Build succeeded after adding usings
- **Committed in:** `e3750e9` (part of Task 4 commit)

**5. [Rule 1 - Bug] Fixed variable name typo in FixtureEmitter**
- **Found during:** Task 4 (FixtureEmitter creation)
- **Issue:** Lines 83-84 used `p.Type` instead of `param.Type` (copy-paste from Select lambda)
- **Fix:** Changed `p.Type` to `param.Type` on lines 83-84
- **Files modified:** IoCTools.Testing/IoCTools.Testing/CodeGeneration/FixtureEmitter.cs
- **Verification:** Build succeeded after fix
- **Committed in:** `e3750e9` (part of Task 4 commit)

**6. [Rule 1 - Bug] Fixed nullable reference warning**
- **Found during:** Final verification
- **Issue:** CS8604 warning for possible null reference in `namespaces.Add(current.ContainingNamespace.ToString())`
- **Fix:** Extract to `var ns = current.ContainingNamespace?.ToString()` and add null check before Add
- **Files modified:** IoCTools.Testing/IoCTools.Testing/CodeGeneration/FixtureEmitter.cs
- **Verification:** Build succeeded with no nullable warnings
- **Committed in:** `569c9cb`

---

**Total deviations:** 6 auto-fixed (all Rule 1 - bugs)
**Impact on plan:** All auto-fixes were necessary compilation corrections. No scope creep, all functionality delivered as specified.

## Issues Encountered

- **SyntaxToken API confusion** - Initially used `m.IsKind()` which doesn't exist for SyntaxToken, corrected to `m.Kind() ==`
- **Nullable struct pattern** - Required explicit nullable casting for struct returns in pipeline lambda, learned from ServiceClassPipeline pattern
- **Variable name shadowing** - Copy-paste from Select lambda left `p.Type` references that needed to be `param.Type`

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Test fixture generator pipeline complete and functional
- Ready for 03-03: Analyzer diagnostics for manual mock/SUT boilerplate detection
- Sample tests can be created to validate generated fixtures work with real service classes
- Configuration injection helpers support IConfiguration, IOptions<T>, IOptionsSnapshot<T>, IOptionsMonitor<T>

---
*Phase: 03-test-fixture-generation*
*Plan: 02*
*Completed: 2026-03-21*
