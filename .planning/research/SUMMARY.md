# Project Research Summary

**Project:** IoCTools v1.4.0 Milestone
**Domain:** .NET source generator library expansion — test fixtures, diagnostics, CLI tooling, documentation
**Researched:** 2026-03-21
**Confidence:** HIGH

## Executive Summary

IoCTools v1.4.0 is a multi-workstream milestone adding four distinct capabilities on top of the existing production-ready v1.3.0 foundation: (1) a new `IoCTools.Testing` NuGet package that generates compile-time Moq-based test fixture base classes from the existing DI graph, (2) `typeof()` diagnostic coverage (IOC090-094) extending the `ManualRegistrationValidator` to detect non-generic DI registration patterns, (3) Diagnostic UX polish (HelpLinkUri, IDE categories), and (4) documentation improvements. Each workstream is largely independent, though the testing package and documentation work should be sequenced carefully to avoid rework. The most technically novel work is `IoCTools.Testing` — the architecture is well-understood but the implementation must navigate Roslyn's analyzer loading model and cross-assembly metadata analysis carefully.

The recommended approach is to treat `IoCTools.Testing` as a completely separate `IIncrementalGenerator` that shares source code with the existing generator via compile-includes (not a shared DLL or a project reference). The fixture generator runs on the *test project* compilation, uses `INamedTypeSymbol.InstanceConstructors` to read generated constructor signatures from metadata, and emits partial classes with typed `Mock<T>` fields and `CreateSut()` factory methods. The typeof() diagnostic work is a contained extension to `ManualRegistrationValidator` and has a clear insertion point at line 89 where the validator currently returns early for non-generic calls. Both workstreams should be preceded by a code-quality pass to tighten bare exception catches that could silently swallow new bugs.

The primary risks are: (a) the package boundary for `IoCTools.Testing` must be correct from the start — Moq cannot be a direct dependency of the package, only a peer dependency in the consumer's test project; (b) the typeof() diagnostic must navigate `TypeOfExpressionSyntax.Type` correctly or silently fail to fire; and (c) documentation changes must keep the README as the NuGet landing page and must not create HelpLinkUri links before the target pages exist. None of these risks are blockers — they are well-defined and have clear prevention strategies.

## Key Findings

### Recommended Stack

The milestone requires no new technology decisions for its core work. The typeof() diagnostics and diagnostic UX improvements use existing Roslyn 4.5.0 APIs already in the codebase. The `IoCTools.Testing` package adds only one meaningful new dependency: Moq 4.20.72 as a *peer dependency* (not a direct package dependency). The generator itself, like the existing generators, targets `netstandard2.0`. Documentation work stays with structured markdown in `/docs/` — DocFX is explicitly deferred unless content volume justifies it.

**Core technologies:**
- **Moq 4.20.72**: Mock generation target for `IoCTools.Testing` generated code — 138M+ downloads, dominant .NET mocking framework, stable `Mock<T>` / `.Object` / `.Setup()` API since 4.x; treat as peer dependency, do NOT take direct package reference
- **Microsoft.CodeAnalysis.CSharp 4.5.0**: Roslyn APIs for `IoCTools.Testing` — must match the pinned version in the existing generator exactly; mixing versions causes analyzer load failures
- **System.Text.Json (built-in)**: `--json` output mode for CLI — already available in net8.0, no new dependency needed
- **DocFX 2.78.5**: Documentation tooling — deferred; use `/docs/` markdown first, evaluate DocFX only if content exceeds ~500 lines of structured reference material
- **IoCTools.Abstractions 1.3.0+**: Attribute detection in the testing generator — the attributes are preserved in compiled assemblies, readable via `INamedTypeSymbol.GetAttributes()` from test project context

### Expected Features

**Must have (table stakes):**
- Auto-declare `Mock<T>` properties for all constructor dependencies in fixture — this is the table stakes equivalent of what Moq.AutoMocker does at runtime
- `CreateSut()` factory method wiring all mocks into service constructor — eliminates constructor-change-breaking boilerplate; this is the core value proposition
- Separate `IoCTools.Testing` NuGet package — test dependencies must never appear in production packages
- IOC090-094 typeof() diagnostics — parity with existing IOC081-086 generic registration diagnostics
- HelpLinkUri on all 87+ diagnostic descriptors — IDEs surface these as clickable links; missing links feel unfinished
- IDE diagnostic categories (Lifetime, Dependency, Registration, etc.) — VS Error List filtering at 86+ diagnostics requires categories

