# Roadmap: IoCTools

## Milestones

- ✅ **v1.5.0** — Test Fixture Generation and Documentation (shipped 2026-03-21)

## Phases

<details>
<summary>✅ v1.5.0 (Phases 1-4) — SHIPPED 2026-03-21</summary>

- [x] Phase 1: Code Quality and Diagnostic UX (2/2 plans) — completed 2026-03-21
- [x] Phase 2: typeof() Diagnostics and CLI (5/5 plans) — completed 2026-03-21
- [x] Phase 3: Test Fixture Generation (4/4 plans) — completed 2026-03-21
- [x] Phase 4: Documentation (4/4 plans) — completed 2026-03-21

**Milestone Summary:** 4 phases, 15 plans, 40 tasks — Test fixture generation with IoCTools.Testing package, typeof() diagnostics (IOC090-094), CLI improvements, and documentation overhaul.

**Archived:** See `.planning/milestones/v1.5.0-ROADMAP.md` for full details.

</details>

## Progress

| Phase | Plans | Status |
|-------|-------|--------|
| 1-4 | 15 | ✅ Complete |
| 1 (FV) | 7 | ✅ Complete |
| 5 | TBD | Planned |
| 6 | TBD | Planned |

### Phase 1: Add first-party FluentValidation source generator support

**Goal:** Extend IoCTools with a separate IoCTools.FluentValidation generator package that discovers validators as DI citizens, refines registrations to IValidator<T> + concrete only, builds composition graphs from SetValidator/Include chains, detects anti-patterns (direct instantiation, lifetime mismatches), extends test fixtures with validation helpers, and adds CLI validator inspection.
**Requirements:** FV-01, FV-02, FV-03, FV-04, FV-05, FV-06, FV-07, FV-08
**Depends on:** Phase 0
**Plans:** 7 plans

Plans:
- [x] 01-01-PLAN.md — Project scaffolding, models, and type utilities
- [x] 01-02-PLAN.md — Main generator partial method hook for FV coordination
- [x] 01-03-PLAN.md — Validator discovery pipeline and registration emitter
- [x] 01-04-PLAN.md — Composition graph builder (SetValidator/Include/SetInheritanceValidator)
- [x] 01-05-PLAN.md — Anti-pattern diagnostics (IOC100-IOC102)
- [x] 01-06-PLAN.md — Test fixture IValidator<T> helpers
- [x] 01-07-PLAN.md — CLI validator inspection and graph commands

### Phase 5: Fix solution file and FV integration wiring

**Goal:** Fix blocking solution build failure, add IOC100-102 to DiagnosticCatalog for CLI suppress command, and resolve HelpLinkUri inconsistencies and analyzer release tracking.
**Gap Closure:** Closes BROKEN-01, BROKEN-02, tech debt from audit
**Depends on:** Phase 1
**Plans:** TBD

### Phase 6: FluentValidation documentation integration

**Goal:** Update all documentation to cover FluentValidation features — add IOC100-102 to diagnostics reference, document CLI validator commands, and document test fixture validation helpers.
**Gap Closure:** Closes MISSING-01, MISSING-02, MISSING-03 from audit
**Depends on:** Phase 5
**Plans:** TBD
