# Phase 4: Documentation - Context

**Gathered:** 2026-03-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Documentation overhaul — evaluate single-doc vs multi-page structure, update all docs for v1.5.0 features (typeof() diagnostics, CLI improvements, test fixture generation), ensure all HelpLinkUris resolve, document IoCTools.Testing package comprehensively. No new features — this phase organizes and presents what already exists plus new v1.5.0 capabilities.

</domain>

<decisions>
## Implementation Decisions

### Documentation structure
- **D-01:** Hybrid approach — lean README.md (~250-300 lines) + focused /docs/ directory
- **D-02:** README focuses on: what, why, install, quickstart, key benefits, links to deeper docs
- **D-03:** /docs/ directory contains: getting-started.md, attributes.md, diagnostics.md (existing), testing.md (NEW), cli-reference.md (NEW), configuration.md, migration.md, platform-constraints.md
- **D-04:** Multi-page structure enables shareable URLs, easier maintenance, progressive disclosure
- **D-05:** Diagnostic reference stays in single docs/diagnostics.md file (searchable, not split per diagnostic)

### Getting started approach
- **D-06:** Hybrid tutorial-reference — progressive disclosure from 30-second to 5-minute to conceptual model
- **D-07:** 30-second version for DI-savvy users (install, annotate, register)
- **D-08:** 5-minute walkthrough building a complete service with explanation
- **D-09:** "What you eliminated" before/after comparison showing value
- **D-10:** Conceptual model explaining how IoCTools thinks
- **D-11:** Strategic links from tutorial to reference sections

### IoCTools.Testing documentation placement
- **D-12:** Dedicated /docs/testing.md (300-400 lines) as primary testing package documentation
- **D-13:** Main README includes 20-line "Testing with IoCTools" teaser section with link
- **D-14:** Package README (IoCTools.Testing.Abstractions/) minimal with NuGet badge + link
- **D-15:** Cross-linking: main README ↔ /docs/testing.md ↔ package README

### Diagnostics reference format
- **D-16:** Keep two-file structure (README table + docs/diagnostics.md)
- **D-17:** README shows curated Error-only table (~30 rows) with link to full reference
- **D-18:** docs/diagnostics.md enhanced with: category navigation sections, severity badges, cross-references between related diagnostics, category index tables
- **D-19:** HelpLinkUri workflow unchanged — current anchor format (#ioc001) works correctly

### v1.5.0 feature integration
- **D-20:** Add "What's New in v1.5.0" section after Installation, before Getting Started (max 4 bullets with anchors)
- **D-21:** Create CHANGELOG.md for version history (industry standard, keeps README current)
- **D-22:** IoCTools.Testing gets new dedicated section in README (80-100 lines) placed after Attribute Reference, before Diagnostics Reference
- **D-23:** CLI enhancements integrated into existing CLI section (no new section)
- **D-24:** typeof() diagnostics added to existing diagnostic table (+6 rows)
- **D-25:** README shows current version only; historical content moved to CHANGELOG

### Platform constraints documentation
- **D-26:** Multi-location strategy — README.md "Platform Support" section + /docs/platform-constraints.md + CONTRIBUTING.md reference
- **D-27:** README gets 2-3 sentence "Platform Support" section answering "Will this work in my project?"
- **D-28:** /docs/platform-constraints.md (200-300 lines) with full technical details, workarounds, feature request guidance
- **D-29:** Key distinction emphasized: netstandard2.0 constrains generator internally, NOT user's service code
- **D-30:** Progressive disclosure — user-facing (README) → contributor-facing (docs/) → internal (CLAUDE.md)

### Claude's Discretion
- Exact wording of "What's New" bullets
- Specific code examples for getting started tutorial
- Visual formatting (badges, tables, collapsible sections)
- docs/ directory file naming conventions

</decisions>

<specifics>
## Specific Ideas

- Hybrid structure balances new user simplicity with existing user discoverability
- Error-only diagnostic table reduces cognitive load (30 rows vs 94)
- "What's New" section replaced each release (v1.6.0 overwrites v1.5.0)
- Generator vs. consumer code distinction is critical for platform constraints
- Links with anchors enable navigation without overwhelming
- CHANGELOG.md follows conventional commits format (Added, Changed, Fixed)

</specifics>

<canonical_refs>
## Canonical References

### Existing documentation structure
- `README.md` — Current 540-line comprehensive documentation
- `docs/diagnostics.md` — 27KB diagnostic reference with anchors
- `CLAUDE.md` — Project instructions with platform constraints section

### Phase 1 decisions on HelpLinkUri strategy
- `.planning/phases/01-code-quality-diagnostic-ux/01-CONTEXT.md` — D-01 through D-04 establish GitHub anchor pattern
- All diagnostic HelpLinkUris point to docs/diagnostics.md#iocXXX format

### Phase 3 test fixture decisions
- `.planning/phases/03-test-fixture-generation/03-CONTEXT.md` — Testing package activation, fixture structure, diagnostics (TDIAG-01 through TDIAG-05)
- IoCTools.Testing.Abstractions/Annotations/CoverAttribute.cs — Attribute definition
- IoCTools.Sample/TestingExamples.cs — Sample usage (commented)

### v1.5.0 feature definitions
- `ROADMAP.md` — Phase 2 (typeof diagnostics, CLI improvements), Phase 3 (test fixture generation)
- `REQUIREMENTS.md` — DOC-01 through DOC-09, DIAG-01 through DIAG-07, TEST-01 through TEST-11

### Ecosystem patterns (external references)
- AutoMapper documentation — Hybrid tutorial/reference structure
- AutoFixture — Separate test package documentation pattern
- Moq.AutoMocker — Test augmentation package with dedicated docs

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `docs/diagnostics.md` — Existing single-file diagnostic reference (proven pattern)
- README.md structure — Well-established sections that can be extracted to /docs/
- CLAUDE.md "Platform Constraints" section — Comprehensive internal constraints documentation

### Established Patterns
- GitHub markdown anchors for HelpLinkUri (#ioc001 pattern)
- Diagnostic descriptor pattern with HelpLinkUri, category, severity
- CLI command table with descriptions and examples

### Integration Points
- README.md will be restructured (content moved to /docs/, not deleted)
- docs/ directory will be created with 8-9 new markdown files
- CHANGELOG.md will be created at repository root
- CONTRIBUTING.md will be updated with platform constraints reference

</code_context>

<deferred>
## Deferred Ideas

- DocFX or full documentation site — Over-engineering at current scale; markdown in repo is sufficient
- API documentation generation via XML doc comments — Not in scope for this phase
- Video tutorials or walkthrough content — Deferred based on user demand
- Internationalization/translation — Deferred until documentation stabilizes

</deferred>

---

*Phase: 04-documentation*
*Context gathered: 2026-03-21*
