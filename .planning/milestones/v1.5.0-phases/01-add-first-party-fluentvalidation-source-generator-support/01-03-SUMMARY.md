---
phase: 01-add-first-party-fluentvalidation-source-generator-support
plan: 03
subsystem: IoCTools.FluentValidation
tags: [source-generator, fluentvalidation, pipeline, registration, discovery]
dependency_graph:
  requires: [01-01, 01-02]
  provides: [validator-discovery-pipeline, validator-registration-emitter]
  affects: [IoCTools.FluentValidation, IoCTools.FluentValidation.Tests]
tech_stack:
  added: []
  patterns: [incremental-pipeline, partial-method-implementation, two-generator-test-pattern]
key_files:
  created:
    - IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/Pipeline/ValidatorPipeline.cs
    - IoCTools.FluentValidation/IoCTools.FluentValidation/CodeGeneration/ValidatorRegistrationGenerator.cs
    - IoCTools.FluentValidation/IoCTools.FluentValidation/Generator/ValidatorRegistrationEmitter.cs
    - IoCTools.FluentValidation.Tests/ValidatorDiscoveryTests.cs
    - IoCTools.FluentValidation.Tests/RegistrationTests.cs
  modified:
    - IoCTools.FluentValidation/IoCTools.FluentValidation/FluentValidationGenerator.cs
    - IoCTools.FluentValidation.Tests/IoCTools.FluentValidation.Tests.csproj
decisions:
  - "Fully qualified global:: prefixed type names in registration lines avoid using statement complexity"
  - "ValidatorPipeline returns ImmutableArray via .Collect() for batch processing in emitter"
  - "Updated test project to net10.0 for SDK compatibility with installed .NET 10 runtime"
metrics:
  duration: 149s
  completed: 2026-03-29T21:12:33Z
  tasks: 2
  files: 7
---

# Phase 01 Plan 03: Validator Discovery Pipeline and Registration Emitter Summary

Incremental pipeline discovering validators with IoCTools lifetime attributes + AbstractValidator<T> base, emitting IValidator<T> + concrete DI registrations via partial method hook.

## What Was Done

### Task 1: ValidatorPipeline, ValidatorRegistrationEmitter, ValidatorRegistrationGenerator
- Created `ValidatorPipeline.Build()` using `CreateSyntaxProvider` that filters for classes with lifetime attributes AND AbstractValidator<T> inheritance
- Created `ValidatorRegistrationGenerator` generating two registration lines per validator: `IValidator<T>` interface + concrete type
- Created `ValidatorRegistrationEmitter` that computes namespace/method prefix using identical logic to main generator's RegistrationEmitter
- Wired pipeline in `FluentValidationGenerator.Initialize()` combining validators with CompilationProvider

### Task 2: Discovery and Registration Tests (11 tests)
- 6 discovery tests: Scoped/Singleton/Transient validators discovered; non-validator class, validator without lifetime, abstract validator class NOT discovered
- 5 registration tests: correct IValidator<T>+concrete format, no non-generic IValidator, no IEnumerable<IValidationRule>, multi-validator same partial method, correct namespace derivation
- Updated test project target framework from net8.0 to net10.0 for SDK compatibility

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 | 4c28496 | feat(01-03): implement validator discovery pipeline and registration emitter |
| 2 | ed782e2 | test(01-03): add validator discovery and registration tests |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Test project target framework incompatible with installed SDK**
- **Found during:** Task 2
- **Issue:** Test project targeted net8.0 but only .NET 10 runtime available
- **Fix:** Updated IoCTools.FluentValidation.Tests.csproj TargetFramework to net10.0
- **Files modified:** IoCTools.FluentValidation.Tests/IoCTools.FluentValidation.Tests.csproj
- **Commit:** ed782e2

## Verification

- `dotnet build IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj --configuration Release` : 0 errors
- `dotnet test IoCTools.FluentValidation.Tests/` : 11 passed, 0 failed

## Known Stubs

None - all functionality is fully wired end-to-end.

## Self-Check: PASSED

- All 5 created files verified present on disk
- Both commits (4c28496, ed782e2) verified in git log
