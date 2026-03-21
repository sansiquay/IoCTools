# Domain Pitfalls

**Domain:** .NET source generator library expansion (test fixtures, typeof() diagnostics, multi-package, docs)
**Researched:** 2026-03-21

## Critical Pitfalls

Mistakes that cause rewrites, broken packages, or major regressions.

### Pitfall 1: typeof() Argument Resolution Returns Null TypeInfo in Source Generators

**What goes wrong:** The existing `ManualRegistrationValidator` extracts types from generic type arguments (`services.AddScoped<IFoo, Bar>()`). For `typeof()` arguments (`services.AddScoped(typeof(IFoo), typeof(Bar))`), the types are passed as runtime `System.Type` arguments, not generic type parameters. Calling `semanticModel.GetTypeInfo()` on a `TypeOfExpressionSyntax` can return a `TypeInfo` with `Type` set to `System.Type` (the wrapper), not the inner type being referenced. Extracting the actual referenced type requires navigating to `TypeOfExpressionSyntax.Type` (the syntax node inside `typeof(...)`) and then calling `semanticModel.GetTypeInfo()` on that inner syntax node.

**Why it happens:** `typeof(Foo)` is an expression of type `System.Type`. The interesting type (`Foo`) is the *argument* to typeof, not the *result type* of the expression. Developers accustomed to working with generic type arguments (where `IMethodSymbol.TypeArguments` gives direct access) apply the same mental model and call `GetTypeInfo()` on the wrong node.

**Consequences:** IOC090-094 diagnostics silently fail to detect typeof-style registrations. No diagnostics fire, giving users the false impression their typeof registrations are validated. Worse, if the validator falls through to the `IOC086` "could use attributes" codepath, it may emit spurious suggestions for registrations that are already correct.

**Prevention:**
1. Parse `InvocationExpressionSyntax.ArgumentList.Arguments` when `TypeArguments.Length == 0`
2. Check each argument for `TypeOfExpressionSyntax` pattern: `arg.Expression is TypeOfExpressionSyntax typeOf`
3. Resolve the inner type via `semanticModel.GetTypeInfo(typeOf.Type).Type` -- note the `.Type` on the syntax node, not on the expression
4. Handle `null` returns defensively -- `GetTypeInfo` returns null `Type` for unresolved/error types
5. Handle open generics separately: `typeof(IRepository<>)` has an `OmittedTypeArgumentSyntax` inside

**Detection:** Add a test case that uses `services.AddScoped(typeof(IFoo), typeof(Bar))` alongside an IoCTools-attributed `Bar` class and assert that IOC091 fires. If it does not, the typeof parsing is broken.

**Phase relevance:** typeof() Diagnostics phase. This is the core technical challenge of that work item.

**Confidence:** HIGH -- verified by reading the existing `ManualRegistrationValidator.cs` (line 89 returns early when `typeArgsSymbol.Length == 0`, which is exactly the typeof case) and Roslyn semantic model documentation.

### Pitfall 2: IoCTools.Testing Package Leaks Moq/xUnit Dependencies into Production

**What goes wrong:** NuGet source generator packages have unusual dependency semantics. If `IoCTools.Testing` is a source generator (shipping in `analyzers/dotnet/cs/`), its NuGet dependencies do not flow to the consuming project the way normal package dependencies do. But if it is a regular library package (which it likely needs to be, since it generates base classes consumed by test code), then its dependency on Moq becomes a transitive dependency of every project that references it. If someone accidentally references `IoCTools.Testing` in a non-test project, Moq and Castle.Core end up in production assemblies.

**Why it happens:** The natural instinct is to make `IoCTools.Testing` a source generator package (like `IoCTools.Generator`), but test fixtures need to be *compiled code* that test classes inherit from, not just source injected at compile time. If it generates source, the generated source references `Mock<T>` which requires Moq to be present. Either way, Moq ends up as a dependency.

**Consequences:** Production binaries bloated with mocking framework DLLs. Potential assembly version conflicts if the consuming project pins a different Moq version. Confusion about whether `IoCTools.Testing` is a generator or a library.

**Prevention:**
1. Make `IoCTools.Testing` a source generator that emits source code referencing `Mock<T>` -- the consumer's test project must already reference Moq independently
2. Do NOT make `IoCTools.Testing` take a PackageReference on Moq. Instead, document that the consuming test project must reference Moq >= 4.18.0
3. In the generator, check whether `Mock<T>` is resolvable in the compilation via `compilation.GetTypeByMetadataName("Moq.Mock\`1")`. If not, emit a diagnostic ("IoCTools.Testing requires Moq to be referenced in your test project") rather than generating uncompilable code
4. Use `PrivateAssets="all"` and `IncludeAssets="runtime; build; native; contentfiles; analyzers"` in the package to prevent transitive leaking

