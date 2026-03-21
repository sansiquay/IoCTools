# Roadmap: IoCTools v1.5.0

## Overview

IoCTools v1.5.0 builds on a mature v1.3.0 foundation (1650+ tests, 86 diagnostics) to deliver four capabilities: internal code quality hardening, expanded diagnostics and CLI tooling, a new compile-time test fixture generator package, and a documentation overhaul. The work flows from stabilization (tighten exception handling, polish diagnostic UX) through feature expansion (typeof() diagnostics, CLI improvements) to the highest-complexity new package (IoCTools.Testing), with documentation written last after all features have stabilized.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Code Quality and Diagnostic UX** - Harden exception handling, centralize patterns, and polish diagnostic metadata across the existing codebase
- [x] **Phase 2: typeof() Diagnostics and CLI** - Add typeof() registration diagnostics (IOC090-094) and extend CLI with --json, --verbose, color output, and filtering improvements (completed 2026-03-21)
- [ ] **Phase 3: Test Fixture Generation** - Ship IoCTools.Testing as a new NuGet package that generates Moq-based test fixture base classes from the DI graph
- [ ] **Phase 4: Documentation** - Evaluate and build documentation structure covering all v1.5.0 features including the new testing package

## Phase Details

### Phase 1: Code Quality and Diagnostic UX
**Goal**: The existing codebase is hardened against silent failures and all diagnostics present polished, actionable metadata to IDEs
**Depends on**: Nothing (first phase)
**Requirements**: QUAL-01, QUAL-02, QUAL-03, QUAL-04, QUAL-05, DUX-01, DUX-02, DUX-03, DUX-04, DUX-05
**Success Criteria** (what must be TRUE):
  1. All bare catch(Exception) blocks in ConstructorGenerator, InterfaceDiscovery, and ServiceRegistrationGenerator are replaced with specific exception handling or logging
  2. RegisterAsAllAttribute checks use AttributeTypeChecker consistently across all 20 locations (no ad-hoc string comparisons remain)
  3. Every diagnostic descriptor (87+) has a non-empty HelpLinkUri and a specific IDE category (Lifetime, Dependency, Configuration, Registration, or Structural)
  4. IOC012/013 messages include the IServiceProvider/CreateScope() suggestion, and IOC015 shows the full inheritance path (e.g., A -> B -> C)
  5. The project builds with zero CS8603 warnings in sample code
**Plans:** 2 plans

Plans:
- [x] 01-01-PLAN.md — Add HelpLinkUri, IDE categories, and enhanced messages to all 87 diagnostic descriptors; create docs/diagnostics.md reference
- [x] 01-02-PLAN.md — Centralize RegisterAsAllAttribute checks, tighten exception handling, adopt ReportDiagnosticDelegate, fix CS8603, add InstanceSharing comments

### Phase 2: typeof() Diagnostics and CLI
**Goal**: Users get diagnostic guidance for typeof()-based DI registrations and CLI users get machine-readable output, debugging tools, and improved filtering
**Depends on**: Phase 1
**Requirements**: DIAG-01, DIAG-02, DIAG-03, DIAG-04, DIAG-05, DIAG-06, DIAG-07, CLI-01, CLI-02, CLI-03, CLI-04, CLI-05, CLI-06, CLI-07
**Success Criteria** (what must be TRUE):
  1. `services.AddScoped(typeof(IFoo), typeof(Foo))` where Foo is already registered by IoCTools triggers IOC091 (duplicate) at build time
  2. `services.AddTransient(typeof(IFoo), typeof(Foo))` where IoCTools registers Foo as Scoped triggers IOC092 (lifetime mismatch)
  3. Running any CLI command with --json produces valid JSON to stdout that can be piped to jq
  4. Running any CLI command with --verbose shows MSBuild diagnostic output, generator timing, and resolved file paths
  5. CLI diagnostic output is color-coded by severity (red for Error, yellow for Warning, cyan for Info) in terminal
**Plans:** 5/5 plans complete

Plans:
- [x] 02-01-PLAN.md — Add IOC090-094 diagnostic descriptors and extend ManualRegistrationValidator with typeof() detection
- [x] 02-02-PLAN.md — Create integration tests for typeof() diagnostics and add sample project examples
- [x] 02-03-PLAN.md — Add CLI infrastructure: AnsiColor utility, OutputContext abstraction, --json/--verbose flags, color-coded printers
- [x] 02-04-PLAN.md — Add wildcard filtering (TypeFilterUtility), fuzzy suggestions (FuzzySuggestionUtility), and profile service count
- [x] 02-05-PLAN.md — Add ioc-tools suppress command with DiagnosticCatalog, .editorconfig generation, --live mode

### Phase 3: Test Fixture Generation
**Goal**: Test authors get auto-generated Moq-based fixture base classes that eliminate mock declaration and SUT construction boilerplate
**Depends on**: Phase 1
**Requirements**: TEST-01, TEST-02, TEST-03, TEST-04, TEST-05, TEST-06, TEST-07, TEST-08, TEST-09, TEST-10, TEST-11, TDIAG-01, TDIAG-02, TDIAG-03, TDIAG-04, TDIAG-05
**Success Criteria** (what must be TRUE):
  1. A test class referencing a service with 5 constructor dependencies gets auto-generated Mock<T> fields and a CreateSut() method that wires them all -- no manual mock declarations needed
  2. Generated fixtures work for services using [Inject], [DependsOn], [InjectConfiguration], and inheritance hierarchies without manual intervention
  3. IoCTools.Testing installs as a separate NuGet package and does not introduce Moq as a transitive dependency to production projects
  4. Typed mock setup helpers (e.g., SetupUserRepository(Action<Mock<IUserRepository>>)) appear in IDE auto-complete on the generated fixture
  5. The test fixture generator has its own comprehensive test suite validating all supported service patterns
  6. Analyzer detects manual mock declarations and SUT construction for services with generated fixtures and suggests using the fixture instead
**Plans**: TBD

Plans:
- [ ] 03-01: TBD
- [ ] 03-02: TBD
- [ ] 03-03: TBD

### Phase 4: Documentation
**Goal**: New and existing users can discover, learn, and reference all IoCTools features through well-structured documentation
**Depends on**: Phase 2, Phase 3
**Requirements**: DOC-01, DOC-02, DOC-03, DOC-04, DOC-05, DOC-06, DOC-07, DOC-08, DOC-09
**Success Criteria** (what must be TRUE):
  1. A new user can go from zero to a working service registration in under 5 minutes following the getting started guide
  2. Every diagnostic (IOC001-IOC094) is documented with its message, cause, and fix guidance in a searchable reference
  3. The IoCTools.Testing package has a dedicated usage guide showing fixture generation, mock setup, and CreateSut() patterns
  4. All HelpLinkUri values from Phase 1 resolve to actual documentation pages (no 404s)
**Plans**: TBD

Plans:
- [ ] 04-01: TBD
- [ ] 04-02: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4
Note: Phase 2 and Phase 3 can execute in parallel (both depend only on Phase 1).

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Code Quality and Diagnostic UX | 2/2 | Complete | 2026-03-21 |
| 2. typeof() Diagnostics and CLI | 5/5 | Complete   | 2026-03-21 |
| 3. Test Fixture Generation | 0/3 | Not started | - |
| 4. Documentation | 0/2 | Not started | - |
