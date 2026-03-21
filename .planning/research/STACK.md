# Technology Stack

**Project:** IoCTools v1.4.0 Milestone
**Researched:** 2026-03-21

## Recommended Stack

### IoCTools.Testing Package

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| Moq | 4.20.72 | Mock generation target | Project requirement; 138M+ NuGet downloads; dominant .NET mocking framework; targets netstandard2.0 so compatible everywhere | HIGH |
| Moq.AutoMock | 4.0.1 | Reference implementation / prior art only | Do NOT take a dependency -- see rationale below | HIGH |
| xUnit | 2.9.3 | Test runner alignment | Match existing generator test project; IoCTools.Testing should be framework-agnostic but tests use xUnit | HIGH |
| .NET 8.0 (TFM) | net8.0 | Package target | Test projects don't need netstandard2.0 breadth; net8.0 is current LTS; net9.0 acceptable alternative | MEDIUM |

### IoCTools.Testing -- Generator Architecture

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| Microsoft.CodeAnalysis.CSharp | 4.5.0 | Roslyn APIs | Match existing generator pinned version exactly -- mixing Roslyn versions across analyzers causes load failures | HIGH |
| IoCTools.Abstractions | 1.3.0+ | Attribute detection | Generator needs to recognize [Inject], [DependsOn], lifetime attributes to know what mocks to generate | HIGH |

### typeof() Diagnostics (IOC090-094)

No new dependencies. Extends existing `ManualRegistrationValidator` in `IoCTools.Generator` using the same Roslyn 4.5.0 APIs already referenced.

### Documentation Tooling

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| DocFX | 2.78.5 | Multi-page documentation site | Official .NET Foundation project; generates API docs from triple-slash comments + markdown content pages; used by Microsoft's own libraries | HIGH |

### CLI Improvements

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| System.Text.Json | (built-in) | --json output mode | Already available in net8.0 runtime; no new dependency needed | HIGH |
| Spectre.Console | (evaluate) | Color-coded terminal output | Current CLI uses raw Console; Spectre.Console is heavyweight for just ANSI colors -- use raw ANSI escape codes instead | MEDIUM |

## Key Decision: Do NOT Depend on Moq.AutoMock

**Recommendation:** IoCTools.Testing should NOT wrap or depend on Moq.AutoMock. Generate standalone code instead.

**Rationale:**

1. **Moq.AutoMock operates at runtime via reflection.** It inspects constructors at test execution time to auto-create mocks. IoCTools.Testing operates at compile time via source generation. These are fundamentally different approaches that don't compose well.

2. **Moq.AutoMock's source generators solve different problems.** Their generators create constructor null-argument tests (`[ConstructorTests]`), options configuration helpers, and logging helpers. They do NOT generate typed `Mock<T>` field declarations or SUT factory methods -- which is what IoCTools.Testing needs to generate.

3. **IoCTools already knows the full dependency graph at compile time.** The generator has complete knowledge of every `[Inject]` field, `[DependsOn]` dependency, configuration binding, and inheritance chain. Moq.AutoMock discovers this at runtime. Generating explicit, typed code from compile-time knowledge produces better IDE support (IntelliSense on mock fields), better error messages, and zero runtime overhead.

4. **Moq.AutoMock v4.0.1 is recent (March 2026) but adds transitive complexity.** It pulls in `NonBlocking` and has its own source generators that can conflict with IoCTools generators in the same project (documented ambiguous method call issues).

**What to learn from Moq.AutoMock:**
- The `[ConstructorTests]` generator pattern is good prior art for attribute-triggered source generation in test projects
- Their approach of generating partial classes with internal visibility is a proven pattern
- Their per-generator MSBuild disable properties (`EnableMoqAutoMockerXxxGenerator`) are a good UX pattern to adopt

## Key Decision: Moq-Only, No Abstraction Layer

**Recommendation:** Generate code that directly references `Moq.Mock<T>`, `mock.Setup()`, `mock.Object`, etc. Do not abstract over the mocking framework.

**Rationale:**

1. **Moq holds 70%+ market share** among .NET mocking frameworks. The PROJECT.md already scopes this to Moq-only for v1.

2. **NSubstitute is gaining mindshare** (25% and growing), but supporting it would require a completely different code generation strategy (NSubstitute uses `Substitute.For<T>()` with no `.Object` accessor, different setup/verify syntax). This is a v2 concern.

3. **Abstracting over mocking frameworks adds complexity with no user benefit.** Users who use NSubstitute won't install IoCTools.Testing; users who use Moq want direct Moq APIs they already know.

