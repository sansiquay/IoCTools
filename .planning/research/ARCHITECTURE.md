# Architecture Patterns

**Domain:** .NET Source Generator Test Fixture Package + Diagnostic Extension
**Researched:** 2026-03-21

## Recommended Architecture

### Decision: IoCTools.Testing as a Separate Source Generator

IoCTools.Testing should be a **separate `IIncrementalGenerator`** in its own NuGet package, not an extension of the existing `DependencyInjectionGenerator`. Three reasons:

1. **Dependency isolation.** The test fixture generator needs to emit code referencing Moq (`Mock<T>`, `Setup()`). If this lived inside `IoCTools.Generator`, every consumer project -- including production code -- would need Moq available during compilation. A separate package means only test projects reference it.

2. **Generator loading model.** Roslyn loads generators as analyzers. Each generator assembly is loaded independently. The existing generator targets `netstandard2.0` and ships in `analyzers/dotnet/cs/`. A test generator ships in the same slot of a different NuGet package. Two generators can run independently on the same compilation without conflict.

3. **Opt-in activation.** Users add `IoCTools.Testing` only to their test projects. The production generator never sees Moq types, test fixture attributes, or test-specific code. Clean separation.

### Code Sharing Strategy: Source-Include via Shared Project

The test fixture generator needs the same dependency analysis logic the main generator uses (`DependencyAnalyzer.GetConstructorDependencies`, `ServiceDiscovery`, `AttributeTypeChecker`, inheritance traversal). Two viable approaches exist:

**Recommended: Shared project (.shproj) or compile-include.**

Create `IoCTools.Generator.Shared/` containing the analysis and model code that both generators need. Both `IoCTools.Generator.csproj` and `IoCTools.Testing.csproj` include these files at compile time:

```xml
<!-- In IoCTools.Testing.csproj -->
<ItemGroup>
  <Compile Include="..\IoCTools.Generator\IoCTools.Generator\Analysis\*.cs"
           LinkBase="Shared\Analysis" />
  <Compile Include="..\IoCTools.Generator\IoCTools.Generator\Models\*.cs"
           LinkBase="Shared\Models" />
  <Compile Include="..\IoCTools.Generator\IoCTools.Generator\Utilities\*.cs"
           LinkBase="Shared\Utilities" />
  <Compile Include="..\IoCTools.Generator\IoCTools.Generator\Generator\ServiceDiscovery.cs"
           LinkBase="Shared\Generator" />
</ItemGroup>
```

This avoids the complexity of a separate shared DLL (which would need to be packed alongside both analyzer assemblies), while giving the test generator full access to `DependencyAnalyzer`, `AttributeTypeChecker`, `ServiceClassInfo`, and all model types.

**Why not a shared DLL:** Source generators cannot reference arbitrary DLLs at runtime -- all dependencies must be packed into the `analyzers/` folder of the NuGet package. A shared DLL approach requires `GeneratePathProperty=true`, custom `GetDependencyTargetPaths` targets, and careful packaging to include the shared DLL in both packages' `analyzers/` directories. This works but adds fragile packaging complexity that compile-include avoids entirely.

**Why not `InternalsVisibleTo` from Generator to Testing:** The Testing package would need to *reference* the Generator assembly, but generators are loaded as analyzers, not as regular project references. Cross-generator assembly references during Roslyn analysis are unreliable and unsupported.

### Component Boundaries

| Component | Responsibility | Communicates With |
|-----------|---------------|-------------------|
| `IoCTools.Abstractions` | Public attributes consumed by user code | All consumer projects (unchanged) |
| `IoCTools.Generator` | Production source generator: registration, constructors, diagnostics (IOC001-094) | Consumer projects at build time (unchanged) |
| `IoCTools.Testing` | Test fixture source generator: Mock fields, SUT factory, setup helpers | Test projects at build time; reads same attributes as Generator |
| `IoCTools.Testing.Abstractions` | Optional: `[GenerateFixture]` attribute to mark test classes | Test projects (if opt-in marker approach chosen) |
| `IoCTools.Generator.Tests` | Tests for production generator (unchanged) | Generator internals |
| `IoCTools.Testing.Tests` | Tests for test fixture generator | Testing generator internals |

### Data Flow

