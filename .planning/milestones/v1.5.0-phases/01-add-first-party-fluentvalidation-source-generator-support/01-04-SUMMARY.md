---
phase: 01-add-first-party-fluentvalidation-source-generator-support
plan: 04
subsystem: source-generator
tags: [fluentvalidation, roslyn, composition-graph, source-generator, netstandard2.0]

requires:
  - phase: 01-add-first-party-fluentvalidation-source-generator-support
    provides: ValidatorClassInfo model, ValidatorPipeline discovery, FluentValidationTypeChecker

provides:
  - CompositionEdge model for validator composition relationships
  - CompositionType enum (SetValidator, Include, SetInheritanceValidator)
  - CompositionGraphBuilder that parses validator bodies for composition invocations
  - ValidatorClassInfo extended with ImmutableArray<CompositionEdge> for pipeline coherence

affects: [01-05, 01-06, 01-07]

tech-stack:
  added: []
  patterns: [composition-graph-builder, syntax-tree-walking-for-invocations, direct-vs-injected-detection]

key-files:
  created:
    - IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/CompositionGraph/CompositionEdge.cs
    - IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/CompositionGraph/CompositionType.cs
    - IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/CompositionGraph/CompositionGraphBuilder.cs
  modified:
    - IoCTools.FluentValidation/IoCTools.FluentValidation/Models/ValidatorClassInfo.cs
    - IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/Pipeline/ValidatorPipeline.cs

key-decisions:
  - "Option A chosen: CompositionEdges embedded in ValidatorClassInfo for pipeline coherence rather than separate pipeline"
  - "Unchecked multiply-add hash for netstandard2.0 compatibility in CompositionEdge"

patterns-established:
  - "Composition graph builder: Walk DescendantNodes().OfType<InvocationExpressionSyntax>() for method detection"
  - "Direct instantiation detection: ObjectCreationExpressionSyntax vs field/parameter/property symbol resolution"
  - "Generator safety: All semantic model calls guarded with null checks and catch (Exception) when not OOM/SOF"

requirements-completed: [FV-05]

duration: 2min
completed: 2026-03-29
---

# Phase 01 Plan 04: Validator Composition Graph Summary

**CompositionGraphBuilder parses SetValidator/Include/SetInheritanceValidator invocations to build directed edges distinguishing direct instantiation from DI-injected child validators**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-29T21:15:19Z
- **Completed:** 2026-03-29T21:17:18Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- CompositionEdge readonly struct with IEquatable and manual GetHashCode for netstandard2.0
- CompositionType enum covering all three FluentValidation composition patterns
- CompositionGraphBuilder walks validator syntax trees for SetValidator, Include, and SetInheritanceValidator invocations
- ResolveChildValidatorType handles ObjectCreationExpressionSyntax (direct), field/parameter/property/local symbols (injected)
- SetInheritanceValidator handler walks lambda bodies for .Add<T>() calls with generic type extraction
- ValidatorClassInfo extended with ImmutableArray<CompositionEdge> integrated into pipeline transform
- All 11 existing tests pass unchanged

## Task Commits

Each task was committed atomically:

1. **Task 1: Create CompositionEdge model and CompositionType enum** - `7240548` (feat)
2. **Task 2: Implement CompositionGraphBuilder and wire into pipeline** - `df4376e` (feat)

## Files Created/Modified
- `Generator/CompositionGraph/CompositionType.cs` - Enum for SetValidator, Include, SetInheritanceValidator
- `Generator/CompositionGraph/CompositionEdge.cs` - Directed edge model with parent/child names, composition type, direct instantiation flag, Location
- `Generator/CompositionGraph/CompositionGraphBuilder.cs` - Syntax tree walker building edges from validator class bodies
- `Models/ValidatorClassInfo.cs` - Added ImmutableArray<CompositionEdge> property and updated equality
- `Generator/Pipeline/ValidatorPipeline.cs` - Calls BuildEdges during transform and passes to ValidatorClassInfo

## Decisions Made
- Chose Option A (embed edges in ValidatorClassInfo) over Option B (separate pipeline) for pipeline coherence and simpler data flow
- FluentValidationGenerator.cs required no changes since composition edges flow through existing pipeline

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Composition graph data is available in ValidatorClassInfo.CompositionEdges for Plan 05 diagnostics
- Edge model supports all three composition patterns with direct/injected distinction
- Location data attached to edges for diagnostic reporting

---
*Phase: 01-add-first-party-fluentvalidation-source-generator-support*
*Completed: 2026-03-29*
