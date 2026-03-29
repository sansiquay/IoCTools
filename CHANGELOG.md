# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [1.5.0] - 2026-03-21

### Added
- typeof() registration diagnostics (IOC090-IOC094) for build-time validation of manual DI registrations
- IoCTools.Testing package with `[Cover<T>]` attribute for auto-generated test fixtures (Mock<T> fields, CreateSut(), setup helpers)
- Test fixture analyzer diagnostics (TDIAG-01 through TDIAG-05) suggesting fixture usage
- CLI improvements: `--json` output mode, `--verbose` debugging, color-coded severity output
- CLI wildcard filtering and fuzzy suggestions for type names
- CLI `config-audit` command for detecting missing configuration keys
- CLI `suppress` command for generating .editorconfig diagnostic suppression recipes
- FluentValidation source generator support (IoCTools.FluentValidation) with validator discovery, composition graphs, and anti-pattern diagnostics (IOC100-IOC102)
- CLI `validators` and `validator-graph` commands for inspecting FluentValidation validators
- FluentValidation-aware test fixture helpers (`SetupValidationSuccess`/`SetupValidationFailure`)

### Changed
- Enhanced IOC012/IOC013/IOC087 messages with IServiceProvider/CreateScope() suggestions
- IOC015 now shows full inheritance path (A -> B -> C format) for lifetime mismatches
- All 94+ diagnostics now have HelpLinkUri pointing to docs/diagnostics.md#iocXXX
- Diagnostic messages reference individual lifetime attributes ([Scoped], [Singleton], [Transient])

### Fixed
- CS8603 null reference warnings in sample code
- Exception handling in ConstructorGenerator, InterfaceDiscovery, and ServiceRegistrationGenerator

### Diagnostic
- 5 new typeof() diagnostics (IOC090-IOC094)
- 5 new test fixture diagnostics (TDIAG-01 through TDIAG-05)

[Unreleased]: https://github.com/yourusername/IoCTools/compare/v1.5.0...HEAD
[1.5.0]: https://github.com/yourusername/IoCTools/compare/v1.4.0...v1.5.0
