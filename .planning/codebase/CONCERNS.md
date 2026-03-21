# Codebase Concerns

**Analysis Date:** 2026-03-21

## Tech Debt

**RegisterAsAll string-literal checks instead of AttributeTypeChecker:**
- Issue: `AttributeTypeChecker` exists for centralized attribute detection, but 10+ locations still use raw string comparisons (`attr.AttributeClass?.Name == "RegisterAsAllAttribute"`) instead of routing through it
- Files: `IoCTools.Generator/IoCTools.Generator/Analysis/TypeAnalyzer.cs`, `IoCTools.Generator/IoCTools.Generator/CodeGeneration/BaseConstructorCallBuilder.cs`, `IoCTools.Generator/IoCTools.Generator/Generator/Pipeline/ServiceClassPipeline.cs`, `IoCTools.Generator/IoCTools.Generator/Generator/ConstructorEmitter.cs`, `IoCTools.Generator/IoCTools.Generator/Generator/DiagnosticsRunner.cs`, `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/MissedOpportunityValidator.cs`, `IoCTools.Generator/IoCTools.Generator/Utilities/ServiceRegistrationScan.cs`, `IoCTools.Generator/IoCTools.Generator/Utilities/DiagnosticScan.cs`
- Impact: If the attribute is ever renamed or its namespace changes, only the `AttributeTypeChecker` path will be updated; raw-string callers will silently break
- Fix approach: Route all `RegisterAsAllAttribute` checks through `AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute)`; tracked in `ideas.md` as "Centralize RegisterAsAllAttribute checks (20 inconsistent locations)"

**ReportDiagnosticDelegate pattern only partially adopted:**
- Issue: The `ReportDiagnosticDelegate` pattern is defined in `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator.RegisterAs.cs` but only used there. Three to four other validators directly call `context.ReportDiagnostic()`, tightening coupling to `SourceProductionContext`
- Files: `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator.RegisterAs.cs`
- Impact: Validators are harder to unit-test in isolation; the pattern exists but is inconsistently applied
- Fix approach: Adopt the delegate pattern in the remaining validators; tracked in `ideas.md`

**All 87 diagnostic descriptors use generic "IoCTools" category:**
- Issue: Every `DiagnosticDescriptor` in `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/` uses `"IoCTools"` as the category string. This prevents IDE tooling from grouping diagnostics by concern area (Lifetime, Dependency, Configuration, Registration, Structural)
- Files: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/DependencyDiagnostics.cs`, `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs`, and all other descriptor files
- Impact: IDE diagnostic filtering is coarse-grained; all 87 diagnostics appear under one bucket
- Fix approach: Assign specific category strings per domain; tracked in `ideas.md`

**No HelpLinkUri on any diagnostic descriptor:**
- Issue: `DiagnosticDescriptorFactory.WithSeverity` preserves `HelpLinkUri` when overriding severity, but no descriptor sets one. The infrastructure is ready; the links are missing.
- Files: All files under `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/`
- Impact: Developers clicking the diagnostic code in IDEs get no documentation link
- Fix approach: Add `https://github.com/nate123456/IoCTools/...` links per diagnostic; tracked in `ideas.md`

**MSBuild property name mismatch between docs and sample:**
- Issue: `CLAUDE.md` documents `IoCToolsManualSeverity` for IOC002 severity configuration; `IoCTools.Sample/IoCTools.Sample.csproj` uses `IoCToolsUnregisteredSeverity`. The generator code at `IoCTools.Generator/IoCTools.Generator/Diagnostics/DiagnosticUtilities.cs:35` only reads `IoCToolsManualSeverity`, so the sample's `IoCToolsUnregisteredSeverity` property is silently ignored
- Files: `IoCTools.Sample/IoCTools.Sample.csproj:18`, `IoCTools.Generator/IoCTools.Generator/Diagnostics/DiagnosticUtilities.cs:33-35`, `CLAUDE.md`
- Impact: The sample's intended IOC002 severity override does nothing at runtime; the documentation and sample demonstrate a property that doesn't work
- Fix approach: Align property name: either rename the generator to read `IoCToolsUnregisteredSeverity`, or update the sample to use `IoCToolsManualSeverity`

