# IoCTools 1.5.1 Release Design

Date: 2026-04-12
Status: Approved design checkpoint
Scope: Define the first real public `1.5.x` release after the tagged-but-not-published `v1.5.0` state, and freeze the implementation scope for that release.

## Purpose

IoCTools is in an awkward release state:

- the repository already has a `v1.5.0` tag
- local `main` contains additional `1.5.0`-line work that has not been pushed
- the public NuGet packages for `IoCTools.Abstractions` and `IoCTools.Generator` still top out at `1.4.0`
- the current GitHub Actions publish workflow only publishes two packages, not the full product surface

The correct response is not to rewrite history.
The correct response is to treat the existing `v1.5.0` tag as a historical internal attempt and ship the first real public release as `1.5.1`.

## Current State

### Repository State

- `origin/main` is behind the current local `main`
- the repo contains completed `1.5.0`-line work for evidence, validator JSON, suppression JSON, release artifacts, and documentation improvements
- the repo now also has replacement `docs/superpowers` planning docs and has removed the legacy repo-root `.planning/` tree

### Public Release State

At the time of this design:

- `IoCTools.Generator` on NuGet is published through `1.4.0`
- `IoCTools.Abstractions` on NuGet is published through `1.4.0`
- the `v1.5.0` git tag exists remotely
- the visible `CI Main Branch` runs around the `v1.5.0` period did not establish a clean published release path

### Packaging State

The release surface is inconsistent:

- `IoCTools.Abstractions` is versioned `1.5.0`
- `IoCTools.Generator` is versioned `1.5.0`
- `IoCTools.Tools.Cli` is versioned `1.5.0`
- `IoCTools.Testing` is versioned `1.5.0`
- `IoCTools.FluentValidation` is versioned `1.0.0`
- multiple package URLs still point at the old repository owner path
- the main publish workflow only pushes `IoCTools.Abstractions` and `IoCTools.Generator`

## Design Goals

- ship the first real public `1.5.x` release as `1.5.1`
- avoid deleting or retagging the existing remote `v1.5.0`
- make the release process match the actual package surface
- include only work that is both valuable and realistically supportable inside one release
- keep the release story easy to explain to users and future maintainers

## Non-Goals

- reuse or rewrite the remote `v1.5.0` tag
- publish a kitchen-sink `1.5.1` that absorbs unrelated cleanup backlog
- turn `1.5.1` into a `2.0`-style compatibility break
- broaden open-generic support beyond the common supported case needed for this release

## Chosen Release Posture

### Versioning

The release version is `1.5.1`.

Reason:

- the repository has already declared `v1.5.0`
- public NuGet has not actually received the `1.5.0` packages
- `1.5.1` preserves history instead of rewriting it
- users get a clear first public `1.5.x` artifact with a coherent changelog

### Package Set

`1.5.1` should ship as a coherent package family, not a partial release.

The release should cover:

- `IoCTools.Abstractions`
- `IoCTools.Generator`
- `IoCTools.Tools.Cli`
- `IoCTools.Testing`
- `IoCTools.FluentValidation`

If a package is part of the supported product and versioned for the release line, the workflow should know how to publish it or intentionally document why it is excluded.

## Scope For 1.5.1

`1.5.1` includes four workstreams.

### 1. Release And Publish Repair

- push the real `main` branch state
- fix the GitHub Actions path so package publication matches the intended product surface
- ensure release gating runs stable restore, build, and test steps before any publish step
- make versioning and changelog text consistent with `1.5.1`

### 2. Metadata And Documentation Hygiene

- correct stale repository URLs and package metadata
- align README and docs with the actual product surface
- keep the explicit `DependsOn`-first and never-`Inject` guidance
- keep `docs/superpowers` as the living planning surface

### 3. Generator Resilience Hardening

- remove the remaining silent-degrade paths that can hide generator failure
- make interface-discovery and constructor-generation failure modes observable
- back the new behavior with targeted regression tests and full-suite verification

### 4. Narrow Open-Generic Support

This release includes only the common case:

- support the standard `typeof(IFoo<>), typeof(Foo<>)` registration shape end-to-end
- align generation, diagnostics, sample usage, CLI evidence, and docs around that supported case

This does not authorize a broad generic-registration redesign.
If unusual generic scenarios still require later work, they stay out of `1.5.1`.

## Alternatives Considered

### Alternative A: Retcon Everything Into `1.5.0`

Rejected.

The remote `v1.5.0` tag already exists.
Reusing it would make the release story harder to reason about and would blur the line between a historical tag and the first public package release.

### Alternative B: Ship A Stabilization-Only `1.5.1`

Viable, but not chosen.

It would reduce release risk, but it would leave the open-generic gap in the middle of a release line that is already being cleaned up and clarified.

### Alternative C: Ship A Kitchen-Sink `1.5.1`

Rejected.

That would mix publish repair with too much speculative cleanup and create unnecessary release risk.

## Release Criteria

`1.5.1` is ready only when all of the following are true:

- `main` is pushed to `origin/main`
- the publish workflow covers the intended package set
- package metadata is internally consistent
- targeted tests for resilience and open generics pass
- the full solution test suite passes
- release docs and changelog clearly describe `1.5.1`
- the release can be explained without hand-waving around the old `v1.5.0` tag

## Risks And Controls

### Risk: Open-generic scope grows during implementation

Control:

- keep support limited to the common `typeof(IFoo<>), typeof(Foo<>)` path
- require sample, docs, diagnostics, and tests to agree on the exact supported shape

### Risk: Workflow changes publish an incomplete or inconsistent package set

Control:

- treat workflow repair as first-class release work, not follow-up
- verify package list, version alignment, and publish conditions before pushing

### Risk: Silent generator-failure fixes change observable behavior

Control:

- write failure-path regression tests first
- document any newly visible diagnostics or error posture changes

## Acceptance Criteria

- a new public `1.5.1` release can be shipped without rewriting the historical `v1.5.0` tag
- the release scope is limited to publish repair, metadata/docs, resilience hardening, and narrow open-generic support
- the package and workflow story is coherent enough to automate confidently
- the release design is specific enough to drive a concrete implementation plan
