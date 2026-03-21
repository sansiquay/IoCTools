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

### Test Fixture Analyzers

- [ ] **TDIAG-01**: Detect manual `new Mock<T>()` fields where T is a dependency of an IoCTools service that has a generated fixture — suggest using the fixture
- [ ] **TDIAG-02**: Detect manual SUT construction (`new ServiceName(mock.Object, ...)`) where a generated `CreateSut()` exists — suggest using CreateSut()
- [ ] **TDIAG-03**: Detect test classes with mock fields matching an IoCTools service's dependency graph — suggest inheriting from the generated fixture base class
- [ ] **TDIAG-04**: Integration tests for all test fixture analyzer diagnostics
- [ ] **TDIAG-05**: Test fixture analyzer examples added to sample/test project

### typeof() Diagnostics

- [ ] **DIAG-01**: typeof() argument parsing foundation added to ManualRegistrationValidator
- [ ] **DIAG-02**: IOC090 — typeof() interface-implementation registration could use IoCTools
- [ ] **DIAG-03**: IOC091 — typeof() registration duplicates IoCTools registration
- [ ] **DIAG-04**: IOC092 — typeof() registration lifetime mismatch
- [ ] **DIAG-05**: IOC094 — Open generic typeof() could use IoCTools attributes
- [x] **DIAG-06**: Integration tests for all typeof() diagnostics (IOC090-094)
- [x] **DIAG-07**: typeof() diagnostic examples added to sample project

### Diagnostic UX

- [x] **DUX-01**: HelpLinkUri added to all 87+ diagnostic descriptors
- [x] **DUX-02**: IDE categories updated to specific groupings (Lifetime, Dependency, Configuration, Registration, Structural)
- [x] **DUX-03**: IOC012/013 messages suggest IServiceProvider/CreateScope() pattern for intentional lifetime violations
- [x] **DUX-04**: IOC015 message shows full inheritance path (A -> B -> C)
- [x] **DUX-05**: IOC016-019 messages include configuration examples showing valid usage

### CLI Improvements

- [x] **CLI-01**: --verbose flag for debugging (MSBuild diagnostics, generator timing, file paths)
- [x] **CLI-02**: --json output mode for all commands (machine-readable output)
- [x] **CLI-03**: Color-coded diagnostic output by severity (red/yellow/cyan)
- [x] **CLI-04**: Fuzzy type suggestions extended to all commands (extract WhyPrinter pattern)
- [x] **CLI-05**: Wildcard/regex support in FilterByType
- [x] **CLI-06**: Service count added to profile command output
- [x] **CLI-07**: .editorconfig recipe generation for suppressing IoCTools diagnostics

### Code Quality

- [x] **QUAL-01**: Centralize RegisterAsAllAttribute checks using AttributeTypeChecker (20 inconsistent locations)
- [x] **QUAL-02**: Adopt ReportDiagnosticDelegate pattern in 3-4 more validators
- [x] **QUAL-03**: Resolve CS8603 null reference warnings in sample code (3 instances in MultiInterfaceExamples.cs)
- [x] **QUAL-04**: Add code comments explaining InstanceSharing.Separate default behavior
- [x] **QUAL-05**: Tighten bare catch(Exception) blocks in ConstructorGenerator, InterfaceDiscovery, and ServiceRegistrationGenerator

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
| QUAL-01 | Phase 1 | Complete |
| QUAL-02 | Phase 1 | Complete |
| QUAL-03 | Phase 1 | Complete |
| QUAL-04 | Phase 1 | Complete |
| QUAL-05 | Phase 1 | Complete |
| DUX-01 | Phase 1 | Complete |
| DUX-02 | Phase 1 | Complete |
| DUX-03 | Phase 1 | Complete |
| DUX-04 | Phase 1 | Complete |
| DUX-05 | Phase 1 | Complete |
| DIAG-01 | Phase 2 | Pending |
| DIAG-02 | Phase 2 | Pending |
| DIAG-03 | Phase 2 | Pending |
| DIAG-04 | Phase 2 | Pending |
| DIAG-05 | Phase 2 | Pending |
| DIAG-06 | Phase 2 | Complete |
| DIAG-07 | Phase 2 | Complete |
| CLI-01 | Phase 2 | Complete |
| CLI-02 | Phase 2 | Complete |
| CLI-03 | Phase 2 | Complete |
| CLI-04 | Phase 2 | Complete |
| CLI-05 | Phase 2 | Complete |
| CLI-06 | Phase 2 | Complete |
| CLI-07 | Phase 2 | Complete |
| TEST-01 | Phase 3 | Pending |
| TEST-02 | Phase 3 | Pending |
| TEST-03 | Phase 3 | Pending |
| TEST-04 | Phase 3 | Pending |
| TEST-05 | Phase 3 | Pending |
| TEST-06 | Phase 3 | Pending |
| TEST-07 | Phase 3 | Pending |
| TEST-08 | Phase 3 | Pending |
| TEST-09 | Phase 3 | Pending |
| TEST-10 | Phase 3 | Pending |
| TEST-11 | Phase 3 | Pending |
| TDIAG-01 | Phase 3 | Pending |
| TDIAG-02 | Phase 3 | Pending |
| TDIAG-03 | Phase 3 | Pending |
| TDIAG-04 | Phase 3 | Pending |
| TDIAG-05 | Phase 3 | Pending |
| DOC-01 | Phase 4 | Pending |
| DOC-02 | Phase 4 | Pending |
| DOC-03 | Phase 4 | Pending |
| DOC-04 | Phase 4 | Pending |
| DOC-05 | Phase 4 | Pending |
| DOC-06 | Phase 4 | Pending |
| DOC-07 | Phase 4 | Pending |
| DOC-08 | Phase 4 | Pending |
| DOC-09 | Phase 4 | Pending |

**Coverage:**
- v1.5.0 requirements: 47 total
- Mapped to phases: 47
- Unmapped: 0

---
*Requirements defined: 2026-03-21*
*Last updated: 2026-03-21 after roadmap creation*
