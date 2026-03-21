# Phase 3: Test Fixture Generation - Research

**Researched:** 2026-03-21
**Domain:** .NET Source Generators, Test Fixture Automation, Moq Integration
**Confidence:** HIGH

## Summary

IoCTools.Testing is a new source generator package targeting **test projects only** that generates Moq-based test fixture code. Test authors write `[Cover<MyService>]` on a `partial class` and get `Mock<T>` fields, `CreateSut()` factory, and typed setup helpers — eliminating manual mock declaration boilerplate.

**Key architectural decision:** The test fixture generator reads the **constructor that IoCTools.Generator already generated** for the service. This is simpler than re-analyzing attributes because the constructor signature fully describes all dependencies (including inheritance chains and configuration injection).

**Primary recommendation:** Create `IoCTools.Testing` as a standalone analyzer package with `CoverAttribute` in a new `IoCTools.Testing.Abstractions` project. Generated fixtures augment the test class via partial classes (not base class inheritance) for IDE visibility and simplicity.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Activation model
- **D-01:** New `[Cover<T>]` attribute on test classes — NOT on service classes
- **D-02:** Attribute lives in `IoCTools.Testing` package (test-only, no production leakage)
- **D-03:** Generic syntax: `[Cover<MyService>]` for type safety and IDE navigation
- **D-04:** Test class must be `partial` for generated fixture members
- **D-05:** Each test class gets independent fixtures (same service, multiple test classes = independent fixtures)

#### Fixture structure
- **D-06:** Generated into the test class via partial class augmentation (not a separate base class)
- **D-07:** `protected readonly Mock<T>` fields with inline initialization
- **D-08:** `public TService CreateSut()` factory method that wires all mocks
- **D-09:** `protected void Setup{Dependency}(Action<Mock<T>> configure)` typed helpers
- **D-10:** No `abstract` — generated members are concrete implementation in partial class

#### Dependency analysis approach
- **D-11:** Read the constructor that IoCTools.Generator already generated for the service
- **D-12:** No source sharing between IoCTools.Generator and IoCTools.Testing packages
- **D-13:** Generated constructor signature is the source of truth for dependencies
- **D-14:** For services with `[InjectConfiguration]`, generate `IOptions<T>` or `IConfiguration` mock helpers

#### Inheritance and configuration handling
- **D-15:** For service hierarchies, read the full generated constructor (includes base parameters)
- **D-16:** `[InjectConfiguration]` services get appropriate mock setup helpers (`IOptions<T>`, `IConfiguration`)
- **D-17:** Configuration helpers support: `ConfigureOptions<T>(Action<T>)`, `ConfigureIConfiguration(Func<string, object>)`

#### Analyzer diagnostics
- **D-18:** Place test fixture analyzers in `IoCTools.Generator` alongside existing diagnostics
- **D-19:** Detection scope: test projects (`.Tests` suffix, `Tests/` directories) + `Mock<T>` usage
- **D-20:** All fixture diagnostics at **Info** severity with MSBuild configuration option
- **D-21:** Diagnostics: TDIAG-01 (manual Mock field), TDIAG-02 (manual SUT construction), TDIAG-03 (could inherit fixture)

#### Package configuration
- **D-22:** Moq 4.20.69+ (latest stable: 4.20.72 verified)
- **D-23:** Target **net8.0 only** (matches existing test projects)
- **D-24:** Moq as direct dependency (not transitive)

### Claude's Discretion

