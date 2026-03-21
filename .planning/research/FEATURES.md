# Feature Landscape

**Domain:** .NET source generator for DI test fixture generation, diagnostics expansion, CLI tooling, and library documentation
**Researched:** 2026-03-21

## Table Stakes

Features users expect. Missing = product feels incomplete.

### IoCTools.Testing Package

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Auto-declare `Mock<T>` fields for all constructor dependencies | This is what Moq.AutoMocker does at runtime; compile-time equivalent must match. Every competing tool does this. | Medium | Generator already knows full dependency graph from `[Inject]`/`[DependsOn]` analysis |
| SUT factory method (`CreateSut()`) wiring mocks into constructor | Eliminates the `new Service(mock1.Object, mock2.Object, ...)` boilerplate that breaks on every constructor change. Core value proposition. | Medium | Must handle inheritance chains (base constructor params) correctly |
| Support `[Inject]` fields, `[DependsOn]`, and inheritance hierarchies | These are the primary IoCTools patterns. Fixture gen that ignores them is useless. | High | Inheritance chain traversal logic already exists in generator; reuse it |
| Separate NuGet package (`IoCTools.Testing`) | Test dependencies (Moq, xUnit) must not leak into production packages. Every test helper library ships separately. | Low | Standard .NET packaging; can target net8.0+ |
| Generated fixture compiles without manual intervention | If users have to fix generated code, adoption dies. Must produce valid C# for all supported service patterns. | High | Requires thorough testing across all service patterns in sample app |
| Mock auto-initialization in constructor or setup | Mocks must be pre-initialized (`new Mock<T>()`) so tests can immediately `.Setup()` without boilerplate. | Low | Simple field initialization in generated constructor/setup |

### Diagnostics (typeof())

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| IOC090-092, IOC094: typeof() registration analysis | typeof() is how most .NET devs register services manually before adopting IoCTools. Must detect these patterns to provide migration guidance. | Medium | Requires parsing `typeof()` arguments from `InvocationExpressionSyntax` in ManualRegistrationValidator |
| Integration tests for all typeof() diagnostics | Without tests, regressions are guaranteed. Current diagnostic suite has 1650+ tests setting the quality bar. | Medium | Follow established `SourceGeneratorTestHelper.CompileWithGenerator()` pattern |

### Diagnostic UX

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| HelpLinkUri on all diagnostic descriptors | IDEs (VS, Rider) surface these as clickable links. Missing links make diagnostics feel unfinished. Modern analyzers always include them. | Low | Mechanical change across 87 descriptors; point to docs pages |
| Specific IDE categories (Lifetime, Dependency, etc.) | VS Error List filtering by category is a primary developer workflow. "IoCTools" as a flat category is unhelpful at 86+ diagnostics. | Low | Change `Category` string in `DiagnosticDescriptor` constructors |

### Documentation

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Complete README covering current features | NuGet consumers cite insufficient docs as their top complaint. Current docs must cover v1.3.0 features including RegisterAs, InstanceSharing, all diagnostics. | Medium | Evaluate single-doc vs multi-page; decision pending |
| Getting started guide | New users need a 5-minute path to first working service. Without it, adoption friction is too high. | Low | Concise: install, add attribute, build, done |
| IoCTools.Testing documentation | New package needs its own usage guide or section. Users must understand what gets generated and how to use it. | Low | Write after implementation stabilizes |

### CLI Improvements

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| `--json` output mode for all commands | Machine-readable output is expected for any CLI tool used in CI/CD pipelines or editor integrations. GraphPrinter already has precedent. | Medium | Requires output abstraction across all printers |
| `--verbose` flag for debugging | When things go wrong, users need diagnostic output. Without it, support burden falls on maintainer. | Low | Add MSBuild diagnostics, generator timing, file paths |

## Differentiators

