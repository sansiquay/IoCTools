# Milestones

## v1.5.0 — Test Fixture Generation, FluentValidation, and Documentation (Shipped: 2026-03-29)

**Phases:** 7 phases, 24 plans | **Timeline:** 25 days (2026-03-04 to 2026-03-29) | **Stats:** 144 commits, 229 files changed, +33,819/-1,723 lines

**Key accomplishments:**

- HelpLinkUri, IDE categories, and enhanced messages added to all 87 diagnostics; centralized attribute checks with OOM/SOF exception filters
- typeof() registration diagnostics (IOC090-094) with open generic detection and ServiceDescriptor factory method support
- CLI improvements: JSON output, verbose debugging, color-coded UI, suppress command, wildcard filtering, fuzzy suggestions
- IoCTools.Testing package with `Cover<T>` attribute, `Mock<T>` field generation, `CreateSut()` factories, typed setup helpers, and TDIAG-01..05 diagnostics
- Multi-page documentation: getting-started, attributes, configuration, diagnostics (102 entries), CLI reference, testing, migration, platform-constraints
- IoCTools.FluentValidation source generator: validator discovery, composition graph builder, anti-pattern diagnostics (IOC100-102), test fixture helpers, CLI validator/graph commands
- Gap closure: fixed solution build (MSB5004), integrated FV diagnostics into CLI catalog and documentation

**Tech debt carried forward:**
- `IoCTools.FluentValidation.csproj` PackageProjectUrl/RepositoryUrl reference `nate123456` instead of `nathan-p-lane`
- CS8602 nullable warning in FixtureEmitter.cs line 147

**Tests:** ~1,814 total (1,671 generator + 25 FV + 13 testing + ~105 CLI)

---