**Detection:** After creating the NuGet package, create a test that references `IoCTools.Testing` without separately referencing Moq. The build should produce a clear diagnostic, NOT a cryptic CS0246 error about `Mock<T>` not found.

**Phase relevance:** Test Fixture Generation phase. Must be decided in package design before any code is written.

**Confidence:** HIGH -- this is a well-documented pattern in the Roslyn source generator ecosystem. The [Roslyn cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md) and [transitive dependency issues](https://github.com/dotnet/roslyn-sdk/issues/592) confirm the challenges.

### Pitfall 3: Moq Version Fragmentation and SponsorLink Fallout

**What goes wrong:** Moq 4.20.0 (August 2023) included SponsorLink, which harvested developer email addresses. The community split: some pinned to 4.18.4 (pre-SponsorLink), some use 4.20.2+ (SponsorLink removed), some migrated to NSubstitute entirely. If `IoCTools.Testing` generates code targeting a specific Moq API surface, version mismatches between what the generator expects and what the user has installed cause compilation failures.

**Why it happens:** Moq's API has been stable across 4.x, but subtle differences exist. `Mock.Of<T>()` behavior, `MockBehavior` defaults, and certain `Setup`/`Returns` overloads have edge cases across versions. More importantly, if IoCTools.Testing references Moq internals or relies on specific Castle.Core behavior, version mismatches surface as runtime failures in tests, not compile errors.

**Consequences:** Users pinned to Moq 4.18.4 (common in enterprise) get build failures. Users who migrated to NSubstitute cannot use IoCTools.Testing at all. The generated code becomes the single most fragile part of the library.

**Prevention:**
1. Generate ONLY basic Moq API surface: `new Mock<T>()`, `mock.Object`, `mock.Setup(x => x.Method())`. Avoid `Mock.Of<T>()`, `MockRepository`, or anything beyond core Mock class usage
2. Test the generated code against Moq 4.18.4 (pre-SponsorLink minimum) AND 4.20.72 (latest)
3. Do NOT pin a specific Moq version in `IoCTools.Testing`. Let the consumer control their Moq version
4. Document minimum supported Moq version (4.18.0 recommended) in package description
5. Keep the generated mock surface trivially simple -- `Mock<T>` field declarations, `.Object` access, SUT construction -- nothing clever

**Detection:** CI matrix testing against Moq 4.18.4 and 4.20.72. If either fails, the generated API surface is too version-specific.

**Phase relevance:** Test Fixture Generation phase. Affects generated code design from day one.

**Confidence:** MEDIUM -- Moq's core API (`Mock<T>`, `.Object`, `.Setup`) has been stable since 4.x, but the SponsorLink controversy means the user base is fragmented. Verified via [Moq NuGet page](https://www.nuget.org/packages/Moq) and [SponsorLink issue](https://github.com/devlooped/moq/issues/1370).

### Pitfall 4: Source Generator Cannot See Test Project's Service Dependency Graph

**What goes wrong:** `IoCTools.Testing` generates test fixtures based on a service's constructor dependencies. But the generator runs in the *test project's* compilation, not the *production project's* compilation. The service classes are visible (via project reference), but the IoCTools attributes on those classes may not be analyzable the same way -- the test project's compilation sees the *compiled* service types, not the source generator's intermediate representation.

**Why it happens:** Source generators operate on the compilation they are attached to. When `IoCTools.Generator` runs on `MyApp`, it builds a full dependency graph from syntax and semantic analysis. When `IoCTools.Testing` runs on `MyApp.Tests`, the service classes from `MyApp` are available as referenced assembly symbols (`INamedTypeSymbol` from metadata), not as syntax nodes. This means you cannot use `SyntaxProvider` to find `[Inject]` fields in referenced assemblies -- you must use the semantic model and attribute analysis on metadata symbols.

**Consequences:** The test fixture generator produces incomplete fixtures (missing dependencies), or crashes trying to traverse syntax trees that do not exist in the test compilation. Inheritance chains in referenced assemblies may be partially visible.

**Prevention:**
1. Use `INamedTypeSymbol.GetAttributes()` on referenced assembly types to read `[Inject]`, `[DependsOn]`, `[Scoped]` etc. from metadata -- this works because `IoCTools.Abstractions` attributes are preserved in compiled assemblies
2. Use `INamedTypeSymbol.GetMembers().OfType<IFieldSymbol>()` to find injected fields rather than syntax-based discovery
3. Use `INamedTypeSymbol.InstanceConstructors` to read the generated constructor's parameter list directly -- the simplest and most reliable approach, since the constructor was already generated by `IoCTools.Generator` in the production build
4. Build a comprehensive test that puts the service in a referenced project (not the same project as the test) and verify the fixture generator sees all dependencies

**Detection:** Integration test where service classes are in a separate assembly (project reference), not inline source code. If the fixture is incomplete, the generator is relying on syntax analysis that only works for same-project types.

**Phase relevance:** Test Fixture Generation phase. This is an architectural decision that must be made before implementation.

**Confidence:** HIGH -- this is a fundamental characteristic of how Roslyn source generators interact with referenced assemblies, confirmed by the [Roslyn source generator cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md).

## Moderate Pitfalls

### Pitfall 5: Open Generic typeof() Parsing Has Different Syntax Structure

**What goes wrong:** `typeof(IRepository<>)` has an `OmittedTypeArgumentSyntax` inside the generic parameter list, while `typeof(IRepository<Customer>)` has a regular `IdentifierNameSyntax`. Code that assumes all typeof arguments resolve to concrete types via `GetTypeInfo()` will get `null` or error types for open generics.

**Prevention:**
1. After extracting `TypeOfExpressionSyntax.Type`, check if it is a `GenericNameSyntax` with any `OmittedTypeArgumentSyntax` children
2. For open generics, use `semanticModel.GetSymbolInfo()` instead of `GetTypeInfo()` to get the `INamedTypeSymbol` with `IsUnboundGenericType == true`
3. Emit IOC094 (open generic could use IoCTools) only for this pattern; do not conflate with closed generic typeof

**Detection:** Test case: `services.AddScoped(typeof(IRepository<>), typeof(Repository<>))` must trigger IOC094, not IOC090.

**Phase relevance:** typeof() Diagnostics phase.

**Confidence:** MEDIUM -- based on Roslyn API documentation and general syntax tree structure knowledge. Should be verified with a spike test.

### Pitfall 6: Multi-Package Generator Assembly Loading Conflicts

**What goes wrong:** When `IoCTools.Generator` and `IoCTools.Testing` are both source generators loaded into the same compilation, they can conflict if they share internal types or depend on different versions of shared dependencies. Roslyn loads each generator into the compiler process, and assembly version conflicts between generators cause `ReflectionTypeLoadException` or silent generator failure.

**Prevention:**
1. `IoCTools.Testing` must NOT reference `IoCTools.Generator` as a project/package reference. They must be fully independent generators
2. If both generators need shared utility code (e.g., attribute name constants, lifetime parsing), extract that into `IoCTools.Abstractions` as compile-time constants, or duplicate the minimal shared code in each generator
3. Ensure both packages target compatible `Microsoft.CodeAnalysis.CSharp` versions (ideally the same pinned version)
4. Test the scenario where both generators are referenced in the same test project

**Detection:** Create a test project that references both `IoCTools.Generator` (transitively via the production project) and `IoCTools.Testing` directly. Build should succeed without assembly loading warnings.

**Phase relevance:** Test Fixture Generation phase (package structure design).

**Confidence:** HIGH -- [transitive analyzer flow-through](https://github.com/NuGet/Home/issues/13813) and [multi-SDK version support](https://andrewlock.net/supporting-multiple-sdk-versions-in-analyzers-and-source-generators/) document these issues extensively.

### Pitfall 7: Documentation Migration Breaks Existing Links and Discoverability

**What goes wrong:** Migrating from a single README.md to multi-page documentation (e.g., `/docs/getting-started.md`, `/docs/attributes.md`, `/docs/diagnostics.md`) breaks all existing links to README.md sections (anchor links like `#diagnostics`). NuGet package pages render only the README.md, not a docs folder. GitHub renders README.md on the repo landing page but multi-page docs require navigation.

**Prevention:**
1. Keep README.md as a concise landing page with links to detailed docs, not as a redirect
2. Do NOT move diagnostic reference tables out of README.md -- NuGet users need this visible without navigating to a docs site
3. If migrating to multi-page, use a `/docs/` folder in the repo, not a separate docs site (keeps everything in one place, no deployment needed)
4. Add a table of contents to README.md that links to each doc page
5. Keep the most-referenced content (quick start, installation, diagnostic table) in README.md itself
6. Move deep-dive content (full attribute reference, architecture guide, test fixtures guide) to `/docs/`

**Detection:** After migration, check that the NuGet.org package page still shows useful content. Check that GitHub repo landing page still serves as a complete getting-started guide.

**Phase relevance:** Documentation Overhaul phase.

**Confidence:** HIGH -- standard open-source documentation pattern. The [google/wire docs split discussion](https://github.com/google/wire/issues/84) illustrates the tradeoffs.

### Pitfall 8: Test Fixture Generator Produces Uncompilable Code for Edge Cases

**What goes wrong:** The production `IoCTools.Generator` has extensive handling for inheritance chains, generic services, configuration injection, and mixed dependency patterns. The test fixture generator must handle the same breadth of service shapes. Common failures: generic type parameter propagation in Mock declarations (`Mock<IRepository<T>>` requires the test class to also be generic), configuration objects that are not mockable (POCO classes, not interfaces), and services with `IEnumerable<T>` collection dependencies that need `List<Mock<T>>` patterns.

**Prevention:**
1. Start with the simplest case only: services with interface-typed `[Inject]`/`[DependsOn]` dependencies. Skip configuration injection and collection dependencies in v1
2. For non-mockable types (concrete classes, value types, `IOptions<T>`), generate a field with a default value or factory method, not a `Mock<T>` -- `Mock<T>` only works for interfaces and virtual classes
3. For `IOptions<T>`, generate `Options.Create(new T())` instead of `Mock<IOptions<T>>`
4. For generic services, generate generic test fixture base classes that propagate type parameters
5. Emit a diagnostic (not a crash) when a dependency type cannot be mocked, explaining why and suggesting manual setup

**Detection:** Test against every service shape in the existing `IoCTools.Sample` project. If any generated fixture does not compile, that service shape needs handling or an explicit skip-with-diagnostic.

**Phase relevance:** Test Fixture Generation phase.

**Confidence:** MEDIUM -- based on analysis of the sample project's service diversity and general Moq limitations. The specific edge cases need spike testing.

### Pitfall 9: Existing Silent Exception Swallowing Masks New Bugs

**What goes wrong:** CONCERNS.md documents multiple bare `catch (Exception)` blocks that return empty strings or empty collections: `ConstructorGenerator` (line 437), `InterfaceDiscovery` (lines 22-31), `ServiceRegistrationGenerator.RegistrationCode` (line 90). When adding typeof() diagnostics or test fixture generation, new code paths may trigger these catch blocks, causing silent failures rather than diagnostic reports. A typeof() parsing bug could be swallowed by the existing catch in `ManualRegistrationValidator`'s caller chain.

**Prevention:**
1. Before adding new diagnostic validators, audit and tighten exception handling in the code paths they flow through
2. Add at minimum a `Debug.WriteLine` or generator diagnostic to each bare catch before starting new feature work
3. New validators (typeof diagnostics, test fixture analysis) should never use bare catch blocks -- always emit a diagnostic on failure

**Detection:** Run the full test suite with `[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage]` removed and check for swallowed exceptions. Alternatively, add `#if DEBUG throw;` to existing catch blocks during development.

**Phase relevance:** Code Quality phase (should be addressed before typeof() Diagnostics and Test Fixture phases).

**Confidence:** HIGH -- directly verified in CONCERNS.md with file and line references.

## Minor Pitfalls

### Pitfall 10: HelpLinkUri Documentation Must Exist Before Links Are Added

**What goes wrong:** Adding `HelpLinkUri` to diagnostic descriptors before the documentation pages exist creates broken links. IDEs render these as clickable links, and a 404 page is worse than no link.

**Prevention:** Create the documentation pages (even as stubs) before merging the HelpLinkUri additions. Use a URL pattern that is stable: `https://github.com/nate123456/IoCTools/blob/main/docs/diagnostics/{IOC_CODE}.md`.

**Phase relevance:** Diagnostic UX Improvements phase. Must coordinate with Documentation Overhaul phase.

**Confidence:** HIGH.

### Pitfall 11: FluentAssertions v6 ContainSingle Pattern in New Tests

**What goes wrong:** New test code written for typeof() diagnostics and test fixture generation will naturally use the same `ContainSingle()` pattern seen in the existing 271 usages. If a FluentAssertions upgrade happens later, these new usages will also need migration.

**Prevention:** For new test code, prefer `.Should().HaveCount(1)` followed by specific element assertions, rather than `.ContainSingle()`. This is forward-compatible with FluentAssertions v7.

**Phase relevance:** All phases that add tests.

**Confidence:** MEDIUM -- FluentAssertions v7 breaking changes are documented but the timeline for forced upgrade is uncertain.

### Pitfall 12: MSBuild Property Name Mismatch Propagates to New Diagnostics

**What goes wrong:** CONCERNS.md documents that `IoCToolsUnregisteredSeverity` in the sample .csproj is silently ignored because the generator reads `IoCToolsManualSeverity`. If new typeof() diagnostics (IOC090-094) add configurable severity via new MSBuild properties, the same mismatch pattern could recur.

**Prevention:** Establish a naming convention for MSBuild properties before adding new ones. Suggested: `IoCTools{DiagnosticCategory}Severity` (e.g., `IoCToolsTypeOfSeverity`). Add an integration test that sets each property and verifies the severity changes in generated diagnostics.

**Phase relevance:** typeof() Diagnostics phase.

**Confidence:** HIGH -- directly verified in CONCERNS.md.

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Test Fixture Generation | Package leaks Moq into production (Pitfall 2) | Source generator pattern with consumer-provided Moq reference |
| Test Fixture Generation | Cannot see dependencies in referenced assemblies (Pitfall 4) | Use constructor parameter analysis on metadata symbols, not syntax |
| Test Fixture Generation | Uncompilable fixtures for edge cases (Pitfall 8) | Start with interface-only dependencies, diagnostic for unmockable types |
| Test Fixture Generation | Two generators loaded conflict (Pitfall 6) | Independent packages, no shared assembly references |
| typeof() Diagnostics | GetTypeInfo on wrong syntax node (Pitfall 1) | Navigate to TypeOfExpressionSyntax.Type before resolving |
| typeof() Diagnostics | Open generics have different syntax (Pitfall 5) | Detect OmittedTypeArgumentSyntax, use GetSymbolInfo |
| typeof() Diagnostics | Errors swallowed by existing catch blocks (Pitfall 9) | Tighten exception handling before adding validators |
| typeof() Diagnostics | MSBuild property naming inconsistency (Pitfall 12) | Convention-based naming with integration tests |
| Documentation Overhaul | Link breakage on migration (Pitfall 7) | Keep README as landing page, move deep content to /docs/ |
| Diagnostic UX | HelpLinkUri points to nonexistent pages (Pitfall 10) | Create doc pages before adding links |
| Code Quality | Silent exception swallowing (Pitfall 9) | Audit bare catch blocks first |
| All test phases | FluentAssertions v6 pattern lock-in (Pitfall 11) | Use HaveCount(1) in new test code |

## Sources

- [Roslyn Source Generator Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md) -- official patterns for generator packaging and cross-project analysis
- [Testing Roslyn Incremental Source Generators (Meziantou)](https://www.meziantou.net/testing-roslyn-incremental-source-generators.htm) -- testing patterns and common mistakes
- [Transitive Analyzers Flow Through by Default (NuGet/Home #13813)](https://github.com/NuGet/Home/issues/13813) -- transitive dependency issues with analyzer packages
- [Source Generator Transitive NuGet Dependencies (roslyn-sdk #592)](https://github.com/dotnet/roslyn-sdk/issues/592) -- dependency resolution in generator packages
- [Supporting Multiple SDK Versions in Source Generators (Andrew Lock)](https://andrewlock.net/supporting-multiple-sdk-versions-in-analyzers-and-source-generators/) -- multi-version packaging strategies
- [Moq SponsorLink Issue (devlooped/moq #1370)](https://github.com/devlooped/moq/issues/1370) -- community impact and version fragmentation
- [Roslyn SemanticModel.GetTypeInfo Issues (dotnet/roslyn #1477)](https://github.com/dotnet/roslyn/issues/1477) -- GetTypeInfo returning null for certain expression types
- [google/wire Docs Split Discussion (#84)](https://github.com/google/wire/issues/84) -- real-world docs migration tradeoffs
- IoCTools codebase: `ManualRegistrationValidator.cs` line 89, `CONCERNS.md`, `CONVENTIONS.md` -- direct code analysis

---

*Pitfalls audit: 2026-03-21*
