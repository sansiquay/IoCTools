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

**Next:** Use `/gsd:new-milestone` to plan the next milestone.

### Phase 1: Add first-party FluentValidation source generator support

**Goal:** Extend IoCTools with a separate IoCTools.FluentValidation generator package that discovers validators as DI citizens, refines registrations to IValidator<T> + concrete only, builds composition graphs from SetValidator/Include chains, detects anti-patterns (direct instantiation, lifetime mismatches), extends test fixtures with validation helpers, and adds CLI validator inspection.
**Requirements:** FV-01, FV-02, FV-03, FV-04, FV-05, FV-06, FV-07, FV-08
**Depends on:** Phase 0
**Plans:** 7 plans

Plans:
- [ ] 01-01-PLAN.md — Project scaffolding, models, and type utilities
- [ ] 01-02-PLAN.md — Main generator partial method hook for FV coordination
- [ ] 01-03-PLAN.md — Validator discovery pipeline and registration emitter
- [ ] 01-04-PLAN.md — Composition graph builder (SetValidator/Include/SetInheritanceValidator)
- [ ] 01-05-PLAN.md — Anti-pattern diagnostics (IOC100-IOC102)
- [ ] 01-06-PLAN.md — Test fixture IValidator<T> helpers
- [ ] 01-07-PLAN.md — CLI validator inspection and graph commands