**Should have (competitive):**
- Compile-time fixture generation with typed mock field names (`UserRepositoryMock`) visible in IDE auto-complete — stronger than Moq.AutoMocker's runtime `mocker.GetMock<T>()` calls
- Mock setup helper methods (`SetupFoo(Action<Mock<IFoo>> configure)`) — readable test arrangement, no existing tool does this at compile time
- `IOptions<T>` configuration helpers using `Options.Create()` (not `Mock<IOptions<T>>`) — standard .NET testing pattern; IoCTools uniquely knows config dependencies at compile time
- Inheritance-aware fixture hierarchy mirroring service hierarchy
- Color-coded CLI output via ANSI escapes — professional polish with no heavyweight dependency
- `--json` and `--verbose` flags for all CLI commands — CI/CD and debugging support

**Defer (v2+):**
- NSubstitute / FakeItEasy support — different API surface requires different generator; Moq is ~75% market share
- CodeFixProvider for diagnostics — separate analyzer package, high complexity, low ROI for current user base
- DocFX full documentation site — over-engineering for current content volume
- Inheritance-aware fixtures — high complexity; basic single-level covers 80% of cases
- Configuration mock helpers — complex to get right; defer until basic fixture gen is proven
- xUnit/NUnit-specific fixture integration — generates plain base classes, let users integrate with their framework

### Architecture Approach

`IoCTools.Testing` must be a standalone `IIncrementalGenerator` in its own NuGet package, sharing source with the existing generator via compile-includes (`<Compile Include="..." />`), not via a shared DLL or project reference. The test fixture generator runs on the test project compilation — where services are visible as referenced assembly metadata symbols, not syntax — so it reads dependency information via `INamedTypeSymbol.InstanceConstructors` on the already-generated production constructors, which is the simplest and most reliable approach. Activation uses naming convention first (`{ServiceName}Tests` → generates fixture for `ServiceName`), with an optional `[GenerateFixture]` attribute as a follow-up for non-standard names.

**Major components:**
1. **IoCTools.Abstractions** (unchanged) — public attributes consumed by user code
2. **IoCTools.Generator** (extended) — adds typeof() diagnostics (IOC090-094) by extending `ManualRegistrationValidator` at the `typeArgsSymbol.Length == 0` early-return insertion point; adds HelpLinkUri and IDE category strings to `DiagnosticDescriptors.cs`
3. **IoCTools.Testing** (new) — standalone source generator; compile-includes shared Analysis/Models/Utilities from Generator source; emits partial classes with `Mock<T>` property declarations, `InitializeMocks()`, and `CreateSut()` based on constructor metadata
4. **IoCTools.Testing.Tests** (new) — tests for fixture generator; integration tests must use cross-project service references (not inline source) to validate metadata symbol analysis

### Critical Pitfalls

1. **typeof() GetTypeInfo on wrong node** — Calling `GetTypeInfo()` on the `TypeOfExpressionSyntax` expression gives `System.Type`, not the inner type. Navigate to `TypeOfExpressionSyntax.Type` (the syntax node inside the parentheses) before resolving. Detection: test that `services.AddScoped(typeof(IFoo), typeof(Bar))` fires IOC091 when `Bar` is already registered by IoCTools.

2. **IoCTools.Testing leaks Moq into production** — Do not take a `PackageReference` on Moq in `IoCTools.Testing.csproj`. Instead, generate source that references `Mock<T>` and document Moq as a required peer dependency in the test project. Emit a diagnostic when `Moq.Mock\`1` is not resolvable in the consumer's compilation rather than generating uncompilable code.

3. **Test fixture generator cannot see dependencies via syntax in referenced assemblies** — The generator runs on the test compilation. Service classes from the production project are available only as metadata symbols (`INamedTypeSymbol`), not as syntax nodes. Use `INamedTypeSymbol.InstanceConstructors` to read the constructor parameters that IoCTools.Generator already generated — do not attempt syntax-based dependency discovery.

4. **Silent exception swallowing masks new bugs** — Multiple bare `catch (Exception)` blocks in `ConstructorGenerator`, `InterfaceDiscovery`, and `ServiceRegistrationGenerator` will swallow failures from new code paths. Audit and tighten exception handling before adding new validators or generators. New code must never use bare catch blocks.

5. **Two generators loaded in same compilation can conflict** — `IoCTools.Generator` (transitive from the production project reference) and `IoCTools.Testing` (direct reference in test project) both run against the test compilation. They must be fully independent — no shared DLL, same pinned Roslyn version (4.5.0), compile-includes only for shared code. Test the two-generator scenario explicitly.

## Implications for Roadmap

Based on research, the milestone has four largely independent workstreams with a recommended sequencing based on dependencies and risk.