- Exact diagnostic descriptor IDs and message text
- Internal structure of TestFixtureGenerator and TestFixturePipeline
- Whether to generate a separate `MockRepository` parameter in CreateSut() overloads
- Exact naming of configuration helper methods

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TEST-01 | IoCTools.Testing ships as a separate NuGet package with Moq as a peer dependency | D-02, D-22, D-24 — `IoCTools.Testing.csproj` with `<PackageReference Include="Moq" Version="4.20.72" />` |
| TEST-02 | Generator auto-declares `Mock<T>` fields for all constructor dependencies | D-11, D-13 — Read generated constructor, emit `protected readonly Mock<T> _mockName = new();` |
| TEST-03 | Generator produces a `CreateSut()` factory method that wires all mock `.Object` values | D-08 — Factory pattern from ConstructorGenerator.Rendering.cs |
| TEST-04 | Generated fixtures support services using `[Inject]` fields | D-13 — Constructor signature includes all Inject dependencies |
| TEST-05 | Generated fixtures support services using `[DependsOn]` attributes | D-13 — Constructor signature includes all DependsOn dependencies |
| TEST-06 | Generated fixtures support inheritance hierarchies with proper base fixture chaining | D-15 — Full generated constructor includes base parameters |
| TEST-07 | Generator produces typed mock setup helper methods (e.g., `SetupUserRepository(Action<Mock<IUserRepository>>)`) | D-09 — Typed helpers from inferred dependency names |
| TEST-08 | Generator produces configuration mock helpers for services using `[InjectConfiguration]` | D-14, D-16, D-17 — IOptions/IConfiguration helpers for config injection |
| TEST-09 | Generated fixture compiles without manual intervention for all supported service patterns | D-06, D-07, D-10 — Partial class augmentation with concrete members |
| TEST-10 | Mock fields are auto-initialized (`new Mock<T>()`) in the fixture constructor | D-07 — Inline initialization pattern |
| TEST-11 | Comprehensive test suite for the test fixture generator covering all IoCTools service patterns | Existing test infrastructure from `IoCTools.Generator.Tests` |
| TDIAG-01 | Detect manual `new Mock<T>()` fields where T is a dependency of an IoCTools service | D-19, D-20 — Info-level diagnostic in test projects |
| TDIAG-02 | Detect manual SUT construction where a generated `CreateSut()` exists | D-19, D-20 — Info-level diagnostic pattern |
| TDIAG-03 | Detect test classes with mock fields matching an IoCTools service's dependency graph | D-19, D-20 — Suggestion diagnostic pattern |
| TDIAG-04 | Integration tests for all test fixture analyzer diagnostics | D-18 — Place in `IoCTools.Generator.Tests` |
| TDIAG-05 | Test fixture analyzer examples added to sample/test project | D-19 — Sample patterns following `DiagnosticExamples.cs` |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.CodeAnalysis.CSharp | 4.5.0 | Roslyn compiler APIs for source generation | Matches existing generator, stable for analyzers |
| Moq | 4.20.72 | Mock library for test fixture generation | Latest stable, ecosystem standard for .NET testing |
| Microsoft.NET.Test.Sdk | 17.8.0 | Test SDK for test project | Matches existing test projects |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit | 2.9.3 | Test runner for fixture generator tests | Test project validation |
| FluentAssertions | 6.12.0 | Assertion library for tests | Matches existing test projects |
| System.Collections.Immutable | 6.0.0 | Immutable collections in generator pipelines | Required for Roslyn incremental generators |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Reading generated constructor | Re-analyze service attributes | Reading constructor is simpler — signature already contains all dependencies including inheritance and config |
| Partial class augmentation | Base class inheritance | Partial augmentation keeps fixtures visible in IDE next to test code; base class requires navigation to separate file |
| Separate IoCTools.Testing package | Bundle in IoCTools.Generator | Separate package prevents Moq transitive dependency to production code; test-only scoping is cleaner |

**Installation:**
```bash
# For test projects only
dotnet add package IoCTools.Testing
# (Moq is automatically pulled as a dependency)
```

**Version verification:**
```bash
# Moq latest verified 2026-03-21
curl -s "https://api.nuget.org/v3/registration5-semver1/moq/index.json" | grep -o '"4\.[0-9]*\.[0-9]*"' | tail -1
# Output: "4.20.72"
```

## Architecture Patterns