```
User's Production Project                    User's Test Project
========================                     ====================

[Scoped]                                     public partial class MyServiceTests
[DependsOn<IFoo, IBar>]                      {
public partial class MyService               }
  : IMyService { }                                    |
        |                                             |
        v                                             v
  IoCTools.Generator                           IoCTools.Testing
  (IIncrementalGenerator)                      (IIncrementalGenerator)
        |                                             |
  Reads: [Scoped], [DependsOn],               Reads: Same attributes on the
  [Inject], inheritance chain                  *referenced production types*
        |                                             |
        v                                             v
  Emits:                                       Emits:
  - MyService_Constructor.g.cs                 - MyServiceTests_Fixture.g.cs
  - ServiceRegistrations_{Asm}.g.cs              containing:
  - Diagnostics                                  - Mock<IFoo> _mockFoo
                                                 - Mock<IBar> _mockBar
                                                 - CreateSut() method
                                                 - SetupFoo/SetupBar helpers
```

**Key insight:** The test generator does NOT need to run on the production project. It runs on the *test project*, which has a project reference to the production project. The test project's `Compilation` contains the production types as referenced symbols. The test generator uses the same `DependencyAnalyzer` logic to inspect those referenced type symbols and extract their dependency graphs.

### IoCTools.Testing Generator Pipeline

```
1. SyntaxProvider.CreateSyntaxProvider
   - Predicate: Find partial classes in test project (optionally marked with [GenerateFixture])
   - Transform: Extract the "service under test" type from naming convention or attribute

2. Dependency Resolution
   - Look up the SUT type in the compilation's referenced assemblies
   - Call shared DependencyAnalyzer.GetConstructorDependencies() on the SUT symbol
   - Collect: all constructor parameter types, their names, their sources (Inject/DependsOn/Config)

3. Fixture Code Generation
   - Emit partial class with Mock<T> fields for each dependency
   - Emit CreateSut() that constructs SUT with mock.Object parameters
   - Emit typed Setup*() helper methods
   - Emit test initialization (constructor or [SetUp]) that creates fresh mocks
```

### Activation Strategy: How the Test Generator Identifies What to Generate

Two options, recommend **Option A** for v1:

**Option A: Naming convention + partial class.**
Any partial class named `{ServiceName}Tests` in a project referencing `IoCTools.Testing` automatically gets fixture generation for `ServiceName`. Simple, zero-attribute, discoverable.

**Option B: Explicit attribute `[GenerateFixture(typeof(MyService))]`.**
More explicit, works for non-standard naming. Requires a small `IoCTools.Testing.Abstractions` package. Better for complex cases (testing the same service from multiple test classes).

Recommend starting with Option A and adding Option B when users need it. The generator can support both simultaneously -- attribute takes priority, then falls back to naming convention.

---

## typeof() Diagnostics Architecture (IOC090-094)

### Integration Point: ManualRegistrationValidator Extension

The typeof() diagnostics extend the existing `ManualRegistrationValidator.ValidateAllTrees()` method. The current validator already walks `InvocationExpressionSyntax` nodes and checks for `AddScoped`/`AddSingleton`/`AddTransient` calls. It currently only handles the **generic overload** (`AddScoped<IFoo, Foo>()`). The typeof() diagnostics add handling for the **non-generic overload** (`AddScoped(typeof(IFoo), typeof(Foo))`).

### Roslyn Pattern for Parsing typeof() Arguments

The non-generic DI registration pattern in user code looks like:

```csharp
services.AddScoped(typeof(IMyService), typeof(MyServiceImpl));
// or for open generics:
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
```

To extract the type from a `typeof()` expression in Roslyn:

```csharp
// Given an ArgumentSyntax from the invocation
if (argument.Expression is TypeOfExpressionSyntax typeOfExpr)
{
    // typeOfExpr.Type is the TypeSyntax inside typeof()
    var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
    var typeSymbol = typeInfo.Type as INamedTypeSymbol;

    // For open generics like typeof(IRepository<>),
    // typeSymbol will be an unbound generic (OriginalDefinition)
    if (typeSymbol != null && typeSymbol.IsUnboundGenericType)
    {
        // Handle open generic registration
    }
}
```

**Key details:**
- `TypeOfExpressionSyntax.Type` gives the `TypeSyntax` node (the thing inside the parentheses)
- `SemanticModel.GetTypeInfo(typeSyntax).Type` resolves to the `ITypeSymbol`
- For open generics (`typeof(IRepository<>)`), the resolved symbol has `IsUnboundGenericType == true`
- `SemanticModel.GetSymbolInfo(typeSyntax).Symbol` can also be used but `GetTypeInfo` is more reliable for type-of expressions