### Phase 1: Code Quality Foundations
**Rationale:** Silent exception swallowing (Pitfall 9) is a pre-existing issue that will mask bugs in every subsequent phase. Tightening the three documented bare catch blocks costs little time and prevents invisible failures during development of new features. MSBuild property naming inconsistency (Pitfall 12, `IoCToolsUnregisteredSeverity` vs `IoCToolsManualSeverity`) should also be resolved here so the convention is established before new properties are added.
**Delivers:** Audited exception handling; MSBuild property naming convention documented and corrected in sample project
**Addresses:** Silent failure risk affecting typeof() diagnostics and test fixture generation
**Avoids:** Pitfalls 9 and 12

### Phase 2: Diagnostic UX Improvements
**Rationale:** HelpLinkUri and IDE category changes are mechanical across existing 87+ descriptors but must be done before documentation pages are created (since the URLs must be consistent). Doing this early sets the URL pattern for all subsequent documentation. Also the lowest-risk, highest-polish-per-effort work in the milestone — good to ship early and builds confidence.
**Delivers:** HelpLinkUri on all diagnostic descriptors; IDE category strings (Lifetime, Dependency, Registration, Configuration, CodeQuality) replacing flat "IoCTools" category
**Addresses:** "Missing links feel unfinished" table stakes; VS Error List filtering usability
**Avoids:** Pitfall 10 (HelpLinkUri before docs exist — Phase 3 creates the stub pages immediately after)

### Phase 3: Documentation Overhaul
**Rationale:** HelpLinkUri links from Phase 2 must resolve. Documentation must exist before IoCTools.Testing is shipped (users need a guide). Content structure decisions here determine whether `/docs/` markdown suffices or DocFX is warranted. Do documentation now, while the target URL pattern from Phase 2 is fresh, and before the IoCTools.Testing guide needs to be written.
**Delivers:** Restructured README as landing page; `/docs/` folder with getting-started guide, diagnostics reference (all 90+ diagnostics), attribute reference, CLI reference; stub pages at HelpLinkUri target URLs
**Addresses:** NuGet consumer documentation complaints; getting-started friction; diagnostic discoverability
**Avoids:** Pitfall 7 (README fragmentation and NuGet page breakage) and Pitfall 10 (404 HelpLinkUri targets)

### Phase 4: typeof() Diagnostics (IOC090-094)
**Rationale:** Self-contained extension to `ManualRegistrationValidator` with a clear insertion point. No new packages, no new dependencies. Delivers tangible user-visible diagnostic value. Completing this before IoCTools.Testing lets the test suite grow to 1650+ tests and validates that the exception handling from Phase 1 is working correctly. Parallel-eligible with Phase 3.
**Delivers:** IOC090 (suggest attributes for unattributed typeof registration), IOC091 (duplicate IoCTools registration via typeof), IOC092 (lifetime mismatch via typeof), IOC094 (open generic typeof); integration tests for all four; MSBuild configurable severity via `IoCToolsTypeOfSeverity`
**Addresses:** Missing parity between generic (`AddScoped<IFoo, Foo>()`) and typeof (`AddScoped(typeof(IFoo), typeof(Foo))`) migration guidance
**Avoids:** Pitfalls 1 (wrong GetTypeInfo node), 5 (open generic syntax), 12 (MSBuild property naming)

### Phase 5: IoCTools.Testing Package
**Rationale:** Most complex and novel work in the milestone. Requires Phases 1-4 to be complete: exception handling must be solid (Phase 1), documentation must exist for the new package guide (Phase 3), and the test suite infrastructure is validated (Phase 4). The package architecture decision (standalone generator, compile-include shared source, peer-dependency Moq) must be locked in before any code is written.
**Delivers:** `IoCTools.Testing` NuGet package; `TestFixtureGenerator` as standalone `IIncrementalGenerator`; generated partial classes with `Mock<T>` property declarations, `InitializeMocks()`, `CreateSut()`; naming convention activation (`{ServiceName}Tests`); diagnostic for missing Moq reference; `IoCTools.Testing.Tests` project with cross-project integration tests
**Addresses:** Compile-time test fixture generation table stakes; typed mock fields with IDE auto-complete; constructor-change resilience
**Avoids:** Pitfalls 2 (Moq leaks to production), 3 (Moq version fragmentation — generate only basic API surface), 4 (metadata symbol analysis via InstanceConstructors), 6 (two generators conflict), 8 (uncompilable fixtures for edge cases — scope v1 to interface-typed dependencies only)
**Uses:** Moq 4.20.72 (peer), Microsoft.CodeAnalysis.CSharp 4.5.0, compile-includes from Generator source

### Phase Ordering Rationale