**Open generic registration not supported (disabled in sample):**
- Issue: The generic repository registration pattern is commented out in `IoCTools.Sample/Program.cs:1879-1890` with the note "temporarily commented out due to open generic registration issue". The generator has partial open-generic support in `TypeAnalyzer.cs` and `DependencyLifetimeResolver.cs` but it does not correctly handle `services.AddScoped(typeof(IRepository<>), typeof(Repository<>))` style registrations
- Files: `IoCTools.Sample/Program.cs:1879`, `IoCTools.Generator/IoCTools.Generator/Analysis/TypeAnalyzer.cs:65-115`
- Impact: Users with open-generic patterns must register them manually; the feature appears to be in progress but is incomplete
- Fix approach: Implement `services.Add*(typeof(IOpenGeneric<>), typeof(Impl<>))` code generation in `ServiceRegistrationGenerator`

**Inconsistent test assertion syntax (ContainSingle):**
- Issue: 271 usages of `.ContainSingle()` exist across test files. This assertion pattern is inconsistent with the project's stated preference; `ideas.md` tracks standardizing diagnostic assertions, noting 271 usages all in internal test code
- Files: Spread across 114 test files in `IoCTools.Generator.Tests/`
- Impact: Low functional impact; maintainability issue as FluentAssertions v7 introduced breaking changes to this assertion
- Fix approach: Replace `.ContainSingle()` with `.HaveCount(1).And.ContainSingle()` or `.ContainSingle()` kept where appropriate after v7 migration; standardize as part of FA upgrade

## Known Bugs

**RegisterAs without Lifetime registers concrete class when it should not:**
- Symptoms: When `[RegisterAs<IService>]` is used without a lifetime attribute, the generator still emits a concrete class registration (`services.AddScoped<ConcreteClass, ConcreteClass>`) in addition to the interface registration
- Files: `IoCTools.Generator.Tests/RegisterAsBasicTests.cs:31-34`, `IoCTools.Generator.Tests/RegisterAsEdgeCasesTests.cs:92-98`
- Trigger: Use `[RegisterAs<T>]` without `[Scoped]`, `[Singleton]`, or `[Transient]`
- Workaround: Always pair `[RegisterAs<T>]` with an explicit lifetime attribute even when only interface registration is desired

**RegisterAs without Lifetime produces unexpected error diagnostics:**
- Symptoms: Generator emits error-level diagnostics for a valid `[RegisterAs<T>]` usage pattern that should be permitted (interface-only registration without a lifetime attribute)
- Files: `IoCTools.Generator.Tests/RegisterAsEdgeCasesTests.cs:92-96`
- Trigger: Class uses `[RegisterAs<IService>]` without any lifetime attribute, implements the interface correctly
- Workaround: Add explicit lifetime attribute to suppress the erroneous diagnostic

**Mixed `[Inject]`/`[DependsOn]` across inheritance levels causes parameter ordering conflicts:**
- Symptoms: When a base class uses `[Inject][ExternalService]` fields and a derived class uses `[DependsOn<T>(external: true)]`, the generated constructor parameter ordering is incorrect, producing non-compiling output
- Files: `IoCTools.Generator.Tests/InheritanceTests.cs:1500-1515`
- Trigger: Combining `[Inject]` and `[DependsOn]` external patterns across inheritance levels
- Workaround: Use consistent patterns within an inheritance chain: all `[Inject]` OR all `[DependsOn]`, never mixed

## Performance Bottlenecks

**`InterfaceDiscovery.CollectAllInterfacesRecursive` traverses interfaces twice:**
- Problem: `CollectAllInterfacesRecursive` in `IoCTools.Generator/IoCTools.Generator/Utilities/InterfaceDiscovery.cs:34-50` iterates `typeSymbol.Interfaces` and then also calls `typeSymbol.AllInterfaces`, which is a superset. The recursive traversal + `AllInterfaces` fallback results in redundant processing for each type in the hierarchy
- Files: `IoCTools.Generator/IoCTools.Generator/Utilities/InterfaceDiscovery.cs:34-50`
- Cause: The `AllInterfaces` call at line 47 was added as a defensive fallback but is now always executed, duplicating work done by the recursive traversal
- Improvement path: Remove the explicit recursive traversal and rely solely on `AllInterfaces` since Roslyn already provides the full transitive closure; or remove `AllInterfaces` and trust the recursive traversal

