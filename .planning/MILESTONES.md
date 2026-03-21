# Milestones

## v1.5.0 Test Fixture Generation and Documentation (Shipped: 2026-03-21)

**Phases completed:** 4 phases, 15 plans, 40 tasks

**Key accomplishments:**

- HelpLinkUri, specific IDE categories, and enhanced messages added to all 87 diagnostics with docs/diagnostics.md reference file
- Centralized 14 RegisterAsAllAttribute checks through AttributeTypeChecker, tightened exception handling with OOM/SOF filters, adopted ReportDiagnosticDelegate in 4 validators, eliminated CS8603 warnings, and documented InstanceSharing.Separate defaults
- typeof() registration diagnostics (IOC090-094) with open generic detection and ServiceDescriptor factory method support
- Integration tests and sample examples for typeof() diagnostics (IOC090-094)
- JSON output mode, verbose debugging, and color-coded terminal UI for all ioc-tools commands
- 1. [Rule 1 - Bug] Removed incomplete SuppressPrinter.cs blocking build
- CLI suppress command generates .editorconfig rules for IoCTools diagnostics with severity/code filtering, live mode, and file appending with conflict detection
- IoCTools.Testing package foundation with CoverAttribute<TService>, generator skeleton, and Moq 4.20.72 dependency
- Test fixture generator with Mock<T> field generation, CreateSut() factories, and typed Setup{Dependency} helpers for [Cover<T>] test classes
- 1. [Rule 2 - Missing Critical] Added IoCTools.Generator project reference to test project
- Core documentation files created: progressive tutorial (getting-started.md), complete attribute reference (attributes.md), and MSBuild configuration guide (configuration.md) with cross-links and 33 code examples.
- Completed:

---