Confidence: HIGH -- this is standard Roslyn API usage confirmed by Microsoft documentation and the existing codebase patterns.

### Validator Structure

Extend the existing `ManualRegistrationValidator` rather than creating a new validator. The current method already iterates all `InvocationExpressionSyntax` nodes and resolves method symbols. The extension adds a new branch:

```
Current flow:
  invocation → resolve method symbol → check if AddScoped/etc
    → typeArgsSymbol.Length > 0 → extract from generic type args
    → typeArgsSymbol.Length == 0 → continue (SKIPS non-generic calls)

Extended flow:
  invocation → resolve method symbol → check if AddScoped/etc
    → typeArgsSymbol.Length > 0 → extract from generic type args (existing IOC081-086)
    → typeArgsSymbol.Length == 0 → check argument list for typeof() expressions
      → argument[0] is TypeOfExpressionSyntax → extract service type
      → argument[1] is TypeOfExpressionSyntax → extract implementation type
      → Apply IOC090-094 logic
```

The `typeArgsSymbol.Length == 0` branch currently exits with `continue`. That is the insertion point. Instead of continuing, check `invocation.ArgumentList.Arguments` for `TypeOfExpressionSyntax` nodes.

### IOC090-094 Diagnostic Flow

| Diagnostic | Condition | Message Pattern |
|------------|-----------|-----------------|
| IOC090 | `typeof(IFoo), typeof(Foo)` where `Foo` has no IoCTools attributes | "typeof() registration of '{0}' as '{1}' could use [{2}] attribute" |
| IOC091 | `typeof(IFoo), typeof(Foo)` where `Foo` is already registered by IoCTools with same lifetime | "typeof() registration duplicates IoCTools registration of '{0}'" |
| IOC092 | `typeof(IFoo), typeof(Foo)` where `Foo` is registered by IoCTools with different lifetime | "typeof() registration uses {0} but IoCTools registers '{1}' as {2}" |
| IOC094 | `typeof(IRepository<>), typeof(Repository<>)` open generic | "Open generic typeof() registration could use IoCTools attributes on '{0}'" |

### Open Generic Detection

Open generics (`typeof(IRepository<>)`) need special handling because:

1. The resolved `INamedTypeSymbol` has `IsUnboundGenericType == true`
2. To check if the implementation type has IoCTools attributes, use `typeSymbol.OriginalDefinition` to get the generic type definition, then check its attributes
3. IoCTools currently does not support open generic registration natively (this is a known limitation), so IOC094 should be an **Info**-level diagnostic suggesting the user could add lifetime attributes even though IoCTools cannot auto-register open generics yet

## Build Order and Dependencies

### Package Dependency Graph

```
IoCTools.Abstractions (netstandard2.0)
    ^                    ^
    |                    |
IoCTools.Generator       IoCTools.Testing
(netstandard2.0)         (netstandard2.0)
    ^                    [compile-includes shared Analysis/Models/Utilities from Generator]
    |
IoCTools.Tools.Cli       IoCTools.Testing.Tests
(net8.0)                 (net8.0/net9.0)
    ^
    |
IoCTools.Tools.Cli.Tests
(net8.0/net9.0)
```

### Build Order

1. **IoCTools.Abstractions** -- no dependencies, builds first
2. **IoCTools.Generator** -- depends on Abstractions (compile-time only via attribute name matching, not project reference)
3. **IoCTools.Testing** -- depends on Abstractions; compile-includes shared source from Generator project
4. **IoCTools.Generator.Tests** -- depends on Generator
5. **IoCTools.Testing.Tests** -- depends on Testing
6. **IoCTools.Tools.Cli** -- depends on Generator
7. **IoCTools.Tools.Cli.Tests** -- depends on CLI
8. **IoCTools.Sample** -- depends on Generator + Abstractions

Note: Steps 2 and 3 could build in parallel since Testing compile-includes source files from Generator's directory (not its build output). However, it is cleaner to build Generator first to ensure the shared source files are stable.

### NuGet Package Structure