**`DiagnosticDescriptorFactory` cache key includes entire `DiagnosticSeverity` but `ConcurrentDictionary` size is unbounded:**
- Problem: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Helpers/DiagnosticDescriptorFactory.cs` caches descriptor overrides in a static `ConcurrentDictionary`. With 87 diagnostics × 4 severity levels, the maximum cache size is 348 entries, which is acceptable. However, the cache is never trimmed and is a static singleton
- Files: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Helpers/DiagnosticDescriptorFactory.cs`
- Cause: Not a current problem but worth noting as diagnostic count grows
- Improvement path: Acceptable as-is; add a comment documenting the bounded maximum size

## Fragile Areas

**`ConstructorGenerator.GenerateConstructorBody` swallows all exceptions:**
- Files: `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorGenerator.cs:437`
- Why fragile: The catch block `catch (Exception) { return ""; }` silently swallows any failure during constructor body generation and returns an empty string. This can produce services that compile but have no constructor, causing runtime DI resolution failures with no build-time signal
- Safe modification: Always test constructor generation changes against the full `IoCTools.Generator.Tests` suite and `IoCTools.Sample` build; add logging via `GeneratorDiagnostics.Report` before returning empty string
- Test coverage: `IoCTools.Generator.Tests/ConstructorGenerationBugTests.cs` covers known scenarios but the bare catch prevents new failure modes from being detected

**`InterfaceDiscovery` silently returns empty on exception:**
- Files: `IoCTools.Generator/IoCTools.Generator/Utilities/InterfaceDiscovery.cs:22-31`
- Why fragile: Catches `InvalidOperationException` and `NullReferenceException` and returns an empty list. A service with no discovered interfaces silently receives no interface registrations. This can produce working builds but incorrect runtime DI wiring
- Safe modification: Add a diagnostic report before returning empty; this is listed as the intended resilience behavior in the comment, but it should be visible to the developer
- Test coverage: No dedicated tests for the failure path

**Bare `catch (Exception)` in `ServiceRegistrationGenerator.RegistrationCode`:**
- Files: `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ServiceRegistrationGenerator.RegistrationCode.cs:90`
- Why fragile: Catches and re-wraps exceptions from `GenerateConditionalServiceRegistrations`. The re-throw only happens if `deduplicatedConditionalServices.Any()`. If the collection is empty and an exception occurs, the exception is swallowed silently
- Safe modification: Remove the `.Any()` guard; always rethrow

**`ConstructorGenerator` silent fallback when `classSymbol` is null:**
- Files: `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorGenerator.cs:76-85`
- Why fragile: When `GetDeclaredSymbol` throws `ArgumentException`, `classSymbol` is set to null and processing continues. Downstream code that depends on `classSymbol` for configuration dependency detection will silently skip those dependencies
- Safe modification: When `classSymbol` is null, emit a diagnostic and return early from the outer method rather than continuing with degraded output

## Test Coverage Gaps

**`ConfigurationInjectionTests.cs` contains ~15 skeleton tests with TODO bodies:**
- What's not tested: Primitive type configuration binding, nullable type handling, `IOptionsMonitor<T>` injection, explicit section name binding, 30-binding performance scenarios
- Files: `IoCTools.Generator.Tests/ConfigurationInjectionTests.cs:780-1730` (sections with `// TODO: When InjectConfiguration is implemented`)
- Risk: These tests pass trivially today (no assertions), masking incomplete coverage of `[InjectConfiguration]` edge cases
- Priority: Medium — the feature is implemented and used in the sample, but the formal test assertions are placeholders

**`ConfigurationInjectionDiagnosticsTests.cs` has two unresolved investigation TODOs:**
- What's not tested: Complex scenario compilation error behavior (line 1012: "expected HasErrors == false eventually"), specific diagnostic message content in one test (line 1083)
- Files: `IoCTools.Generator.Tests/ConfigurationInjectionDiagnosticsTests.cs:1012`, `IoCTools.Generator.Tests/ConfigurationInjectionDiagnosticsTests.cs:1083`
- Risk: A configuration-related compilation error in a complex scenario is acknowledged but untriaged; it may represent a real generator bug in combined inheritance + configuration injection paths
- Priority: High — compilation errors are the most visible user-facing failures