4. **Generated code should look like code the developer would write by hand.** That means `Mock<IMyService> _myServiceMock` fields, `_myServiceMock.Setup(...)` calls, and `_myServiceMock.Object` in constructors.

## Key Decision: DocFX for Documentation

**Recommendation:** Use DocFX 2.78.5 when/if the project outgrows single-README.

**Rationale:**

1. **DocFX is the .NET ecosystem standard.** It's a .NET Foundation project, used by Microsoft's own libraries, and generates both API reference docs (from XML comments) and conceptual pages (from markdown).

2. **API reference generation is the killer feature.** IoCTools has rich XML comments on its attributes. DocFX can auto-generate browsable API docs from these, which is exactly what users need for a library with 86+ diagnostics and numerous attributes.

3. **GitHub Pages deployment is free.** DocFX outputs static HTML that deploys directly to GitHub Pages via a simple CI action.

4. **Alternatives considered:**
   - **mdBook** (Rust-based): Fast and clean, but no .NET API doc generation. Would require hand-maintaining attribute reference pages. Reject.
   - **MkDocs / Material for MkDocs**: Python-based, popular for general docs. No .NET API integration. Reject for same reason as mdBook.
   - **Plain markdown in /docs**: Viable for the near-term. Evaluate after v1.4.0 whether content volume warrants DocFX migration. Acceptable interim approach.

5. **Timing recommendation:** Do NOT set up DocFX as part of this milestone unless the documentation overhaul reveals content exceeding ~500 lines. Start with structured markdown in `/docs/` and migrate to DocFX when API reference docs become necessary. DocFX setup is a half-day task, not a blocker.

## IoCTools.Testing -- Generated Code Patterns

The generated test fixture code should follow these Moq patterns (based on real-world .NET test conventions and the Delta project patterns noted in PROJECT.md):

### Pattern: Mock Field Declarations

```csharp
// Generated partial class
public partial class MyServiceFixture
{
    protected Mock<ILogger<MyService>> LoggerMock { get; } = new Mock<ILogger<MyService>>();
    protected Mock<IUserRepository> UserRepositoryMock { get; } = new Mock<IUserRepository>();
    protected Mock<IEmailService> EmailServiceMock { get; } = new Mock<IEmailService>();
}
```

**Why properties not fields:** Properties with `{ get; }` are the modern C# convention. Auto-initialized `= new Mock<T>()` ensures each test gets fresh mocks without explicit setup.

### Pattern: SUT Factory Method

```csharp
// Generated in same partial class
protected MyService CreateSut()
{
    return new MyService(
        LoggerMock.Object,
        UserRepositoryMock.Object,
        EmailServiceMock.Object);
}
```

**Why a method not a property:** Factory method signals "creates new instance each call." Property would imply caching, which is wrong for SUT creation in tests.

### Pattern: Configuration Mock Integration

For services using `[InjectConfiguration("Section")]`:

```csharp
protected IOptions<DatabaseSettings> DatabaseSettingsOptions { get; set; }
    = Options.Create(new DatabaseSettings());

protected MyService CreateSut()
{
    return new MyService(
        LoggerMock.Object,
        DatabaseSettingsOptions);
}
```

**Why `IOptions<T>` not `Mock<IOptions<T>>`:** Mocking `IOptions<T>` is an anti-pattern. `Options.Create()` is the standard approach in .NET testing.

### Pattern: Inheritance Support

For services with inheritance chains, generate fixtures that mirror the hierarchy:

```csharp
public partial class DerivedServiceFixture : BaseServiceFixture
{
    protected Mock<IDerivedDependency> DerivedDependencyMock { get; } = new Mock<IDerivedDependency>();

    protected new DerivedService CreateSut()
    {
        return new DerivedService(
            BaseServiceMock.Object,  // from base fixture
            DerivedDependencyMock.Object);
    }
}
```

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Mocking framework | Moq 4.20.72 | NSubstitute 5.x | v2 consideration; different API surface requires different generator; Moq is project requirement |
| Auto-mocking | Custom source gen | Moq.AutoMock 4.0.1 | Runtime reflection vs compile-time generation; different problem space; transitive dependency risk |
| Compile-time mocking | Custom source gen | MockSourceGenerator 0.3.0 | Abandoned (v0.3.0, no updates in 2+ years, 2 GitHub stars); generates mock implementations, not test fixtures |
| Compile-time mocking | Custom source gen | MockGen | Abandoned experimental project; couldn't handle generic methods |
| Doc tooling | DocFX 2.78.5 | mdBook | No .NET API doc generation |
| Doc tooling | DocFX 2.78.5 | MkDocs Material | No .NET API doc generation |
| Doc interim | Structured /docs/ markdown | DocFX immediately | Over-engineering for current content volume; evaluate after doc overhaul |
| CLI colors | Raw ANSI escapes | Spectre.Console | Heavyweight dependency for simple color output; CLI is a dotnet tool, keep it lean |
| JSON output | System.Text.Json (built-in) | Newtonsoft.Json | Already available in net8.0; no reason to add external dependency |