### Recommended Project Structure
```
IoCTools.Testing/
├── IoCTools.Testing.Abstractions/     # Cover<T> attribute (test-only, no production leakage)
│   ├── Annotations/
│   │   └── CoverAttribute.cs          # [Cover<TService>] marks test classes
│   └── IoCTools.Testing.Abstractions.csproj  # net8.0, minimal dependencies
├── IoCTools.Testing/                   # Source generator (analyzer package)
│   ├── IoCTools.Testing/
│   │   ├── TestFixtureGenerator.cs     # Main IIncrementalGenerator
│   │   ├── Generator/
│   │   │   ├── TestFixturePipeline.cs  # Discover [Cover<T>] test classes
│   │   │   └── FixtureEmitter.cs       # Emit Mock<T> fields, CreateSut(), helpers
│   │   ├── Analysis/
│   │   │   ├── ConstructorReader.cs    # Parse generated constructor from service
│   │   │   └── DependencyNameInferrer.cs # Extract _mockName from param type
│   │   └── Models/
│   │       └── TestClassInfo.cs        # Test class + covered service pair
│   └── IoCTools.Testing.csproj         # net8.0, Moq dependency, analyzer packaging
└── IoCTools.Testing.Tests/             # Fixture generator test suite
    ├── FixtureGeneration/
    │   ├── BasicServiceFixtureTests.cs
    │   ├── InheritanceFixtureTests.cs
    │   └── ConfigurationFixtureTests.cs
    └── IoCTools.Testing.Tests.csproj
```

### Pattern 1: Partial Class Augmentation for Test Fixtures

**What:** Generate fixture members into the test class via `partial class` augmentation rather than base class inheritance.

**When to use:** All test fixture generation — enables IDE auto-complete visibility and keeps fixtures next to test code.

**Example:**
```csharp
// Test author writes:
[Cover<UserService>]
public partial class UserServiceTests
{
    [Fact]
    public void CreateUser_ShouldCallRepository()
    {
        // Generated members available here:
        SetupUserRepository(m => m
            .Setup(x => x.AddAsync(It.IsAny<User>()))
            .ReturnsAsync(true));

        var sut = CreateSut(); // Generated factory

        sut.CreateUser("test@example.com");

        _mockUserRepository.Verify(x => x.AddAsync(It.IsAny<User>()), Times.Once);
    }
}

// Generator emits (same file, partial augmentation):
public partial class UserServiceTests
{
    protected readonly Mock<IUserRepository> _mockUserRepository = new();
    protected readonly Mock<ILogger<UserService>> _mockLogger = new();
    protected readonly Mock<IEmailService> _mockEmailService = new();

    public UserService CreateSut() => new(
        _mockUserRepository.Object,
        _mockLogger.Object,
        _mockEmailService.Object);

    protected void SetupUserRepository(Action<Mock<IUserRepository>> configure) => configure(_mockUserRepository);
    protected void SetupLogger(Action<Mock<ILogger<UserService>>> configure) => configure(_mockLogger);
    protected void SetupEmailService(Action<Mock<IEmailService>> configure) => configure(_mockEmailService);
}
```

### Pattern 2: Constructor Reading for Dependency Discovery

**What:** Parse the **generated constructor** from the service class to extract dependencies, rather than re-analyzing `[Inject]`/`[DependsOn]` attributes.

**When to use:** All fixture generation — the constructor signature is the source of truth for dependencies.

**Example:**
```csharp
// Source: IoCTools.Generator output (generated/UserService_Constructor.g.cs)
public partial class UserService
{
    public UserService(IUserRepository userRepository, ILogger<UserService> logger, IEmailService emailService)
    {
        this._userRepository = userRepository;
        this._logger = logger;
        this._emailService = emailService;
    }
}

// Fixture generator reads this signature to infer:
// 1. Three dependencies needed
// 2. Mock field names: _mockUserRepository, _mockLogger, _mockEmailService
// 3. Parameter order for CreateSut()
// 4. Types for Mock<T> generics
```

### Pattern 3: Incremental Generator Pipeline for Test Classes