**IoCTools.Testing.nupkg:**
```
analyzers/
  dotnet/
    cs/
      netstandard2.0/
        IoCTools.Testing.dll          # The test fixture generator
lib/
  netstandard2.0/
    (empty or minimal - generator has no lib output)
build/
  IoCTools.Testing.targets            # MSBuild integration (optional config)
```

The consumer's test project references both packages:
```xml
<!-- Production project -->
<PackageReference Include="IoCTools.Abstractions" Version="1.4.0" />
<PackageReference Include="IoCTools.Generator" Version="1.4.0"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />

<!-- Test project -->
<PackageReference Include="IoCTools.Testing" Version="1.4.0"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
<PackageReference Include="Moq" Version="4.20.72" />
```

## Patterns to Follow

### Pattern 1: Compile-Include for Shared Generator Logic

**What:** Include source files from the main generator project into the testing generator at compile time, avoiding DLL dependency management.

**When:** Two source generators need the same analysis logic (dependency walking, attribute checking, model types).

**Example:**
```xml
<!-- IoCTools.Testing.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <IsRoslynComponent>true</IsRoslynComponent>
  </PropertyGroup>

  <!-- Shared source from main generator -->
  <ItemGroup>
    <Compile Include="..\IoCTools.Generator\IoCTools.Generator\Analysis\DependencyAnalyzer.cs"
             LinkBase="Shared\Analysis" />
    <Compile Include="..\IoCTools.Generator\IoCTools.Generator\Analysis\InjectFieldAnalyzer.cs"
             LinkBase="Shared\Analysis" />
    <Compile Include="..\IoCTools.Generator\IoCTools.Generator\Analysis\DependsOnFieldAnalyzer.cs"
             LinkBase="Shared\Analysis" />
    <Compile Include="..\IoCTools.Generator\IoCTools.Generator\Analysis\ConfigurationFieldAnalyzer.cs"
             LinkBase="Shared\Analysis" />
    <Compile Include="..\IoCTools.Generator\IoCTools.Generator\Models\*.cs"
             LinkBase="Shared\Models" />
    <Compile Include="..\IoCTools.Generator\IoCTools.Generator\Utilities\AttributeTypeChecker.cs"
             LinkBase="Shared\Utilities" />
    <Compile Include="..\IoCTools.Generator\IoCTools.Generator\Utilities\TypeHelpers.cs"
             LinkBase="Shared\Utilities" />
    <Compile Include="..\IoCTools.Generator\IoCTools.Generator\Generator\ServiceDiscovery.cs"
             LinkBase="Shared\Generator" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.5.0" />
  </ItemGroup>
</Project>
```

### Pattern 2: typeof() Argument Extraction

**What:** Extract type symbols from `typeof()` expressions passed as method arguments.

**When:** Analyzing non-generic DI registration calls like `services.AddScoped(typeof(IFoo), typeof(Foo))`.

**Example:**
```csharp
// Inside ManualRegistrationValidator, after the existing generic type args check:
if (typeArgsSymbol.Length == 0)
{
    var args = invocation.ArgumentList.Arguments;
    if (args.Count < 1) continue;

    INamedTypeSymbol? serviceType = null;
    INamedTypeSymbol? implType = null;

    // Single typeof arg: AddScoped(typeof(Foo)) -- self-registration
    if (args.Count >= 1 && args[0].Expression is TypeOfExpressionSyntax typeOfService)
    {
        var typeInfo = semanticModel.GetTypeInfo(typeOfService.Type);
        serviceType = typeInfo.Type as INamedTypeSymbol;
    }

    // Two typeof args: AddScoped(typeof(IFoo), typeof(Foo))
    if (args.Count >= 2 && args[1].Expression is TypeOfExpressionSyntax typeOfImpl)
    {
        var typeInfo = semanticModel.GetTypeInfo(typeOfImpl.Type);
        implType = typeInfo.Type as INamedTypeSymbol;
    }

    if (serviceType == null) continue;
    implType ??= serviceType;

    // Check for open generic: typeof(IRepository<>)
    bool isOpenGeneric = serviceType.IsUnboundGenericType || implType.IsUnboundGenericType;

    // Now apply same IOC081-086 logic, plus new IOC090-094
    // ...
}
```

### Pattern 3: Test Fixture Emission (New)

**What:** Generate a partial class with mock fields and SUT factory based on the target service's dependency graph.

**When:** A partial test class targets a service that has IoCTools-managed dependencies.