**`DEBUG_*` test methods are permanent test suite members:**
- What's not tested: These methods (`DEBUG_ConditionalService_EnvironmentOnly`, `DEBUG_ConfigurationIntegration_SimpleTest`, `DEBUG_ConfigurationIntegration_OnlyInjectConfigurationTest`) are real, passing tests with exploratory/diagnostic intent but are named as debug artifacts
- Files: `IoCTools.Generator.Tests/ConfigurationInjectionIntegrationTests.cs:107, 166, 204`; `IoCTools.Generator.Tests/LifetimeDependencyAdvancedGenericTests.cs` (`#region DEBUG TESTS`)
- Risk: Low correctness risk; maintainability issue as test names imply they can be removed
- Priority: Low — rename or promote to properly named tests

**`ConditionalServiceDeploymentScenarioTests.cs` has one skipped test:**
- What's not tested: Legacy assertion format scenario
- Files: `IoCTools.Generator.Tests/ConditionalServiceDeploymentScenarioTests.cs:427`
- Risk: Low; the comment states it's covered by other tests
- Priority: Low — either delete or restore with updated assertions

## Missing Critical Features

**`typeof()` argument parsing for `services.AddScoped(typeof(IFoo), typeof(Bar))` patterns:**
- Problem: `ManualRegistrationValidator` cannot parse `typeof()` arguments, so IOC081/082/086 diagnostics do not fire for the `typeof()` registration style. This is the most common style in legacy codebases being migrated to IoCTools
- Blocks: IOC090-094 diagnostics (planned); accurate detection of duplicate/conflicting manual registrations in typeof-style code
- Tracked in: `ideas.md` as the "typeof() Diagnostics" backlog section

**No `--verbose` CLI flag:**
- Problem: When the CLI produces unexpected output or misses services, there is no way to get MSBuild diagnostics, generator timing, or file path information. Debugging requires manually setting MSBuild properties
- Blocks: User self-service debugging of CLI issues
- Tracked in: `ideas.md` as "Add --verbose flag for debugging"

**No JSON output mode for CLI:**
- Problem: CLI output is human-readable only. Integrating IoCTools CLI into CI pipelines or build scripts requires parsing human-readable text
- Blocks: Scripted/automated consumption of registration analysis
- Tracked in: `ideas.md` as "Add JSON output mode for all commands"

## Dependencies at Risk

**Microsoft.CodeAnalysis.CSharp pinned to 4.5.0 (released mid-2022):**
- Risk: Roslyn 4.5.0 is significantly behind the current 4.x series. This version predates incremental generator improvements, enhanced cancellation token propagation, and nullable analysis improvements in later versions
- Impact: Missing incremental compilation optimizations; potential incompatibility with newer SDK tooling that ships newer Roslyn versions
- Migration plan: Evaluate upgrading to 4.8.x or 4.9.x; test against all generator test scenarios before releasing; the `netstandard2.0` target constraint for the generator does not require pinning the Roslyn analysis packages to old versions

**FluentAssertions pinned to 6.12.0 in both test projects:**
- Risk: FluentAssertions v7 introduced breaking changes to assertion syntax. The project is pinned to v6 across `IoCTools.Generator.Tests` and `IoCTools.Tools.Cli.Tests`. When v6 is eventually dropped from support or a transitive dependency forces upgrade, 271 `ContainSingle` usages and other v6-specific patterns will need review
- Impact: Forced upgrade will require widespread test changes
- Migration plan: Plan FA upgrade as a dedicated phase; audit all 271 `ContainSingle` usages and other potentially changed assertion methods before upgrading; tracked partially in `ideas.md`

**Microsoft.Extensions.* packages pinned to 6.0.x in test projects and sample:**
- Risk: `IoCTools.Generator.Tests` and `IoCTools.Sample` reference `Microsoft.Extensions.*` at version 6.0.x while `IoCTools.Tools.Cli.Tests` TestProjects use 8.0.0. The generator itself targets `netstandard2.0` but is being tested against .NET 6.0 extension packages while the CLI test projects use .NET 8 packages
- Impact: Version skew between test environments; diagnostics or code generation behavior that differs between .NET 6 and .NET 8 extension package versions may not be caught
- Migration plan: Align all non-generator test projects to a single consistent `Microsoft.Extensions.*` version

---

*Concerns audit: 2026-03-21*