- Code quality (Phase 1) must precede everything because silent exception swallowing can mask bugs in all new code.
- Diagnostic UX (Phase 2) must precede documentation (Phase 3) because the HelpLinkUri URL pattern must be decided before stub pages are created.
- typeof() diagnostics (Phase 4) can proceed in parallel with documentation (Phase 3) since they are independent workstreams; ordering here is risk-management (documentation validates the process, then diagnostics add features).
- IoCTools.Testing (Phase 5) is last because it is the highest-complexity, highest-risk work and benefits from the clean foundation provided by all earlier phases; its documentation guide also requires Phase 3 infrastructure to be in place.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 5 (IoCTools.Testing):** The compile-include strategy for sharing source between generators needs a spike to validate it works correctly with the existing `DependencyAnalyzer`, `AttributeTypeChecker`, and model types — some may have dependencies that don't compile cleanly in isolation. Also, the naming convention activation logic (`{ServiceName}Tests` → look up `ServiceName` in referenced assemblies) needs a prototype to confirm metadata lookup is reliable.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Code Quality):** Standard code audit — find bare catch blocks, tighten them. No research needed.
- **Phase 2 (Diagnostic UX):** Mechanical change to `DiagnosticDescriptors.cs`. Pattern is clear.
- **Phase 3 (Documentation):** Content authoring, no technical unknowns.
- **Phase 4 (typeof() Diagnostics):** Architecture file documents the exact insertion point and Roslyn API patterns. The open generic case may need a brief spike test but the approach is well-defined.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | No new external dependencies except Moq (peer). All decisions supported by existing codebase analysis and official documentation. |
| Features | HIGH | MVP scope is clear. Competitive landscape well-understood. Anti-features explicitly identified prevent scope creep. |
| Architecture | HIGH | Compile-include strategy, package boundary decisions, and metadata-based dependency analysis are all verified against official Roslyn documentation and the existing codebase. |
| Pitfalls | HIGH | Most pitfalls were identified via direct codebase analysis (ManualRegistrationValidator line 89, CONCERNS.md bare catch blocks) rather than inference. Open generic typeof syntax (Pitfall 5) is MEDIUM — needs spike test. |

**Overall confidence:** HIGH

### Gaps to Address

- **Compile-include scope validation:** The architecture recommends including `DependencyAnalyzer`, `AttributeTypeChecker`, and model types from the Generator into the Testing generator. This list may be incomplete or may include files with transitive dependencies that require additional includes. Address with a spike during Phase 5 planning.
- **Naming convention edge cases:** The `{ServiceName}Tests` activation strategy will have edge cases (generic services like `Repository<T>Tests`, nested classes, partial name matches). These need explicit test coverage and documented fallback behavior. Address during Phase 5 feature definition.
- **Open generic typeof syntax:** Pitfall 5 is rated MEDIUM confidence. The `OmittedTypeArgumentSyntax` detection approach for `typeof(IRepository<>)` should be validated with a Roslyn spike before Phase 4 implementation begins.
- **FluentAssertions v7 migration timeline:** New tests in Phases 4 and 5 should use `HaveCount(1)` over `ContainSingle()` as a forward-compatibility measure, but the actual v7 upgrade timeline is undetermined.

## Sources

### Primary (HIGH confidence)
- [Roslyn Source Generator Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md) — generator packaging, cross-project analysis, incremental generator patterns
- [Moq 4.20.72 on NuGet](https://www.nuget.org/packages/moq/) — 138M+ downloads, API surface stability
- [Moq.AutoMock GitHub + Source Generators docs](https://github.com/moq/Moq.AutoMocker) — prior art for test fixture source generation; rationale for not depending on it
- [DocFX 2.78.5 release](https://github.com/dotnet/docfx/releases) — .NET Foundation project, Feb 2025 release
- IoCTools codebase: `ManualRegistrationValidator.cs` line 89, `CONCERNS.md`, `DiagnosticDescriptors.cs` — direct code analysis
- [Transitive Analyzers NuGet/Home #13813](https://github.com/NuGet/Home/issues/13813) — transitive dependency issues with analyzer packages
- [Meziantou: Working with types in a Roslyn analyzer](https://www.meziantou.net/working-with-types-in-a-roslyn-analyzer.htm) — TypeInfo/GetTypeInfo patterns

### Secondary (MEDIUM confidence)
- [NSubstitute vs Moq comparison](https://blog.dotnetconsult.tech/2025/12/moq-vs-nsubstitute-choosing-right.html) — Moq ~70-75% market share, NSubstitute ~25% and growing
- [Moq SponsorLink Issue](https://github.com/devlooped/moq/issues/1370) — version fragmentation; basis for Pitfall 3 prevention strategy
- Roslyn syntax tree knowledge (OmittedTypeArgumentSyntax for open generics) — needs spike test confirmation

### Tertiary (LOW confidence)
- [Slant .NET doc tools comparison](https://www.slant.co/topics/4111/~documentation-tools-for-net-developers) — community rankings; used only to validate DocFX recommendation, not as sole source

---
*Research completed: 2026-03-21*
*Ready for roadmap: yes*
