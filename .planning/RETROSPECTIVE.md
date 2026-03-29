# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v1.5.0 — Test Fixture Generation, FluentValidation, and Documentation

**Shipped:** 2026-03-29
**Phases:** 7 | **Plans:** 24 | **Timeline:** 25 days

### What Was Built
- Test fixture source generator (IoCTools.Testing) with Mock<T> fields, CreateSut() factories, and TDIAG diagnostics
- FluentValidation source generator with validator discovery, composition graphs, anti-pattern diagnostics (IOC100-102), and CLI commands
- typeof() registration diagnostics (IOC090-094) detecting manual DI patterns
- CLI improvements: JSON output, color-coded UI, suppress command, validator inspection
- HelpLinkUri + IDE categories on all 87 original diagnostics
- Multi-page documentation (10 files, 3,420 lines)

### What Worked
- Coarse granularity (4 main phases) kept planning overhead low while delivering substantial features
- Parallel phase execution (Phases 2 and 3 ran concurrently since both only depended on Phase 1)
- Worktree-based agent isolation prevented merge conflicts during parallel plan execution
- Audit-driven gap closure: milestone audit caught 5 integration issues that were fixed in 2 targeted phases
- Name-based type detection pattern for FluentValidation avoided adding package dependencies to generators

### What Was Inefficient
- Phase 01 (FV) was added mid-milestone and had 7 plans — more granular than the original "coarse" phases, creating inconsistency
- SUMMARY.md frontmatter was inconsistently populated (39 of 64 requirements missing from frontmatter), requiring VERIFICATION.md as the authoritative source
- Two v1.5.0 milestone entries were created in MILESTONES.md (original ship + FV extension) — should have been one continuous milestone
- Some velocity metrics in STATE.md had raw timestamps instead of formatted durations

### Patterns Established
- Partial method hook pattern for optional generator extensions (FluentValidation hooks into main generator)
- Name-based Roslyn type detection for conditional library-specific code generation
- Compilation reference gating for optional helper generation
- Audit → gap closure → re-audit cycle for milestone quality assurance
- Manual diagnostic catalog over reflection for CLI tools

### Key Lessons
1. Milestone audits before completion catch real integration issues — the 5 gaps found would have shipped as bugs
2. netstandard2.0 constraints (no records, no HashCode, no ToHashSet) require constant vigilance and should be validated early in each plan
3. Documentation phases work best after all features stabilize — Phase 04 didn't need rework because it came last
4. FluentValidation integration proved the generator architecture is extensible via partial methods without tight coupling

### Cost Observations
- Model mix: ~80% opus, ~20% sonnet (quality profile)
- Sessions: ~15-20 across the milestone
- Notable: Worktree isolation enabled parallel plan execution, significantly reducing wall-clock time for multi-plan phases

---

## Cross-Milestone Trends

### Process Evolution

| Milestone | Phases | Plans | Key Change |
|-----------|--------|-------|------------|
| v1.5.0 | 7 | 24 | Audit-driven gap closure, worktree parallelization |

### Cumulative Quality

| Milestone | Tests | Diagnostics | Packages |
|-----------|-------|-------------|----------|
| v1.5.0 | ~1,814 | 102 | 5 |

### Top Lessons (Verified Across Milestones)

1. Milestone audits before archival catch integration issues that per-phase verification misses
2. netstandard2.0 constraints must be validated early — they cause cascading test failures when violated
3. Documentation should follow feature work, not run in parallel with it