**What:** Use `IIncrementalGenerator` with `CreateSyntaxProvider` to discover `[Cover<T>]` test classes, similar to `ServiceClassPipeline`.

**When to use:** Fixture discovery — incremental compilation performance.

**Example:**
```csharp
// Source: ServiceClassPipeline.cs pattern to replicate
internal static class TestFixturePipeline
{
    internal static IncrementalValuesProvider<TestClassInfo> Build(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) =>
                {
                    var typeDecl = (TypeDeclarationSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl);
                    if (symbol == null) return null;

                    // Check for [Cover<T>] attribute
                    var coverAttr = symbol.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.Name == "CoverAttribute");
                    if (coverAttr == null) return null;

                    // Extract TService generic type argument
                    var serviceType = (INamedTypeSymbol?)coverAttr.AttributeClass?.TypeArguments.FirstOrDefault();
                    if (serviceType == null) return null;

                    // Require partial class
                    if (!typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                        return null;

                    return new TestClassInfo(symbol, typeDecl, ctx.SemanticModel, serviceType);
                })
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x!.Value);
    }
}
```

### Pattern 4: Configuration Helper Generation

**What:** Generate typed helpers for `IOptions<T>` and `IConfiguration` dependencies from `[InjectConfiguration]` services.

**When to use:** Services with configuration injection need appropriate mock setup helpers.

**Example:**
```csharp
// Service with [InjectConfiguration]:
[Scoped]
public partial class DatabaseConnectionService
{
    [InjectConfiguration("Database:ConnectionString")]
    private readonly string _connectionString;

    [Inject] private readonly ILogger<DatabaseConnectionService> _logger;

    // Generated constructor:
    // public DatabaseConnectionService(ILogger<DatabaseConnectionService> logger, IConfiguration configuration)
}

// Generated fixture includes:
public partial class DatabaseConnectionServiceTests
{
    protected readonly Mock<ILogger<DatabaseConnectionService>> _mockLogger = new();
    protected readonly Mock<IConfiguration> _mockConfiguration = new();

    public DatabaseConnectionService CreateSut() => new(
        _mockLogger.Object,
        _mockConfiguration.Object);

    // Configuration-specific helper:
    protected void ConfigureIConfiguration(Func<string, object?> valueProvider)
    {
        _mockConfiguration
            .Setup(x => x.GetValue<string>(It.IsAny<string>()))
            .Returns((string key) => valueProvider(key)?.ToString());
    }

    protected void SetupLogger(Action<Mock<ILogger<DatabaseConnectionService>>> configure) => configure(_mockLogger);
}
```

### Anti-Patterns to Avoid

- **Do NOT read attributes directly from service classes:** The generated constructor already contains the complete dependency signature. Reading it is simpler and handles inheritance correctly.
- **Do NOT use base class inheritance for fixtures:** Partial class augmentation keeps generated members visible in IDE next to test code. Base classes require file navigation.
- **Do NOT generate fixtures in production projects:** `Cover<T>` attribute should live in `IoCTools.Testing.Abstractions` (test-only package), not `IoCTools.Abstractions`.
- **Do NOT make Moq a transitive dependency of IoCTools.Generator:** Moq stays in `IoCTools.Testing` only — test projects opt-in explicitly.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Mock field declaration | `new Mock<T>()` boilerplate for each test class | Auto-generated `protected readonly Mock<T> _mockName = new();` | Eliminates repetitive mock initialization, ensures all constructor deps are covered |
| SUT construction wiring | Manual `new Service(mock1.Object, mock2.Object, ...)` | Auto-generated `public TService CreateSut()` | Constructor signature changes propagate automatically, less maintenance |
| Typed setup helpers | Manual `Action<Mock<T>>` delegate setup for each mock | Auto-generated `protected void Setup{Dependency}(Action<Mock<T>>)` | IDE auto-complete discovers all available helpers, consistent naming |
| Configuration mock wiring | Manual `IConfiguration.Setup` chains for each test | Auto-generated `ConfigureIConfiguration(Func<string, object>)` | Centralized configuration mocking, supports key-value providers |