Features that set IoCTools.Testing apart from Moq.AutoMocker, AutoFixture.AutoMoq, and other runtime solutions. These are the competitive advantage.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Compile-time fixture generation (zero runtime overhead)** | Moq.AutoMocker uses reflection at runtime. IoCTools.Testing generates concrete fixture classes at compile time. Faster test execution, IDE auto-complete on mock fields, no reflection surprises. | Already scoped | This is the fundamental differentiator vs all runtime auto-mocking containers |
| **Typed mock fields with real names** | Generated `Mock<IUserRepository> UserRepositoryMock` fields are visible in IDE auto-complete. Moq.AutoMocker requires `mocker.GetMock<IUserRepository>()` runtime calls -- no discoverability, no compile-time safety. | Low | Name derivation from type: `I{Name}` -> `{Name}Mock` convention |
| **Mock setup helper methods** | Generate typed `SetupUserRepository(Action<Mock<IUserRepository>> configure)` methods for readable test arrangement. No existing tool does this at compile time. | Medium | Optional convenience; adds test readability |
| **Configuration mock helpers** | Generate pre-wired `IOptions<T>` / `IConfiguration` setup for services using `[InjectConfiguration]`. No competing tool understands configuration bindings. | Medium | Unique because IoCTools knows configuration dependencies at compile time |
| **Inheritance-aware fixture generation** | Generate fixture base classes that properly handle inherited dependencies, calling base setup methods. Moq.AutoMocker has no concept of this. | High | Leverage existing inheritance chain analysis; test fixture mirrors service hierarchy |
| **Constructor change resilience with compile errors** | When a service constructor changes (new dependency added), the generated fixture updates automatically. Tests that use `CreateSut()` keep compiling. Tests that manually construct break at compile time with clear errors. | Already scoped | Inherent benefit of source generation approach |
| **Suggest IServiceProvider/CreateScope() in IOC012/013** | Current diagnostics say "singleton depends on scoped" but don't tell you how to fix it. Adding the specific workaround pattern is immediately actionable. | Low | Append to existing diagnostic message format string |
| **Full inheritance path in IOC015** | Show `A -> B -> C` chain in diagnostic message so developers see exactly where the lifetime conflict originates. | Low | Data already available from inheritance analysis |
| **Better config error messages with examples (IOC016-019)** | Show what valid configuration looks like, not just "invalid key". Turns cryptic errors into learning moments. | Low | Add example snippets to diagnostic message strings |
| **Color-coded CLI diagnostic output** | Red/yellow/cyan severity coloring makes scanning output fast. Professional CLI polish. | Low | Use `System.Console` ANSI escapes with terminal detection |
| **Fuzzy type suggestions across all CLI commands** | WhyPrinter already does this. Extending to all commands gives consistent "did you mean?" experience. | Low | Extract WhyPrinter fuzzy logic into shared utility |
| **Wildcard/regex support in CLI FilterByType** | Power users filtering large service graphs need pattern matching. | Low | `Regex` or glob pattern matching on type name strings |
| **.editorconfig recipe for diagnostic suppression** | One-command setup for teams that want to suppress specific IoCTools diagnostics project-wide. | Low | Generate `.editorconfig` entries like `dotnet_diagnostic.IOC001.severity = none` |

### Documentation Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Diagnostics reference page** | Searchable table of all 90+ diagnostics with code, severity, description, and fix guidance. No competing source generator has this quality of diagnostic documentation. | Medium | Already have the data in CLAUDE.md; needs public-facing format |
| **Attribute reference page** | One-page reference for all attributes with examples. Faster than reading source or README. | Medium | Extract from existing sample code |
| **CLI reference page** | Command reference with examples for all 9+ commands. | Low | Extract from existing CLI help text |

## Anti-Features

Features to explicitly NOT build.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **NSubstitute / FakeItEasy support** | Splits generated code complexity, doubles maintenance surface. Moq has ~75% market share in .NET mocking. Ship Moq-only for v1, revisit only on demonstrated demand. | Document as intentional scope; accept PRs if community contributes |
| **Runtime auto-mocking container** | Moq.AutoMocker already does this well. Competing on runtime auto-mocking is a losing battle. The value is compile-time generation. | Point users to Moq.AutoMocker for runtime needs; IoCTools.Testing is the compile-time complement |
| **Test data generation (AutoFixture territory)** | AutoFixture solves test data creation. Generating fake data is orthogonal to mock wiring. Scope creep with no competitive advantage. | Recommend AutoFixture alongside IoCTools.Testing in docs |
| **CodeFixProvider for diagnostics** | Requires separate analyzer package, high complexity, separate build/test infrastructure. ROI is low for current user base. | HelpLinkUri links to docs with manual fix instructions |
| **DocFX / full documentation site** | Over-engineering for a library with ~90 diagnostic codes and ~15 attributes. GitHub README + a few markdown pages in the repo is sufficient. Full DocFX sites need ongoing maintenance. | Multi-page markdown in repo (GitHub renders natively); consider DocFX only if community grows significantly |
| **Test assertion helpers** | FluentAssertions already provides excellent assertion syntax. Building custom assertion methods fragments the ecosystem. | Document recommended FluentAssertions patterns in test fixture guide |
| **Mocking sealed classes / non-virtual methods** | MockMe and similar tools solve this with IL weaving. Source generators cannot mock sealed types without fundamentally different architecture. Out of scope. | Document limitation; recommend MockMe for sealed class scenarios |
| **xUnit/NUnit-specific fixture integration** | Generating `[ClassFixture]` or `[TestFixture]` wiring adds framework coupling. Generated base classes work with any test framework. | Generate plain base classes; let users integrate with their test framework |
| **Progress indicators for CLI** | Most ops complete in 1-5 seconds. Spinner/progress bar adds complexity for no user benefit. | Skip entirely |

## Feature Dependencies

