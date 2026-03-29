# Phase 1: Add First-Party FluentValidation Source Generator Support - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-03-29
**Phase:** 01-add-first-party-fluentvalidation-source-generator-support
**Areas discussed:** Generation Scope, Attribute & API Design, Package Architecture, DI Integration, Composition Graph

---

## Round 1: Initial Gray Area Selection

All four areas selected: Generation Scope, Attribute & API Design, Package Architecture, Integration with IoCTools DI.

User requested deep research: "run each discussion point through a separate agent which audits the codebase, docs, tests, everything, giving the agent a sense of the spirit of the project."

Four parallel research agents spawned — one per area. Each produced 5-10 discussion points with evidence-based recommendations.

---

## Registration Method

| Option | Description | Selected |
|--------|-------------|----------|
| Separate Add{Assembly}Validators() | New extension method, user calls both | |
| Merge into AddServices() | Validators flow into existing registration | |
| Internal coordination, single call | Separate package contributes internally to existing AddServices() | ✓ |

**User's choice:** "if we make this a separate package, then it will still be automatically integrated into a singular call — the only point of making it public is to let the user consent, but they brought in the package and added ioc tools primitives to their validators, which means they did consent already"

---

## Attribute Design

| Option | Description | Selected |
|--------|-------------|----------|
| Existing attributes only | [Scoped]/[Singleton]/[Transient] + AbstractValidator<T> base class | ✓ |
| New [ValidatorFor<T>] attribute | Explicit marker, requires new Abstractions package | |
| Convention-only (no attribute) | Auto-discover all AbstractValidator<T>, violates explicit-intent | |

**User's choice:** "the only reason we'd want new attributes is because we want to offer new features for them, or the featureset we already offer for normal services is not sufficient... there is no reason to repeat ourselves"

**Notes:** User expressed disappointment that this question was asked — the answer was obvious from IoCTools' own design philosophy. Validators are services; existing attributes handle them.

---

## Target Framework

| Option | Description | Selected |
|--------|-------------|----------|
| netstandard2.0 | Matches IoCTools.Generator, broadest compatibility | ✓ |
| net8.0 | Modern C# features, matches IoCTools.Testing | |

**User's choice:** netstandard2.0, same constraints. "if we want to get modern C#, we should be doing it across the board"

---

## Scope Reframing: DI Lens

User pushed back on FluentValidation analysis features (unvalidated properties, rule strength, async/sync detection): "those sound nice for fluent validation, but what do they have to do with ioc tools?"

This reframed the entire feature set. Features were re-evaluated strictly through the DI scope:
- Registration refinement — IN (DI correctness)
- Missing validator diagnostic — ALREADY WORKS (IOC001)
- Lifetime mismatch — ALREADY WORKS (IOC012/IOC015)
- Rule analysis — OUT (FluentValidation linting)
- Child validator new detection — IN (DI anti-pattern)
- Test fixtures — IN (DI dependency mocking)
- CLI — IN (DI graph inspection)

---

## Deep DI-Focused Audit

A fifth research agent was dispatched to study FluentValidation's DI surface deeply. Key findings:

1. **Registration is a bug today** — InterfaceDiscovery over-registers FluentValidation internal interfaces that FV's own DI deliberately skips
2. **SetValidator(new ...) is the most common FV anti-pattern** — bypasses DI, dependencies not resolved
3. **Include(new ...) is the same anti-pattern** — bundle with SetValidator detection
4. **Test fixtures are modest but real** — 3-5 lines saved per test class
5. **CLI is low priority** — information already visible in ioc-tools services

---

## Composition Graph

| Option | Description | Selected |
|--------|-------------|----------|
| Parse validator bodies for SetValidator/Include/SetInheritanceValidator | Build composition graph as DI edges | ✓ |
| Constructor-only dependency analysis | Only see what's in the constructor | |

**User's choice:** "will we be parsing the fluent validator 'stack' to understand it? for graph purposes" — confirmed yes, composition graph is in scope.

**Notes:** This enhances all other features: richer anti-pattern diagnostics (show full chain being bypassed), CLI (trace lifetime constraints through composition), and lifetime validation (propagation through SetValidator chains).

---

## CLI and Test Helpers Scope

User explicitly stated CLI features and test helpers are "in scope, not future work" — correcting the research agents' recommendation to defer.

---

## Claude's Discretion

- Diagnostic ID numbering scheme
- Exact CLI command names and output format
- Internal composition graph data structure
- Partial class/method coordination mechanism between generators
- Registration refinement mechanism (general-purpose vs. FluentValidation-specific)

## Deferred Ideas

- FluentValidation linting features (property coverage, rule strength, etc.) — belongs in a FV-specific analyzer
- MediatR ValidationBehavior auto-wiring — separate framework
- Empty validator scaffolding via CLI — future CLI enhancement
- Modern C# across the board — separate milestone