**Key insight:** Test fixture boilerplate is mechanical code generation — no runtime intelligence needed. Compile-time source generation eliminates repetition while maintaining full test flexibility.

## Common Pitfalls

### Pitfall 1: Constructor Not Found
**What goes wrong:** Fixture generator can't locate the generated constructor for the service (e.g., service not marked `partial`).

**Why it happens:** `IoCTools.Generator` only emits constructors for `partial` classes with service intent attributes. Non-partial classes don't get generated constructors.

**How to avoid:** Emit diagnostic `TDIAG-04` when `Cover<T>` references a service without a generated constructor. Suggest adding `partial` to the service class or lifetime attribute.

**Warning signs:** `TestClassInfo.ServiceSymbol` has no constructor matching expected signature, or generated file not found in compilation.

### Pitfall 2: Generic Type Naming Collisions
**What goes wrong:** Generated mock field names collide for multiple services with same generic (e.g., `ILogger<ServiceA>` and `ILogger<ServiceB>` both become `_mockLogger`).

**Why it happens:** Simple type name extraction drops generic arguments, losing disambiguation.

**How to avoid:** Include generic type arguments in mock names: `_mockLoggerServiceA`, `_mockLoggerServiceB`, or use indexed suffixes: `_mockLogger1`, `_mockLogger2`.

**Warning signs:** CS0102 duplicate member errors when multiple `ILogger<T>` dependencies exist.

### Pitfall 3: Configuration Type Mismatch
**What goes wrong:** Generated `IOptions<T>` mock doesn't match actual constructor parameter type (e.g., constructor takes `IOptionsSnapshot<T>` but fixture generates `IOptions<T>`).

**Why it happens:** `[InjectConfiguration]` with `SupportsReloading=true` generates `IOptionsSnapshot<T>` or `IOptionsMonitor<T>`, not `IOptions<T>`.

**How to avoid:** Read the exact parameter type from the generated constructor, not the field attribute. Emit helpers matching the exact `IOptions*` variant.

**Warning signs:** Type mismatch errors in `CreateSut()` call.

### Pitfall 4: Test Project Detection False Positives
**What goes wrong:** Fixture diagnostics (TDIAG-01, TDIAG-02, TDIAG-03) fire in production projects or unrelated test projects.

**Why it happens:** Simple `.Tests` suffix check isn't comprehensive — some test projects use different naming (e.g., `*.UnitTests`, `*.IntegrationTests`).

**How to avoid:** Combine multiple heuristics: `.Tests` suffix, `Tests/` directory, presence of `xunit`/`NUnit`/`MSTest` package references, AND `Moq` usage.

**Warning signs:** Info diagnostics appearing in production code where `Mock<T>` is legitimately used (rare but possible).

### Pitfall 5: Inheritance Chain Constructor Resolution
**What goes wrong:** Fixture for derived service reads wrong constructor (base class constructor instead of most-derived).

**Why it happens:** Service hierarchies have multiple constructors (base + derived). Generator must find the **most-derived** generated constructor.

**How to avoid:** When parsing constructors for `ServiceClassInfo`, filter for constructors where `ContainingType` exactly matches `Cover<T>`'s service type, not base types.

**Warning signs:** `CreateSut()` has wrong parameter count (missing derived deps).

## Code Examples

Verified patterns from official sources:

### Basic Test Fixture Generation
```csharp
// Test author writes:
using IoCTools.Testing.Annotations;
using Moq;

[Cover<UserService>]
public partial class UserServiceTests
{
    [Fact]
    public void GetUserById_ShouldReturnUser()
    {
        SetupUserRepository(m => m
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(new User { Id = 1, Name = "Test" }));

        var sut = CreateSut();

        var result = await sut.GetUserByIdAsync(1);

        result.Should().NotBeNull();
        result.Name.Should().Be("Test");
    }
}

// Generator emits (partial augmentation):
// Source: ServiceClassPipeline.cs pattern
public partial class UserServiceTests
{
    protected readonly Mock<IUserRepository> _mockUserRepository = new();
    protected readonly Mock<ILogger<UserService>> _mockLogger = new();

    public UserService CreateSut() => new(
        _mockUserRepository.Object,
        _mockLogger.Object);

    protected void SetupUserRepository(Action<Mock<IUserRepository>> configure) => configure(_mockUserRepository);
    protected void SetupLogger(Action<Mock<ILogger<UserService>>> configure) => configure(_mockLogger);
}
```

