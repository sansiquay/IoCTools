# Phase 3: Test Fixture Generation - Context

**Gathered:** 2026-03-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Ship IoCTools.Testing as a new NuGet package that generates Moq-based test fixture code from the DI graph. Test authors write less boilerplate — Mock<T> fields, CreateSut() factory, and setup helpers are auto-generated. Package targets test projects only; production code remains unchanged.

</domain>

<decisions>
## Implementation Decisions

### Activation model
- **D-01:** New `[Cover<T>]` attribute on test classes — NOT on service classes
- **D-02:** Attribute lives in `IoCTools.Testing` package (test-only, no production leakage)
- **D-03:** Generic syntax: `[Cover<MyService>]` for type safety and IDE navigation
- **D-04:** Test class must be `partial` for generated fixture members
- **D-05:** Each test class gets independent fixtures (same service, multiple test classes = independent fixtures)

### Fixture structure
- **D-06:** Generated into the test class via partial class augmentation (not a separate base class)
- **D-07:** `protected readonly Mock<T>` fields with inline initialization
- **D-08:** `public TService CreateSut()` factory method that wires all mocks
- **D-09:** `protected void Setup{Dependency}(Action<Mock<T>> configure)` typed helpers
- **D-10:** No `abstract` — generated members are concrete implementation in partial class

### Dependency analysis approach
- **D-11:** Read the constructor that IoCTools.Generator already generated for the service
- **D-12:** No source sharing between IoCTools.Generator and IoCTools.Testing packages
- **D-13:** Generated constructor signature is the source of truth for dependencies
- **D-14:** For services with `[InjectConfiguration]`, generate `IOptions<T>` or `IConfiguration` mock helpers

### Inheritance and configuration handling
- **D-15:** For service hierarchies, read the full generated constructor (includes base parameters)
- **D-16:** `[InjectConfiguration]` services get appropriate mock setup helpers (`IOptions<T>`, `IConfiguration`)
- **D-17:** Configuration helpers support: `ConfigureOptions<T>(Action<T>)`, `ConfigureIConfiguration(Func<string, object>)`

### Analyzer diagnostics
- **D-18:** Place test fixture analyzers in `IoCTools.Generator` alongside existing diagnostics
- **D-19:** Detection scope: test projects (`.Tests` suffix, `Tests/` directories) + `Mock<T>` usage
- **D-20:** All fixture diagnostics at **Info** severity with MSBuild configuration option
- **D-21:** Diagnostics: TDIAG-01 (manual Mock field), TDIAG-02 (manual SUT construction), TDIAG-03 (could inherit fixture)

### Package configuration
- **D-22:** Moq 4.20.69 (latest stable)
- **D-23:** Target **net8.0 only** (matches existing test projects)
- **D-24:** Moq as direct dependency (not transitive)

### Claude's Discretion
- Exact diagnostic descriptor IDs and message text
- Internal structure of TestFixtureGenerator and TestFixturePipeline
- Whether to generate a separate `MockRepository` parameter in CreateSut() overloads
- Exact naming of configuration helper methods

</decisions>

<specifics>
## Specific Ideas

- Reading the generated constructor is simpler than re-analyzing attributes — the constructor signature tells us everything
- Partial class augmentation keeps fixture code visible next to test code in IDE
- `Cover<T>` is intentionally short — like `Moq<T>` in Moq.AutoMocker, memorable
- Info-level diagnostics keep suggestions helpful but not intrusive
- Test fixture generation is a separate pipeline, doesn't affect production service registration performance

</specifics>

<canonical_refs>
## Canonical References

### Existing generator architecture
- `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorGenerator.Rendering.cs` — Constructor generation patterns to mirror
- `IoCTools.Generator/IoCTools.Generator/Models/InheritanceHierarchyDependencies.cs` — Base vs derived dependency separation
- `IoCTools.Generator/IoCTools.Generator/Generator/ServiceClassPipeline.cs` — Pipeline pattern to replicate for test fixtures
- `IoCTools.Generator/IoCTools.Generator/DependencyInjectionGenerator.cs` — Main generator initialization point

### Diagnostic patterns
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/` — Descriptor patterns for TDIAG-01 through TDIAG-05
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Validators/MissedOpportunityValidator.cs` — Suggestion diagnostic model
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/DiagnosticsPipeline.cs` — How diagnostics attach to generator

### Sample service patterns
- `IoCTools.Sample/Services/InheritanceExamples.cs` — Service hierarchies for inheritance test coverage
- `IoCTools.Sample/Services/ConfigurationInjectionExamples.cs` — Configuration patterns to support
- `IoCTools.Sample/generated/` — Examples of generated constructors to read

### Test project patterns
- `IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj` — Reference TFM and package patterns
- `IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj` — Additional test project reference

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ConstructorGenerator` output: Generated constructors are the dependency source of truth
- `ServiceClassInfo` model: Shows how to carry symbol + syntax through pipelines
- `DiagnosticDescriptors` pattern: 8-argument constructor with HelpLinkUri, category, severity

### Established Patterns
- Source generator uses `IncrementalValuesProvider<T>` for pipeline data flow
- `context.SyntaxProvider.CreateSyntaxProvider()` for type discovery
- `context.RegisterSourceOutput()` for code emission
- `internalsVisibleTo` for test project access to generator internals

### Integration Points
- `IoCTools.Testing` is a new source generator package (analyzer, not runtime dependency)
- Needs `Microsoft.CodeAnalysis.CSharp` and `Moq` package references
- Generated fixture files live in test project, use `.g.cs` suffix convention
- Test fixture diagnostics run in `IoCTools.Generator` pipeline (has full compilation context)

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 03-test-fixture-generation*
*Context gathered: 2026-03-21*
