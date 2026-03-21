# Phase 4: Documentation - Research

**Researched:** 2026-03-21
**Domain:** Technical documentation for .NET source generator library with test augmentation package
**Confidence:** HIGH

## Summary

Phase 4 transforms IoCTools from a single-README project to a multi-page documentation structure while maintaining the 538-line README as a concise landing page. The key challenge is balancing new user onboarding (5-minute path to first service) with comprehensive reference material for 94 diagnostics (IOC001-IOC094 + TDIAG-01 through TDIAG-05). The CONTEXT.md decisions establish a hybrid approach: lean README + focused `/docs/` directory with shareable URLs, progressive disclosure, and dedicated IoCTools.Testing documentation.

**Primary recommendation:** Follow the CONTEXT.md hybrid structure—keep README.md as a 250-300 line entry point, create 8-9 focused markdown files in `/docs/`, add CHANGELOG.md for version history, and ensure all HelpLinkUri anchors resolve. The diagnostic reference stays as a single searchable file (`docs/diagnostics.md`) rather than splitting per diagnostic.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Hybrid approach — lean README.md (~250-300 lines) + focused /docs/ directory
- **D-02:** README focuses on: what, why, install, quickstart, key benefits, links to deeper docs
- **D-03:** /docs/ directory contains: getting-started.md, attributes.md, diagnostics.md (existing), testing.md (NEW), cli-reference.md (NEW), configuration.md, migration.md, platform-constraints.md
- **D-04:** Multi-page structure enables shareable URLs, easier maintenance, progressive disclosure
- **D-05:** Diagnostic reference stays in single docs/diagnostics.md file (searchable, not split per diagnostic)
- **D-06:** Hybrid tutorial-reference — progressive disclosure from 30-second to 5-minute to conceptual model
- **D-07:** 30-second version for DI-savvy users (install, annotate, register)
- **D-08:** 5-minute walkthrough building a complete service with explanation
- **D-09:** "What you eliminated" before/after comparison showing value
- **D-10:** Conceptual model explaining how IoCTools thinks
- **D-11:** Strategic links from tutorial to reference sections
- **D-12:** Dedicated /docs/testing.md (300-400 lines) as primary testing package documentation
- **D-13:** Main README includes 20-line "Testing with IoCTools" teaser section with link
- **D-14:** Package README (IoCTools.Testing.Abstractions/) minimal with NuGet badge + link
- **D-15:** Cross-linking: main README <-> /docs/testing.md <-> package README
- **D-16:** Keep two-file structure (README table + docs/diagnostics.md)
- **D-17:** README shows curated Error-only table (~30 rows) with link to full reference
- **D-18:** docs/diagnostics.md enhanced with: category navigation sections, severity badges, cross-references between related diagnostics, category index tables
- **D-19:** HelpLinkUri workflow unchanged — current anchor format (#ioc001) works correctly
- **D-20:** Add "What's New in v1.5.0" section after Installation, before Getting Started (max 4 bullets with anchors)
- **D-21:** Create CHANGELOG.md for version history (industry standard, keeps README current)
- **D-22:** IoCTools.Testing gets new dedicated section in README (80-100 lines) placed after Attribute Reference, before Diagnostics Reference
- **D-23:** CLI enhancements integrated into existing CLI section (no new section)
- **D-24:** typeof() diagnostics added to existing diagnostic table (+6 rows)
- **D-25:** README shows current version only; historical content moved to CHANGELOG
- **D-26:** Multi-location strategy — README.md "Platform Support" section + /docs/platform-constraints.md + CONTRIBUTING.md reference
- **D-27:** README gets 2-3 sentence "Platform Support" section answering "Will this work in my project?"
- **D-28:** /docs/platform-constraints.md (200-300 lines) with full technical details, workarounds, feature request guidance
- **D-29:** Key distinction emphasized: netstandard2.0 constrains generator internally, NOT user's service code
- **D-30:** Progressive disclosure — user-facing (README) -> contributor-facing (docs/) -> internal (CLAUDE.md)

### Claude's Discretion
- Exact wording of "What's New" bullets
- Specific code examples for getting started tutorial
- Visual formatting (badges, tables, collapsible sections)
- docs/ directory file naming conventions

### Deferred Ideas (OUT OF SCOPE)
- DocFX or full documentation site — Over-engineering at current scale; markdown in repo is sufficient
- API documentation generation via XML doc comments — Not in scope for this phase
- Video tutorials or walkthrough content — Deferred based on user demand
- Internationalization/translation — Deferred until documentation stabilizes

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DOC-01 | Evaluate single-doc vs multi-page documentation structure | CONTEXT.md D-01 through D-05 establish hybrid approach with locked decisions |
| DOC-02 | If warranted, migrate to multi-page docs in `/docs/` directory | CONTEXT.md D-03 specifies exact file list for /docs/ directory |
| DOC-03 | Getting started guide (5-minute path to first working service) | CONTEXT.md D-06 through D-11 define hybrid tutorial-reference structure |
| DOC-04 | Attributes reference page with examples for all attributes | CONTEXT.md D-03 specifies attributes.md; existing README Attribute Reference table can be extracted |
| DOC-05 | Diagnostics reference page (searchable table of all diagnostics with fix guidance) | docs/diagnostics.md already exists (27KB); CONTEXT.md D-16 through D-19 define enhancements |
| DOC-06 | CLI reference page with command examples | CONTEXT.md D-03 specifies cli-reference.md; existing README CLI section can be extracted |
| DOC-07 | IoCTools.Testing usage guide | CONTEXT.md D-12 through D-15 define testing.md structure and cross-linking strategy |
| DOC-08 | Update README to cover v1.3.0+ features completely | CONTEXT.md D-20 through D-25 define README restructuring with What's New, CHANGELOG, testing section |
| DOC-09 | Cross-reference netstandard2.0 constraints in documentation | CONTEXT.md D-26 through D-30 define multi-location platform constraints strategy |

</phase_requirements>

## Standard Stack

### Core
| Tool/Format | Purpose | Why Standard |
|-------------|---------|--------------|
| GitHub Flavored Markdown | All documentation files | Native to GitHub, renders in repo, supports tables/code blocks |
| markdown anchors (#ioc001) | HelpLinkUri navigation | Proven pattern in existing docs/diagnostics.md, all HelpLinkUris use this format |
| CHANGELOG.md | Version history | Industry standard (Keep a Changelog), keeps README current |

### Supporting
| Format/Tool | Purpose | When to Use |
|-------------|---------|-------------|
| HTML tables (in markdown) | Diagnostic reference | Existing pattern in README, enables sorting/filtering |
| code fences with language hints | C# examples | ```csharp for syntax highlighting |
| relative links ([path](docs/...)) | Cross-document navigation | GitHub renders correctly for repo navigation |
| badges (shields.io) | NuGet package versions | Existing pattern in README header |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Single README.md | DocFX static site | DocFX over-engineering at current scale; markdown sufficient |
| docs/diagnostics.md single file | Split per diagnostic (ioc001.md, etc.) | Single file searchable; split harder to navigate for 94+ diagnostics |
| In-repo markdown | External docs site | External site adds deployment overhead; GitHub markdown renders well |

**Installation:** None required (markdown is native to git/GitHub)

**Version verification:** N/A (documentation files, not code packages)

## Architecture Patterns

### Recommended Documentation Structure

```
IoCTools/
├── README.md                     # Lean 250-300 line landing page
├── CHANGELOG.md                  # NEW: Version history (v1.5.0, v1.6.0, etc.)
├── CONTRIBUTING.md               # Update with platform constraints reference
├── docs/
│   ├── getting-started.md        # NEW: 30-second + 5-minute + conceptual tutorial
│   ├── attributes.md             # NEW: Extracted from README Attribute Reference
│   ├── diagnostics.md            # EXISTING: Enhance with category sections, severity badges
│   ├── testing.md                # NEW: IoCTools.Testing package usage guide
│   ├── cli-reference.md          # NEW: Extracted from README CLI section
│   ├── configuration.md          # NEW: MSBuild properties, .editorconfig, GeneratorOptions
│   ├── migration.md              # NEW: Migrating from manual DI or other containers
│   └── platform-constraints.md   # NEW: netstandard2.0 details, workarounds
├── IoCTools.Testing.Abstractions/
│   └── README.md                 # NEW: Minimal package README with NuGet badge + link to /docs/testing.md
```

### Pattern 1: Progressive Disclosure in Getting Started
**What:** Three-layer onboarding (30-second, 5-minute, conceptual)
**When to use:** New user acquisition via README → getting-started.md
**Example:**
```markdown
## Getting Started

### 30-Second Quick Start (DI-Savvy)
1. Install packages
2. Add `[Scoped]` attribute
3. Call `AddYourAssemblyRegisteredServices()`

### 5-Minute Walkthrough
[Complete service example with explanation]

### How IoCTools Thinks
[Conceptual model of generator behavior]
```

### Pattern 2: Cross-Document Linking Strategy
**What:** Strategic links between related sections
**When to use:** Tutorial → reference transitions
**Example:**
```markdown
README "Getting Started" section → "See [getting-started.md](docs/getting-started.md) for detailed walkthrough"
README "What's New" bullets → Anchor links to detailed sections
README "Testing" teaser → "Full guide: [docs/testing.md](docs/testing.md)"
```

### Anti-Patterns to Avoid
- **Documentation sprawl:** Don't create 20+ small files; keep focused on 8-9 key documents
- **Link rot:** All HelpLinkUris must resolve; verify anchors exist before committing
- **Version mixing:** README shows current version only; move historical to CHANGELOG.md
- **Platform confusion:** Clearly distinguish generator constraints (netstandard2.0) from user code (net8.0+)

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Documentation site generator | Custom DocFX config | GitHub markdown | GitHub renders markdown natively; no build step needed |
| API doc generation | Custom XML doc scraper | Not needed in this phase | Out of scope per deferred ideas |
| Diagnostic anchors | Manual anchor calculation | Existing #iocXXX pattern | All 94 diagnostics already use this format |
| CHANGELOG formatting | Custom changelog tool | Keep a Changelog format | Industry standard, human-readable |

**Key insight:** GitHub markdown is sufficient for IoCTools' documentation needs. External documentation sites add deployment complexity without significant user benefit at current scale.

## Common Pitfalls

### Pitfall 1: HelpLinkUri 404s
**What goes wrong:** New diagnostics added without corresponding docs/diagnostics.md entries
**Why it happens:** Diagnostic descriptors reference anchors that don't exist yet
**How to avoid:** Verify anchor exists in docs/diagnostics.md when adding new diagnostic descriptors; use `grep` to check for `## IOCXXX` pattern
**Warning signs:** IDE F1 help shows "Page not found" errors

### Pitfall 2: README Bloat
**What goes wrong:** README grows back to 500+ lines after adding v1.5.0 features
**Why it happens:** Tempting to add full documentation inline rather than extracting to /docs/
**How to avoid:** Enforce 250-300 line target; extract detailed content to /docs/ files with "Read more in docs/X.md" links
**Warning signs:** README requires excessive scrolling to reach Installation section

### Pitfall 3: Inconsistent Cross-Links
**What goes wrong:** Links break when files are renamed or moved
**Why it happens:** Hard-coded paths without verification
**How to avoid:** Use relative links consistently; test all links after restructuring
**Warning signs:** GitHub shows broken link indicators in preview

### Pitfall 4: Platform Confusion
**What goes wrong:** Users think netstandard2.0 limits their service code
**Why it happens:** Unclear distinction between generator and consumer code
**How to avoid:** Explicitly call out "netstandard2.0 = generator only; your code = any TFM" in Platform Support section
**Warning signs:** Issues asking "can I use C# 12 features in my services?"

## Code Examples

### Diagnostic Reference Enhancement Pattern
**Current structure** (docs/diagnostics.md):
```markdown
## IOC001

**Severity:** Error
**Category:** IoCTools.Dependency

**Cause:** No implementation of the depended-upon interface exists in the project.

**Fix:** Create a class implementing the interface with a lifetime attribute, add `[ExternalService]`, or register manually.
```

**Enhanced structure** (with category sections):
```markdown
## Dependency Diagnostics (IOC001-IOC009)

### IOC001

**Severity:** [!Error](badge) | **Category:** IoCTools.Dependency

**Cause:** No implementation of the depended-upon interface exists in the project.

**Fix:** Create a class implementing the interface with a lifetime attribute, add `[ExternalService]`, or register manually.

**Related:** [IOC002](#ioc002) (implementation exists but not registered)

---
```

### Testing Package Documentation Pattern
**Structure for docs/testing.md** (300-400 lines):
```markdown
# Testing with IoCTools

## Overview
[What IoCTools.Testing does, why it eliminates boilerplate]

## Installation
[Package reference for test project only]

## Quick Example
[Before: manual mocks → After: [Cover<T>]]

## Generated Members
- Mock<T> fields
- CreateSut() factory
- Setup helpers

## Advanced Scenarios
- Inheritance hierarchies
- Configuration injection
- Typed setup helpers

## Test Diagnostics
[TDIAG-01 through TDIAG-05 reference]

[Link back to main README]
```

### README Teaser Section Pattern
```markdown
## Testing with IoCTools

IoCTools.Testing auto-generates test fixtures eliminating mock declaration boilerplate.

```csharp
[Cover<UserService>]
public partial class UserServiceTests
{
    [Fact]
    public void Test() {
        var sut = CreateSut(); // Auto-generated
    }
}
```

[Full testing guide](docs/testing.md)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single 538-line README | Hybrid README + /docs/ | Phase 4 (this phase) | Shareable URLs, easier maintenance, progressive disclosure |
| Inline version history | CHANGELOG.md | Phase 4 (this phase) | README stays current; version history follows standard format |
| No testing package docs | Dedicated docs/testing.md | Phase 4 (this phase) | Testing package discoverability improves |
| All diagnostics in README | Error-only in README + full in docs/ | Phase 4 (this phase) | README cognitive load reduced (30 rows vs 94) |

**Deprecated/outdated:**
- Single-file documentation: Modern libraries use multi-page structure for better navigation
- Version history in README: CHANGELOG.md is industry standard

## Open Questions

1. **Diagnostic categorization for docs/diagnostics.md**
   - What we know: CONTEXT.md D-18 specifies category navigation sections, severity badges, cross-references
   - What's unclear: Exact category headings (Lifetime, Dependency, Configuration, Registration, Structural match IDE categories)
   - Recommendation: Use IDE categories from DiagnosticDescriptors (5 subcategories) as navigation sections

2. **CLI command examples format**
   - What we know: CONTEXT.md D-03 specifies cli-reference.md
   - What's unclear: Table format vs. heading-per-command format
   - Recommendation: Follow existing README CLI table format; expand each command with example section below the table

## Validation Architecture

> Skip this section — workflow.nyquist_validation not applicable to documentation phase. Documentation quality is verified through user feedback and link checking.

### Manual Verification Strategy
| Check Type | Command/Method | Frequency |
|------------|----------------|-----------|
| Link integrity | `markdown-link-check` or manual review | After each docs commit |
| Anchor resolution | Verify `#iocXXX` anchors exist in docs/diagnostics.md | When adding diagnostics |
| README line count | `wc -l README.md` | After README edits |

## Sources

### Primary (HIGH confidence)
- **Context.md Decisions D-01 through D-30** — Locked implementation decisions defining hybrid structure
- **Existing docs/diagnostics.md** — 27KB diagnostic reference with proven anchor pattern
- **Existing README.md** — 538-line comprehensive documentation with Attribute Reference, CLI section, Before/After examples
- **TestingExamples.cs** — Sample code showing IoCTools.Testing usage patterns (commented examples)

### Secondary (MEDIUM confidence)
- **AutoMapper GitHub README** — Hybrid tutorial/reference structure example (What is it? + How do I get started? + Where can I get it?)
- **Contributing.md** — Existing contribution guidelines with diagnostic workflow

### Tertiary (LOW confidence)
- WebSearch attempts for ".NET source generator documentation best practices" returned empty results; relied on project-specific patterns and CONTEXT.md decisions instead

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Markdown and GitHub features are well-understood
- Architecture: HIGH - CONTEXT.md provides explicit structure decisions (30 locked decisions)
- Pitfalls: HIGH - Based on established documentation patterns and existing project pain points
- Code examples: HIGH - Derived from existing README.md, TestingExamples.cs, docs/diagnostics.md

**Research date:** 2026-03-21
**Valid until:** 60 days (documentation patterns are stable; CONTEXT.md decisions are locked)