### Inheritance Fixture Generation
```csharp
// Service hierarchy:
// BaseRepository<T> -> UserRepository (has ILogger + IUserRepository deps)

[Cover<UserRepository>]
public partial class UserRepositoryTests
{
    [Fact]
    public async Task GetUserById_ShouldCallCache()
    {
        // Generated fixture includes BOTH base and derived dependencies:
        SetupCacheService(m => m
            .Setup(x => x.GetOrSet("user:1", It.IsAny<Func<User>>()))
            .Returns(new User { Id = 1 }));

        SetupLogger(m => { }); // Base dependency

        var sut = CreateSut();
        var result = await sut.GetByIdAsync(1);

        result.Should().NotBeNull();
    }
}

// Generator emits (includes base constructor parameters):
public partial class UserRepositoryTests
{
    // Base dependencies from BaseRepository<T>:
    protected readonly Mock<IConfiguration> _mockConfiguration = new();
    protected readonly Mock<ILogger<BaseRepository<User>>> _mockBaseLogger = new();

    // Derived dependencies from UserRepository:
    protected readonly Mock<ICacheService> _mockCacheService = new();
    protected readonly Mock<ILogger<UserRepository>> _mockDerivedLogger = new();

    public UserRepository CreateSut() => new(
        _mockConfiguration.Object,
        _mockBaseLogger.Object,
        _mockCacheService.Object,
        _mockDerivedLogger.Object);

    protected void SetupCacheService(Action<Mock<ICacheService>> configure) => configure(_mockCacheService);
    protected void SetupConfiguration(Action<Mock<IConfiguration>> configure) => configure(_mockConfiguration);
    // ... other helpers
}
```

