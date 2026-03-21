# Requirements: IoCTools v1.5.0

**Defined:** 2026-03-21
**Core Value:** Eliminate DI boilerplate in both production and test code through compile-time source generation with zero runtime overhead.

## v1.5.0 Requirements

Requirements for this milestone. Each maps to roadmap phases.

### Test Fixture Generation

- [ ] **TEST-01**: IoCTools.Testing ships as a separate NuGet package with Moq as a peer dependency
- [ ] **TEST-02**: Generator auto-declares `Mock<T>` fields for all constructor dependencies of a service
- [ ] **TEST-03**: Generator produces a `CreateSut()` factory method that wires all mock `.Object` values into the service constructor
- [ ] **TEST-04**: Generated fixtures support services using `[Inject]` fields
- [ ] **TEST-05**: Generated fixtures support services using `[DependsOn]` attributes
- [ ] **TEST-06**: Generated fixtures support inheritance hierarchies with proper base fixture chaining
- [ ] **TEST-07**: Generator produces typed mock setup helper methods (e.g., `SetupUserRepository(Action<Mock<IUserRepository>>)`)
- [ ] **TEST-08**: Generator produces configuration mock helpers for services using `[InjectConfiguration]` (pre-wired `IOptions<T>` setup)
- [ ] **TEST-09**: Generated fixture compiles without manual intervention for all supported service patterns
- [ ] **TEST-10**: Mock fields are auto-initialized (`new Mock<T>()`) in the fixture constructor
- [ ] **TEST-11**: Comprehensive test suite for the test fixture generator covering all IoCTools service patterns

### typeof() Diagnostics

- [ ] **DIAG-01**: typeof() argument parsing foundation added to ManualRegistrationValidator
- [ ] **DIAG-02**: IOC090 — typeof() interface-implementation registration could use IoCTools
- [ ] **DIAG-03**: IOC091 — typeof() registration duplicates IoCTools registration
- [ ] **DIAG-04**: IOC092 — typeof() registration lifetime mismatch
- [ ] **DIAG-05**: IOC094 — Open generic typeof() could use IoCTools attributes
- [ ] **DIAG-06**: Integration tests for all typeof() diagnostics (IOC090-094)
- [ ] **DIAG-07**: typeof() diagnostic examples added to sample project

### Diagnostic UX

- [ ] **DUX-01**: HelpLinkUri added to all 87+ diagnostic descriptors
- [ ] **DUX-02**: IDE categories updated to specific groupings (Lifetime, Dependency, Configuration, Registration, Structural)
- [ ] **DUX-03**: IOC012/013 messages suggest IServiceProvider/CreateScope() pattern for intentional lifetime violations
- [ ] **DUX-04**: IOC015 message shows full inheritance path (A -> B -> C)
- [ ] **DUX-05**: IOC016-019 messages include configuration examples showing valid usage

### CLI Improvements

- [ ] **CLI-01**: --verbose flag for debugging (MSBuild diagnostics, generator timing, file paths)
- [ ] **CLI-02**: --json output mode for all commands (machine-readable output)
- [ ] **CLI-03**: Color-coded diagnostic output by severity (red/yellow/cyan)
- [ ] **CLI-04**: Fuzzy type suggestions extended to all commands (extract WhyPrinter pattern)
- [ ] **CLI-05**: Wildcard/regex support in FilterByType
- [ ] **CLI-06**: Service count added to profile command output
- [ ] **CLI-07**: .editorconfig recipe generation for suppressing IoCTools diagnostics

### Code Quality

- [ ] **QUAL-01**: Centralize RegisterAsAllAttribute checks using AttributeTypeChecker (20 inconsistent locations)
- [ ] **QUAL-02**: Adopt ReportDiagnosticDelegate pattern in 3-4 more validators
- [ ] **QUAL-03**: Resolve CS8603 null reference warnings in sample code (3 instances in MultiInterfaceExamples.cs)
- [ ] **QUAL-04**: Add code comments explaining InstanceSharing.Separate default behavior
- [ ] **QUAL-05**: Tighten bare catch(Exception) blocks in ConstructorGenerator, InterfaceDiscovery, and ServiceRegistrationGenerator

