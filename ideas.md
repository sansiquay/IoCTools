# IoCTools Ideas & Future Work

## Implementation Backlog

### typeof() Diagnostics

- Add typeof() argument parsing to ManualRegistrationValidator (foundation for IOC090-094)
- Add IOC090 - typeof() interface-implementation registration could use IoCTools
- Add IOC091 - typeof() registration duplicates IoCTools registration
- Add IOC092 - typeof() registration lifetime mismatch
- Add IOC094 - Open generic typeof() could use IoCTools attributes
- Add integration tests for all typeof() diagnostics
- Add typeof() diagnostic examples to sample project
- Update CLAUDE.md diagnostic reference for IOC090-094

### Diagnostic UX

- Add HelpLinkUri to all 87 diagnostic descriptors
- Use specific categories for IDE grouping (Lifetime, Dependency, Configuration, Registration, Structural)
- Suggest IServiceProvider/CreateScope() pattern in IOC012/013 for intentional lifetime violations
- Better config error messages with examples for IOC016-019
- Show full inheritance path in IOC015 diagnostic

### CLI Improvements

- Add --verbose flag for debugging (MSBuild diagnostics, generator timing, file paths)
- Add JSON output mode for all commands (--json flag; GraphPrinter already has precedent)
- Color-code diagnostic output by severity (red/yellow/cyan)
- Extend fuzzy type suggestions to all commands (WhyPrinter pattern already exists)
- Add wildcard/regex support to FilterByType in CLI services
- Add service count to profile command output
- Add .editorconfig recipe for suppressing IoCTools diagnostics

### Code Quality

- Centralize RegisterAsAllAttribute checks using AttributeTypeChecker (20 inconsistent locations)
- Adopt ReportDiagnosticDelegate pattern in 3-4 more validators
- Resolve CS8603 null reference warnings in sample code (3 instances in MultiInterfaceExamples.cs)
- Add code comments explaining InstanceSharing.Separate default behavior

### Documentation

- Cross-reference netstandard2.0 constraints in README
- Update CLAUDE.md diagnostic reference table for new diagnostic codes

---

## Future Considerations

These are worth revisiting later based on user feedback or project growth:

- Add diagnostic for 20+ dependencies as a code smell indicator (validate threshold first)
- Implement CodeFixProvider for common diagnostics (requires separate analyzer package)
- Detect validation attributes and suggest IValidateOptions (complex; better as docs)
- Add progress indicators for CLI long operations (most ops complete in 1-5 seconds)
- Standardize diagnostic assertion syntax across test suite (271 usages; internal-only)
- Annotate non-diagnostic ContainSingle usages in test code (low priority clarity)