**Example of generated output:**
```csharp
// Generated: MyServiceTests_Fixture.g.cs
public partial class MyServiceTests
{
    private Mock<IFoo> _mockFoo;
    private Mock<IBar> _mockBar;
    private Mock<IConfiguration> _mockConfiguration;

    protected MyService CreateSut()
    {
        return new MyService(_mockFoo.Object, _mockBar.Object, _mockConfiguration.Object);
    }

    protected void SetupFoo(Action<Mock<IFoo>> setup) => setup(_mockFoo);
    protected void SetupBar(Action<Mock<IBar>> setup) => setup(_mockBar);

    // Called from constructor or test initialization
    private void InitializeMocks()
    {
        _mockFoo = new Mock<IFoo>();
        _mockBar = new Mock<IBar>();
        _mockConfiguration = new Mock<IConfiguration>();
    }
}
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Embedding Test Generator in Production Generator

**What:** Adding test fixture emission to `DependencyInjectionGenerator.cs` alongside production code generation.

**Why bad:** Forces Moq types to be resolvable in every project that uses IoCTools, even production code. Bloats the production generator with test-only logic. Violates single responsibility. Users cannot opt out of test fixture generation.

**Instead:** Separate `IoCTools.Testing` package with its own `IIncrementalGenerator`.

### Anti-Pattern 2: Shared DLL Between Two Analyzer Assemblies

**What:** Creating a `IoCTools.Generator.Core.dll` referenced by both `IoCTools.Generator.dll` and `IoCTools.Testing.dll` in their respective `analyzers/` directories.

**Why bad:** Both NuGet packages must include the shared DLL in their `analyzers/dotnet/cs/` folder. Version skew between the two packages causes assembly loading conflicts. Roslyn's analyzer loading uses shadow-copying which can fail with duplicate assembly names across packages.

**Instead:** Compile-include shared source files. Both generators compile the same code into their own assembly, avoiding runtime dependency resolution entirely.

### Anti-Pattern 3: IoCTools.Testing Depending on IoCTools.Generator at Runtime

**What:** Adding a `ProjectReference` or `PackageReference` from Testing to Generator expecting to call Generator's methods at runtime.

**Why bad:** Source generators are loaded as analyzers. They cannot reference other analyzer assemblies as regular dependencies. The Generator DLL is not in the normal reference path -- it is in `analyzers/dotnet/cs/`.

**Instead:** Compile-include the shared source files, giving Testing its own compiled copy of the analysis logic.

## Scalability Considerations

| Concern | Current (v1.3) | With IoCTools.Testing (v1.4) | Future |
|---------|----------------|-------------------------------|--------|
| Generator count | 1 generator per project | 1-2 generators per project (prod + test) | Same |
| Build time | Minimal overhead | Two generators run independently; test generator only in test projects | Incremental caching handles scale |
| Shared code drift | N/A | Compile-include means both generators always use same source | If divergence needed, extract to .shproj |
| NuGet package count | 2 (Abstractions + Generator) | 3 (+ Testing) or 4 (+ Testing.Abstractions) | Same |
| Test project complexity | Manual mock setup | Auto-generated fixtures | Add NSubstitute/FakeItEasy support |

## Sources

- [Roslyn Source Generator Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md) -- official patterns for packaging and incremental generators
- [Roslyn Discussion #47517: Reference local projects in Source Generator](https://github.com/dotnet/roslyn/discussions/47517) -- shared code between generator assemblies
- [Thinktecture: Using 3rd-Party Libraries in Source Generators](https://www.thinktecture.com/en/net/roslyn-source-generators-using-3rd-party-libraries/) -- packaging dependencies alongside analyzers
- [Andrew Lock: Supporting multiple .NET SDK versions](https://andrewlock.net/creating-a-source-generator-part-14-supporting-multiple-sdk-versions-in-a-source-generator/) -- multi-targeting for generator packages
- [Meziantou: Working with types in a Roslyn analyzer](https://www.meziantou.net/working-with-types-in-a-roslyn-analyzer.htm) -- TypeInfo/GetTypeInfo patterns
- Existing codebase: `ManualRegistrationValidator.cs`, `DependencyAnalyzer.cs`, `ConstructorEmitter.cs`, `DependencyInjectionGenerator.cs`

---

*Architecture analysis: 2026-03-21*