### Documentation

- [ ] **DOC-01**: Evaluate single-doc vs multi-page documentation structure
- [ ] **DOC-02**: If warranted, migrate to multi-page docs in `/docs/` directory
- [ ] **DOC-03**: Getting started guide (5-minute path to first working service)
- [ ] **DOC-04**: Attributes reference page with examples for all attributes
- [ ] **DOC-05**: Diagnostics reference page (searchable table of all diagnostics with fix guidance)
- [ ] **DOC-06**: CLI reference page with command examples
- [ ] **DOC-07**: IoCTools.Testing usage guide
- [ ] **DOC-08**: Update README to cover v1.3.0+ features completely
- [ ] **DOC-09**: Cross-reference netstandard2.0 constraints in documentation

## v2 Requirements

Deferred to future milestone. Tracked but not in current roadmap.

### Testing Package Extensions

- **TEST-F01**: NSubstitute support as an alternative to Moq
- **TEST-F02**: FakeItEasy support
- **TEST-F03**: xUnit/NUnit-specific fixture integration (ClassFixture, TestFixture)

### Advanced Diagnostics

- **DIAG-F01**: CodeFixProvider for common diagnostics (separate analyzer package)
- **DIAG-F02**: Diagnostic for 20+ dependencies as code smell indicator
- **DIAG-F03**: Detect validation attributes and suggest IValidateOptions

### Tooling

- **CLI-F01**: Progress indicators for CLI long operations
- **CLI-F02**: Standardize diagnostic assertion syntax across test suite

## Out of Scope

| Feature | Reason |
|---------|--------|
| Runtime auto-mocking container | Moq.AutoMocker already does this well; IoCTools value is compile-time generation |
| Test data generation | AutoFixture's territory; orthogonal to mock wiring |
| DocFX / full documentation site | Over-engineering at current scale; multi-page markdown in repo is sufficient |
| Mocking sealed classes | Requires IL weaving; fundamentally different architecture |
| Test assertion helpers | FluentAssertions is the ecosystem standard; don't fragment it |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| TEST-01 | TBD | Pending |
| TEST-02 | TBD | Pending |
| TEST-03 | TBD | Pending |
| TEST-04 | TBD | Pending |
| TEST-05 | TBD | Pending |
| TEST-06 | TBD | Pending |
| TEST-07 | TBD | Pending |
| TEST-08 | TBD | Pending |
| TEST-09 | TBD | Pending |
| TEST-10 | TBD | Pending |
| TEST-11 | TBD | Pending |
| DIAG-01 | TBD | Pending |
| DIAG-02 | TBD | Pending |
| DIAG-03 | TBD | Pending |
| DIAG-04 | TBD | Pending |
| DIAG-05 | TBD | Pending |
| DIAG-06 | TBD | Pending |
| DIAG-07 | TBD | Pending |
| DUX-01 | TBD | Pending |
| DUX-02 | TBD | Pending |
| DUX-03 | TBD | Pending |
| DUX-04 | TBD | Pending |
| DUX-05 | TBD | Pending |
| CLI-01 | TBD | Pending |
| CLI-02 | TBD | Pending |
| CLI-03 | TBD | Pending |
| CLI-04 | TBD | Pending |
| CLI-05 | TBD | Pending |
| CLI-06 | TBD | Pending |
| CLI-07 | TBD | Pending |
| QUAL-01 | TBD | Pending |
| QUAL-02 | TBD | Pending |
| QUAL-03 | TBD | Pending |
| QUAL-04 | TBD | Pending |
| QUAL-05 | TBD | Pending |
| DOC-01 | TBD | Pending |
| DOC-02 | TBD | Pending |
| DOC-03 | TBD | Pending |
| DOC-04 | TBD | Pending |
| DOC-05 | TBD | Pending |
| DOC-06 | TBD | Pending |
| DOC-07 | TBD | Pending |
| DOC-08 | TBD | Pending |
| DOC-09 | TBD | Pending |

**Coverage:**
- v1.5.0 requirements: 42 total
- Mapped to phases: 0
- Unmapped: 42

---
*Requirements defined: 2026-03-21*
*Last updated: 2026-03-21 after initial definition*
