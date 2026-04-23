# IoCTools Metadata And Doc Hygiene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to execute this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clean the remaining package metadata drift and establish `docs/superpowers` plus the user-facing docs as the only current documentation truth after the legacy `.planning/` removal.

**Architecture:** Treat this as ownership cleanup, not a broad content rewrite. Fix the remaining incorrect NuGet metadata, refresh docs that still reflect stale planning-era statements, and make sure the repo no longer relies on deleted `.planning/` files for current project understanding.

**Tech Stack:** SDK-style `.csproj` metadata, markdown docs, xUnit verification where packaging or CLI output changes warrant it, NuGet pack validation.

---

### Task 1: Fix the remaining package metadata drift

**Files:**
- Modify: `IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj`
- Review: other package `.csproj` files for owner/url/version consistency
- Modify: `README.md`

- [ ] Update the FluentValidation package URLs and ownership metadata to match the current repository identity.
- [ ] Audit the remaining packable projects so package metadata is internally consistent across the release line.
- [ ] Re-pack the affected packages to ensure the cleanup does not introduce new warnings.

Run:

```bash
env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj -c Release -o artifacts
```

### Task 2: Replace stale planning-era statements in living docs

**Files:**
- Modify: `README.md`
- Modify: `docs/getting-started.md`
- Modify: `docs/migration.md`
- Modify: `docs/testing.md`
- Modify: `docs/cli-reference.md`
- Modify: `CHANGELOG.md`

- [ ] Remove any stale references to old package targets, outdated command counts, or outdated support posture that came from the deleted planning documents.
- [ ] Make sure the docs reflect the current `1.5.x` line, the evidence command, and the never-`Inject` guidance.
- [ ] Keep the docs concise and user-facing instead of recreating internal roadmap prose.

### Task 3: Delete the repo-root `.planning/` folder and keep the repo coherent afterward

**Files:**
- Remove: `.planning/`
- Review: `CLAUDE.md` only if it directly points readers to files that no longer exist

- [ ] Remove the repo-root `.planning/` directory once the replacement docs exist.
- [ ] Check for broken references in tracked markdown docs after deletion.
- [ ] Leave `.claude/worktrees/` alone unless separately requested; this phase is about the repo-root planning surface.

### Task 4: Verify packaging and documentation integrity

**Files:**
- Verify all modified docs and metadata files

- [ ] Run `git diff --check`.
- [ ] Run the relevant pack commands for any changed packable project metadata.
- [ ] Run a targeted search for stale `.planning/` references in tracked docs.
- [ ] Run full solution tests if any code or CLI behavior changed while cleaning docs or metadata.

Run:

```bash
git diff --check
rg -n "\.planning/" README.md docs CLAUDE.md
env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj -c Release -o artifacts
```

## Acceptance Criteria

- no repo-root `.planning/` directory remains
- package metadata points at the current repository identity
- user-facing docs do not depend on deleted planning files
- `docs/superpowers` contains the forward-looking implementation plans that replaced the deleted planning tree