```
IoCTools.Testing Package:
  Mock<T> field generation -> SUT factory method (factory needs fields)
  SUT factory method -> Inheritance-aware fixtures (inheritance needs basic factory working first)
  Configuration mock helpers -> Mock<T> field generation (config helpers extend the field pattern)
  Mock setup helpers -> Mock<T> field generation (setup methods reference fields)

typeof() Diagnostics:
  typeof() argument parsing (foundation) -> IOC090, IOC091, IOC092, IOC094 (all depend on parser)
  Integration tests -> all IOC090-094 implementations

Diagnostic UX:
  HelpLinkUri -> Documentation pages must exist first (or use placeholder URLs)
  IDE categories -> No dependencies; can ship independently

CLI:
  --json output mode -> Requires output abstraction (touches all printer classes)
  --verbose -> No dependencies
  Color-coded output -> No dependencies
  Fuzzy suggestions -> Extract WhyPrinter logic first
  Wildcard/regex -> No dependencies

Documentation:
  Getting started guide -> Stable API (write last)
  Diagnostics reference -> HelpLinkUri (URLs must match)
  IoCTools.Testing guide -> Package must be implemented first
```

## MVP Recommendation

Prioritize for IoCTools.Testing v1:

1. **Mock<T> field generation** -- Core value, all other features build on this
2. **SUT factory method (CreateSut())** -- Eliminates the #1 boilerplate complaint; constructor change resilience is the killer feature
3. **Separate NuGet package** -- Ship correctly from day one; retrofitting package boundaries is painful
4. **typeof() diagnostics (IOC090-094)** -- Parallel workstream; no dependency on testing package
5. **HelpLinkUri on all descriptors** -- Low effort, high polish; do early before docs structure is finalized
6. **IDE diagnostic categories** -- Low effort, ships with HelpLinkUri changes

Defer to post-v1:
- **Mock setup helper methods**: Nice-to-have convenience, not blocking adoption
- **Configuration mock helpers**: Complex to get right; defer until basic fixture gen is proven
- **Inheritance-aware fixtures**: High complexity; basic single-level fixtures cover 80% of use cases
- **DocFX or full documentation site**: Markdown pages in repo sufficient for current scale
- **Color-coded CLI output**: Polish item; defer to post-launch

## Competitive Landscape Summary

| Tool | Approach | Strengths | Gaps IoCTools.Testing Fills |
|------|----------|-----------|---------------------------|
| **Moq.AutoMocker** | Runtime auto-mocking container | Zero setup, works with any class | No IDE discoverability for mocks, runtime reflection overhead, no compile-time safety, no constructor change detection |
| **AutoFixture.AutoMoq** | Runtime auto-mocking + test data | Test data generation + mocking in one | Same runtime limitations as AutoMocker; heavier dependency |
| **MockSourceGenerator** | Compile-time mock generation | Generates mock implementations | Generates mocks themselves, not test fixtures with SUT wiring |
| **SourceMock** | Compile-time mock framework | Source-generated mocks | Replaces Moq entirely; different philosophy. Not a fixture generator. |
| **MockMe** | Compile-time mock framework | Can mock sealed classes | Replaces Moq; not a fixture/scaffolding tool |
| **Unit Test Boilerplate Generator** | VS extension, one-time scaffolding | Quick initial generation | One-shot; doesn't update when constructor changes. IDE-specific. |
| **IoCTools.Testing** | Compile-time fixture generation from DI graph | Typed fields, auto-update on constructor change, IDE auto-complete, zero reflection | New approach: leverages existing DI graph knowledge for test scaffolding |

**The gap IoCTools.Testing uniquely fills:** No existing tool generates typed, auto-updating test fixture base classes from compile-time DI graph knowledge. Runtime tools (AutoMocker, AutoFixture) lack IDE discoverability and compile-time safety. One-shot generators (VS extension) don't update when constructors change. Source-generated mock frameworks (MockMe, SourceMock) replace Moq rather than scaffolding fixtures around it.

## Sources

- [Moq.AutoMocker GitHub](https://github.com/moq/Moq.AutoMocker) -- runtime auto-mocking container API reference
- [AutoFixture.AutoMoq NuGet](https://www.nuget.org/packages/autofixture.automoq/) -- AutoFixture Moq integration
- [MockSourceGenerator GitHub](https://github.com/hermanussen/MockSourceGenerator) -- compile-time mock generation
- [SourceMock GitHub](https://github.com/ashmind/SourceMock) -- source-generated mocking framework
- [MockMe NuGet](https://www.nuget.org/packages/MockMe) -- compile-time mock scaffolding
- [Unit Test Boilerplate Generator](https://marketplace.visualstudio.com/items?itemName=RandomEngy.UnitTestBoilerplateGenerator) -- VS extension for test scaffolding
- [DocFX](https://dotnet.github.io/docfx/) -- .NET documentation generator
- [NuGet README best practices](https://devblogs.microsoft.com/dotnet/write-a-high-quality-readme-for-nuget-packages/) -- Microsoft guidance on NuGet documentation
- [Moq.AutoMocker DEV Community overview](https://dev.to/sparky/these-are-a-few-of-my-favorite-tools-moq-automock-3ka3) -- feature walkthrough
- [AutoFixture auto-mocking patterns](https://dev.to/pedrostc/til-using-autofixture-with-auto-mocking-is-awesome-2phd) -- Frozen attribute pattern