## IoCTools.Testing Package Structure

```
IoCTools.Testing/
  IoCTools.Testing.csproj          # net8.0, references Moq 4.20.72
  TestFixtureGenerator.cs          # IIncrementalGenerator entry point
  Analysis/
    ServiceDependencyCollector.cs   # Extracts deps from IoCTools-annotated classes
  CodeGeneration/
    FixtureClassGenerator.cs        # Generates partial fixture classes
    SutFactoryGenerator.cs          # Generates CreateSut() methods
    MockFieldGenerator.cs           # Generates Mock<T> property declarations
  Attributes/
    GenerateFixtureAttribute.cs     # [GenerateFixture] marker attribute
  build/
    IoCTools.Testing.targets        # MSBuild integration (if needed)
```

**Key architectural note:** The generator itself targets netstandard2.0 (Roslyn constraint), but the PACKAGE targets net8.0+ because test projects don't need broad framework support. The generator assembly inside the package is still netstandard2.0.

**Correction:** Actually, the generator DLL must be netstandard2.0 (Roslyn loads it). The attributes assembly and any runtime helpers can target net8.0. This mirrors the existing IoCTools pattern: `IoCTools.Abstractions` (netstandard2.0) + `IoCTools.Generator` (netstandard2.0). For IoCTools.Testing, the generator is netstandard2.0 but the package metadata can specify net8.0 as the minimum consumer TFM.

## Installation

```bash
# For production code (existing)
dotnet add package IoCTools.Abstractions
dotnet add package IoCTools.Generator

# For test projects (new)
dotnet add package IoCTools.Testing
dotnet add package Moq  # Peer dependency, version >= 4.20.72

# Documentation (when ready)
dotnet tool install docfx --version 2.78.5
```

## Version Compatibility Matrix

| Package | Target | Min Consumer | Dependencies |
|---------|--------|-------------|-------------|
| IoCTools.Abstractions | netstandard2.0 | .NET Framework 4.6.1+ | None |
| IoCTools.Generator | netstandard2.0 | .NET Framework 4.6.1+ | Roslyn 4.5.0 (analyzer) |
| IoCTools.Testing | netstandard2.0 (generator) | net8.0+ (test projects) | Moq >= 4.20.72 (peer) |
| IoCTools.Tools.Cli | net8.0 | net8.0+ | Roslyn Workspaces 4.5.0 |

## Sources

- [Moq 4.20.72 on NuGet](https://www.nuget.org/packages/moq/) - Published Sept 2024, 138M+ downloads - HIGH confidence
- [Moq.AutoMock 4.0.1 on NuGet](https://www.nuget.org/packages/Moq.AutoMock/4.0.1) - Published March 2026 - HIGH confidence
- [Moq.AutoMock Source Generators docs](https://github.com/moq/Moq.AutoMocker/blob/master/docs/SourceGenerators.md) - Five generators documented - HIGH confidence
- [Moq.AutoMock GitHub](https://github.com/moq/Moq.AutoMocker) - Prior art for test fixture source generation - HIGH confidence
- [DocFX 2.78.5 release](https://github.com/dotnet/docfx/releases) - Feb 2025, .NET 10 support - HIGH confidence
- [DocFX official docs](https://dotnet.github.io/docfx/) - .NET Foundation project - HIGH confidence
- [MockSourceGenerator on NuGet](https://www.nuget.org/packages/MockSourceGenerator/) - v0.3.0, effectively abandoned - HIGH confidence
- [MockGen on GitHub](https://github.com/thomas-girotto/MockGen) - Experimental, abandoned - MEDIUM confidence
- [NSubstitute vs Moq comparison](https://blog.dotnetconsult.tech/2025/12/moq-vs-nsubstitute-choosing-right.html) - Market share data - MEDIUM confidence
- [Slant .NET doc tools comparison](https://www.slant.co/topics/4111/~documentation-tools-for-net-developers) - Community rankings - LOW confidence

---

*Stack research: 2026-03-21*