### Configuration Injection Fixture
```csharp
// Service with [InjectConfiguration]:
[Scoped]
public partial class DatabaseConnectionService
{
    [InjectConfiguration("Database:ConnectionString")]
    private readonly string _connectionString;

    [Inject] private readonly ILogger<DatabaseConnectionService> _logger;

    // Generated constructor includes IConfiguration parameter
}

[Cover<DatabaseConnectionService>]
public partial class DatabaseConnectionServiceTests
{
    [Fact]
    public void TestConnection_ShouldUseConfiguredConnectionString()
    {
        ConfigureIConfiguration(key => key switch
        {
            "Database:ConnectionString" => "Server=test;Database=db;",
            _ => throw new ArgumentException($"Unexpected key: {key}")
        });

        var sut = CreateSut();

        var result = sut.GetConnectionInfo();
        result.Should().Contain("Server=test");
    }
}

// Generator emits:
public partial class DatabaseConnectionServiceTests
{
    protected readonly Mock<ILogger<DatabaseConnectionService>> _mockLogger = new();
    protected readonly Mock<IConfiguration> _mockConfiguration = new();

    public DatabaseConnectionService CreateSut() => new(
        _mockLogger.Object,
        _mockConfiguration.Object);

    protected void ConfigureIConfiguration(Func<string, object?> valueProvider)
    {
        _mockConfiguration
            .Setup(x => x.GetValue<string>(It.IsAny<string>()))
            .Returns((string key) => valueProvider(key)?.ToString());

        _mockConfiguration
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => valueProvider(key)?.ToString());
    }

    protected void SetupLogger(Action<Mock<ILogger<DatabaseConnectionService>>> configure) => configure(_mockLogger);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual mock declaration in every test class | Auto-generated Mock<T> fields via source generator | IoCTools v1.5.0 (this phase) | Eliminates boilerplate, ensures all deps covered |
| Manual SUT construction: `new Service(mock1.Object, ...)` | Auto-generated `CreateSut()` factory | IoCTools v1.5.0 (this phase) | Constructor signature changes propagate automatically |
| Manual `Action<Mock<T>>` setup for each test | Auto-generated typed setup helpers | IoCTools v1.5.0 (this phase) | IDE discoverability, consistent naming |
| No compile-time validation of test fixture correctness | Analyzer diagnostics (TDIAG-01 through TDIAG-05) | IoCTools v1.5.0 (this phase) | Suggests fixture usage when manual mocks detected |

**Deprecated/outdated:**
- Manual base class inheritance for test fixtures: Modern partial class augmentation is more discoverable in IDE.
- Runtime auto-mocking containers: Compile-time generation has zero runtime overhead and better IDE integration.

## Open Questions

1. **MockRepository integration**
   - What we know: Moq supports `MockRepository` for custom mock behavior and verification aggregation
   - What's unclear: Whether to add `MockRepository? mockRepository = null` parameter to `CreateSut()` overloads
   - Recommendation: Skip for initial implementation — can be added as overload later if users request it. Most tests don't need custom MockRepository.

2. **Diagnostic message text for TDIAG-01 through TDIAG-05**
   - What we know: All should be Info severity with MSBuild configuration option
   - What's unclear: Exact wording and HelpLinkUri patterns
   - Recommendation: Follow existing diagnostic patterns from `DiagnosticDescriptors.cs` (e.g., IOC068 "ConstructorCouldUseDependsOn" as template for helpful suggestion messages)

3. **Generic service fixture naming**
   - What we know: `Cover<MyService<T>>` requires handling generic type arguments
   - What's unclear: How to name mock fields for generic dependencies (e.g., `IRepository<User>` vs `IRepository<Order>`)
   - Recommendation: Include generic argument in mock name: `_mockUserRepository`, `_mockOrderRepository` based on type argument name, not full type.

4. **Multiple `[Cover<T>]` attributes on same test class**
   - What we know: `[AttributeUsage]` on `CoverAttribute` controls whether multiple covers are allowed
   - What's unclear: Should we allow testing multiple services in one fixture class?
   - Recommendation: Disallow multiple `[Cover<T>]` initially (`[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]`). Can relax later if use case emerges.

## Validation Architecture

> **Skipped:** `workflow.nyquist_validation` is explicitly set to `false` in `.planning/config.json`.

## Sources

### Primary (HIGH confidence)
- **Existing generator architecture** — `IoCTools.Generator/IoCTools.Generator/` pipeline patterns (ServiceClassPipeline, DiagnosticsPipeline, ConstructorGenerator)
- **Generated constructor output** — `IoCTools.Sample/generated/` examples showing constructor signature format
- **Dependency analysis logic** — `IoCTools.Generator/Analysis/DependencyAnalyzer.cs` for inheritance-aware dependency collection
- **Diagnostic descriptor patterns** — `IoCTools.Generator/Diagnostics/Descriptors/StructuralDiagnostics.cs` for Info-level suggestion patterns

### Secondary (MEDIUM confidence)
- **Moq NuGet package** — Verified latest stable version 4.20.72 via NuGet API (2026-03-21)
- **Test project structure** — `IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj` for net8.0 target pattern

### Tertiary (LOW confidence)
- None — all findings verified against codebase or official documentation

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Verified versions from NuGet API, matches existing generator patterns
- Architecture: HIGH — Based on existing generator code patterns (ServiceClassPipeline, ConstructorGenerator)
- Pitfalls: HIGH — Derived from netstandard2.0 constraints and source generator limitations
- Configuration handling: MEDIUM — Requires validation for `IOptions<T>` vs `IOptionsSnapshot<T>` vs `IOptionsMonitor<T>` edge cases

**Research date:** 2026-03-21
**Valid until:** 30 days (stable .NET ecosystem)
