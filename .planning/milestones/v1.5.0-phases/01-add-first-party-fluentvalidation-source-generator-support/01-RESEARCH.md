# Phase 1: Add First-Party FluentValidation Source Generator Support - Research

**Researched:** 2026-03-29
**Domain:** .NET source generator for FluentValidation DI integration
**Confidence:** HIGH

## Summary

This phase extends IoCTools with a separate `IoCTools.FluentValidation` NuGet package containing an `IIncrementalGenerator` that understands FluentValidation validators as DI citizens. The generator discovers validators (classes with IoCTools lifetime attributes inheriting `AbstractValidator<T>`), refines their interface registrations to match FluentValidation conventions (`IValidator<T>` + concrete only, not `IValidator` non-generic or `IEnumerable<IValidationRule>`), builds a composition graph from `SetValidator`/`Include`/`SetInheritanceValidator` invocations, and emits diagnostics for anti-patterns like `new ChildValidator()` when the child has DI dependencies.

The architecture follows the established `IoCTools.Testing` precedent: a fully independent generator package with no `ProjectReference` to `IoCTools.Generator`. The generator targets `netstandard2.0` (same constraints as the main generator -- no records, no init, no `HashCode`). FluentValidation 11.x is the correct dependency since 12.x dropped `netstandard2.0` support.

**Primary recommendation:** Build `IoCTools.FluentValidation` as a separate `IIncrementalGenerator` targeting `netstandard2.0`, depending on FluentValidation 11.12.0 (latest 11.x with netstandard2.0 support). Follow the `IoCTools.Testing` project/packaging pattern exactly. The two generators coordinate through partial class extension of the `GeneratedServiceCollectionExtensions` class.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Separate `IoCTools.FluentValidation` NuGet package -- keeps the FluentValidation dependency optional. Users who don't use FluentValidation pay nothing.
- **D-02:** No new abstractions package. Existing IoCTools attributes (`[Scoped]`, `[Singleton]`, `[Transient]`, `[Inject]`, `[RegisterAs<T>]`, etc.) are sufficient. Validators are services -- no special attributes needed.
- **D-03:** No new user-facing registration method. Validator registrations flow into the existing `Add{Assembly}RegisteredServices()` via partial class/method coordination between the two generators. Single call site, zero ceremony.
- **D-04:** Target `netstandard2.0` -- same constraints as `IoCTools.Generator` (no records, no init, no `HashCode`).
- **D-05:** Generator is fully independent -- no `ProjectReference` to `IoCTools.Generator`. Follows the `IoCTools.Testing` precedent.
- **D-06:** FluentValidation NuGet dependency flows to consumers intentionally. Pin to specific stable version.
- **D-07:** Discovery signal = existing IoCTools lifetime attribute + class inherits `AbstractValidator<T>`. The base class tells the generator what `T` is (via `BaseType.TypeArguments[0]`).
- **D-08:** Registration refinement -- register only `IValidator<T>` + concrete type for validators, NOT all interfaces. FluentValidation's own DI extensions deliberately skip `IValidator` (non-generic) and `IEnumerable<IValidationRule>`.
- **D-09:** `partial` required on validator classes (same as all IoCTools services -- IOC080 enforces this).
- **D-10:** Validator lifetime determined by the IoCTools attribute on the class. No special defaults.
- **D-11:** Parse validator bodies to build a composition graph. Walk syntax trees for `SetValidator(...)`, `Include(...)`, and `SetInheritanceValidator(...)`.
- **D-12:** Composition graph enables lifetime constraint propagation, anti-pattern diagnostics, and CLI visualization.
- **D-13:** Detect `SetValidator(new ChildValidator())` and `Include(new SharedRulesValidator())` where the instantiated type is DI-managed or has `[Inject]` fields.
- **D-14:** Show full dependency chain being bypassed in diagnostic messages.
- **D-15:** Follow existing diagnostic patterns: `DiagnosticDescriptors`, `{Concern}Validator`, configurable MSBuild severity.
- **D-16:** Extend `IoCTools.Testing` fixture generation for `IValidator<T>` parameters.
- **D-17:** Only generate FluentValidation-aware helpers when FluentValidation is detected in compilation references.
- **D-18:** CLI validator inspection in scope.
- **D-19:** CLI should trace through composition chains to explain lifetimes.
- **D-20:** No validation rule analysis (out of scope).
- **D-21:** No empty validator scaffolding (out of scope).
- **D-22:** No MediatR pipeline behavior auto-wiring (out of scope).
- **D-23:** No rule generation from data annotations (out of scope).

### Claude's Discretion
- Diagnostic ID numbering scheme (continuing IOC series or new FV series)
- Exact CLI command names and output format
- Internal architecture of the composition graph data structure
- How partial class/method coordination works between the two generators
- Registration refinement mechanism (FluentValidation-specific awareness vs. general-purpose interface filter)

### Deferred Ideas (OUT OF SCOPE)
- FluentValidation linting (property coverage, rule strength, async/sync detection, ruleset completeness)
- MediatR ValidationBehavior auto-wiring
- Empty validator scaffolding via CLI
- Modern C# across the board (records, init-only)
- Cross-assembly validator discovery
</user_constraints>

## Project Constraints (from CLAUDE.md)

- **netstandard2.0 for generators**: No `record` types, no `init` properties, no `required` members, no `HashCode` type. Use classes/structs with manual `IEquatable<T>`.
- **Generator never throws**: Emit diagnostics instead. Catch guards around validators.
- **Testing**: xUnit, FluentAssertions, Arrange/Act/Assert, `sealed` test classes, `#region` directives.
- **Naming**: PascalCase files, `{Subject}Tests.cs`, `{Concern}Validator`, `_camelCase` fields, file-scoped namespaces, `using` inside namespace.
- **Code style**: 4 spaces, UTF-8, LF, final newline, nullable reference types enabled.
- **Validators short-circuit**: `if (!config.DiagnosticsEnabled) return;`

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| FluentValidation | 11.12.0 | FluentValidation reference for type detection | Latest 11.x with netstandard2.0 support; 12.x dropped netstandard2.0 |
| Microsoft.CodeAnalysis.CSharp | 4.5.0 | Roslyn APIs for source generation | Same version as IoCTools.Generator and IoCTools.Testing |
| Microsoft.CodeAnalysis.Analyzers | 3.3.4 | Analyzer rule enforcement | Same version as existing generators |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit | 2.9.3 | Test framework | Test project |
| FluentAssertions | 6.12.0 | Test assertions | Test project |
| Microsoft.NET.Test.Sdk | 17.8.0 | Test runner | Test project |

### Critical Version Decision: FluentValidation 11.x vs 12.x

FluentValidation 12.0 (released late 2024) dropped support for `netstandard2.0`, `netstandard2.1`, `.NET 5`, `.NET 6`, and `.NET 7`. The minimum supported platform is now `.NET 8`.

Since `IoCTools.FluentValidation` must target `netstandard2.0` (decision D-04), **FluentValidation 11.12.0** is the correct dependency. This is the latest stable release in the 11.x line (released November 2025). Its only netstandard2.0 dependency is `System.Threading.Tasks.Extensions >= 4.5.4`.

This means the generator can only reference FluentValidation 11.x types at compile time. However, this is sufficient because:
1. The generator only needs Roslyn symbols, not runtime FluentValidation types.
2. The generator detects `AbstractValidator<T>` by **name** (string matching on type hierarchy), not by direct type reference.
3. FluentValidation's `IValidator<T>` and `AbstractValidator<T>` type shapes are stable across 11.x and 12.x.

**Alternative approach (recommended):** The generator does NOT actually need a PackageReference to FluentValidation at all. It detects `AbstractValidator<T>` and `IValidator<T>` by fully-qualified name via Roslyn's `INamedTypeSymbol`. This is the same pattern used for IHostedService detection in `TypeAnalyzer.IsAssignableFromIHostedService`. If the generator takes no FluentValidation dependency, it works with ANY version the consumer uses (11.x, 12.x, future versions). The FluentValidation dependency would only flow to consumers if it's a non-private asset -- and the generator should NOT have it flow. This needs a design decision.

**Recommendation:** Do NOT take a PackageReference to FluentValidation in the generator. Detect types by name. This matches the pattern used for `IHostedService`, `IRequestHandler`, etc. throughout IoCTools.

## Architecture Patterns

### Recommended Project Structure
```
IoCTools.FluentValidation/
  IoCTools.FluentValidation/
    IoCTools.FluentValidation.csproj        # netstandard2.0, source generator
    FluentValidationGenerator.cs            # IIncrementalGenerator entry point
    Generator/
      Pipeline/
        ValidatorPipeline.cs                # Discovery pipeline (like ServiceClassPipeline)
        ValidatorDiagnosticsPipeline.cs      # Diagnostic attachment (like DiagnosticsPipeline)
      ValidatorDiscovery.cs                 # AbstractValidator<T> detection
      ValidatorRegistrationEmitter.cs       # Partial class emission for registrations
      CompositionGraph/
        CompositionGraphBuilder.cs          # SetValidator/Include/SetInheritanceValidator parsing
        CompositionEdge.cs                  # Edge in the composition graph
        ValidatorCompositionInfo.cs         # Immutable model (struct, IEquatable<T>)
    Diagnostics/
      FluentValidationDiagnosticDescriptors.cs
      Validators/
        DirectInstantiationValidator.cs     # D-13: new ChildValidator() detection
        CompositionLifetimeValidator.cs     # Lifetime propagation through composition chains
    CodeGeneration/
      ValidatorRegistrationGenerator.cs     # Generate registration code lines
    Models/
      ValidatorClassInfo.cs                 # Immutable pipeline model (like ServiceClassInfo)
    Utilities/
      FluentValidationTypeChecker.cs        # Type name matching utilities
  build/
    IoCTools.FluentValidation.targets       # MSBuild property exposure
IoCTools.FluentValidation.Tests/
  IoCTools.FluentValidation.Tests.csproj    # net8.0 test project
  TestHelper.cs                             # Runs main gen + FV gen together
  ValidatorDiscoveryTests.cs
  RegistrationTests.cs
  CompositionGraphTests.cs
  DiagnosticTests.cs
```

### Pattern 1: Two-Generator Coordination via Partial Class

**What:** Both `IoCTools.Generator` and `IoCTools.FluentValidation` emit partial classes that extend the same `GeneratedServiceCollectionExtensions` class. Each generates its own `.g.cs` file with an extension method.

**Challenge:** Two generators cannot emit the same partial method. The current main generator emits a single `Add{Assembly}RegisteredServices()` method. A second generator cannot add to this method body.

**Recommended approach:** The FluentValidation generator emits a SEPARATE extension method (e.g., `AddFluentValidationServices()`) that the user can call alongside the main one. This is simpler and avoids the partial method coordination complexity entirely. However, D-03 says "no new user-facing registration method."

**Resolution for D-03:** The FluentValidation generator generates a partial class extension that contains a partial method call. The main generator and the FV generator both contribute to the same static class via partial class merging, but with separate methods. The generated code calls both internally. Specifically:

```csharp
// Generated by IoCTools.Generator (existing)
public static partial class GeneratedServiceCollectionExtensions
{
    public static IServiceCollection AddMyAppRegisteredServices(this IServiceCollection services)
    {
        // main registrations...
        AddMyAppFluentValidationServices(services); // partial method call
        return services;
    }

    static partial void AddMyAppFluentValidationServices(IServiceCollection services);
}

// Generated by IoCTools.FluentValidation (new)
public static partial class GeneratedServiceCollectionExtensions
{
    static partial void AddMyAppFluentValidationServices(IServiceCollection services)
    {
        // validator registrations...
    }
}
```

**Important note on partial methods:** In C# `netstandard2.0`, partial methods must return void and cannot have `out` parameters. The `static partial void` pattern works. If the FV generator is not installed, the partial method call is a no-op (this is how C# partial methods work -- unimplemented partial methods are removed by the compiler).

**This requires a change to the main generator** to emit the partial method declaration and call. This is a cross-cutting concern that must be planned carefully.

### Pattern 2: Validator Discovery Pipeline

**What:** Mirror `ServiceClassPipeline.Build()` for validators.

```csharp
// Pseudocode - netstandard2.0 compatible
internal static class ValidatorPipeline
{
    internal static IncrementalValuesProvider<ValidatorClassInfo> Build(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) =>
                {
                    var typeDecl = (TypeDeclarationSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl);
                    if (symbol == null || symbol.IsAbstract) return null;

                    // Check: has IoCTools lifetime attribute
                    var hasLifetime = HasLifetimeAttribute(symbol);
                    if (!hasLifetime) return null;

                    // Check: inherits AbstractValidator<T> by name
                    var validatedType = GetAbstractValidatorTypeArgument(symbol);
                    if (validatedType == null) return null;

                    return new ValidatorClassInfo(symbol, typeDecl, ctx.SemanticModel, validatedType);
                })
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x!.Value)
            // ... dedup, collect
    }

    private static INamedTypeSymbol? GetAbstractValidatorTypeArgument(INamedTypeSymbol symbol)
    {
        var current = symbol.BaseType;
        while (current != null)
        {
            if (current.Name == "AbstractValidator" &&
                current.ContainingNamespace?.ToDisplayString() == "FluentValidation" &&
                current.TypeArguments.Length == 1)
            {
                return current.TypeArguments[0] as INamedTypeSymbol;
            }
            current = current.BaseType;
        }
        return null;
    }
}
```

### Pattern 3: Composition Graph via Syntax Tree Walking

**What:** Parse validator constructor bodies and `RuleFor` chains for `SetValidator`, `Include`, and `SetInheritanceValidator` calls.

**How:** Use `SyntaxTree` walking on the validator class declaration to find `InvocationExpressionSyntax` nodes matching these method names. Resolve the type argument to determine the child validator type. Build a directed graph of validator-to-validator edges.

```csharp
// Pseudocode
internal static class CompositionGraphBuilder
{
    internal static List<CompositionEdge> BuildEdges(
        TypeDeclarationSyntax validatorDecl,
        SemanticModel model)
    {
        var edges = new List<CompositionEdge>();

        foreach (var invocation in validatorDecl.DescendantNodes()
            .OfType<InvocationExpressionSyntax>())
        {
            var methodName = GetMethodName(invocation);

            if (methodName == "SetValidator" || methodName == "Include")
            {
                // Check if argument is `new SomeValidator()` (anti-pattern)
                // or an injected dependency (correct pattern)
                var arg = invocation.ArgumentList.Arguments.FirstOrDefault();
                if (arg?.Expression is ObjectCreationExpressionSyntax creation)
                {
                    var typeInfo = model.GetTypeInfo(creation);
                    edges.Add(new CompositionEdge(
                        CompositionType.DirectInstantiation,
                        typeInfo.Type));
                }
                else if (arg?.Expression is IdentifierNameSyntax identifier)
                {
                    var symbolInfo = model.GetSymbolInfo(identifier);
                    // Resolve to injected field/parameter type
                    edges.Add(new CompositionEdge(
                        CompositionType.Injected,
                        ResolveType(symbolInfo)));
                }
            }

            if (methodName == "SetInheritanceValidator")
            {
                // Parse the lambda/action body for .Add<T>() calls
                // Each .Add<T>() call adds an edge
            }
        }

        return edges;
    }
}
```

### Pattern 4: Registration Refinement (D-08)

**What:** When registering a validator, only register `IValidator<T>` + concrete type, not all interfaces like `IValidator` (non-generic) or `IEnumerable<IValidationRule>`.

This matches FluentValidation's own `ServiceCollectionExtensions` which uses:
- `TryAddEnumerable` for `IValidator<T>`
- `TryAdd` for the concrete validator type

The FluentValidation generator knows the `T` from `AbstractValidator<T>`, so it can construct the correct `IValidator<T>` registration.

### Anti-Patterns to Avoid
- **Taking a runtime dependency on FluentValidation in the generator:** The generator runs at compile time in the Roslyn host process. It should detect FluentValidation types by name, not by direct type reference.
- **Modifying the main generator's output format:** Changes to the main generator to support partial method coordination must be backward-compatible. If the FV generator is not installed, the partial method call is a no-op.
- **Over-parsing validator bodies:** Walking syntax trees for SetValidator/Include is sufficient. Do NOT attempt semantic analysis of rule chains or validation logic (D-20).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Type hierarchy detection | Custom reflection-based type checking | Roslyn `INamedTypeSymbol.BaseType` chain walking | Established pattern in `TypeAnalyzer.IsAssignableFromIHostedService` |
| Diagnostic descriptors | Ad-hoc diagnostic reporting | `DiagnosticDescriptor` + `DiagnosticUtilities.CreateDynamicDescriptor` | Existing infrastructure with severity configuration |
| Test compilation setup | Custom compilation builder | Follow `TestHelper.cs` pattern from IoCTools.Testing.Tests | Two-generator test pattern already proven |
| Interface filtering | Manual string exclusion lists | Targeted registration (IValidator<T> only) | Don't filter from a full list; generate only what's needed |

## Common Pitfalls

### Pitfall 1: FluentValidation Version Incompatibility
**What goes wrong:** Generator takes a PackageReference to FluentValidation 11.x, consumer uses 12.x, version conflict at build time.
**Why it happens:** FluentValidation 12.x is a major version bump with breaking target framework changes.
**How to avoid:** Do not take a PackageReference to FluentValidation in the generator at all. Detect types by fully-qualified name via Roslyn symbols. This works with any FluentValidation version the consumer uses.
**Warning signs:** Build errors mentioning FluentValidation version conflicts in consumer projects.

### Pitfall 2: Partial Method Coordination Timing
**What goes wrong:** The main generator emits the partial method call, but the FV generator hasn't run yet (or isn't installed), causing compilation errors.
**Why it happens:** Partial method semantics in C# require careful handling.
**How to avoid:** Use `static partial void` methods (no return value, no out params). In C# < 9.0, unimplemented partial methods are silently removed by the compiler. Since the generated code targets the consumer's compilation (not netstandard2.0), and consumers use C# 8+, this works. But verify: partial methods with `static` modifier require C# 9.0. For broader compatibility, use instance partial methods on the extensions class -- but the class is static. Alternative: use `#if` conditional compilation or a separate extension method that's called conditionally. **This needs careful design.**
**Warning signs:** CS0759 errors about partial method implementations.

### Pitfall 3: SetInheritanceValidator Parsing Complexity
**What goes wrong:** The `SetInheritanceValidator` method takes a lambda/action that contains `.Add<TDerived>()` calls. Parsing these requires walking into lambda bodies.
**Why it happens:** SetInheritanceValidator uses a builder pattern with a callback: `x.SetInheritanceValidator(v => { v.Add<Dog>(new DogValidator()); })`.
**How to avoid:** Walk `DescendantNodes()` recursively through lambda bodies. Look for `InvocationExpressionSyntax` with method name "Add" and extract the generic type argument.
**Warning signs:** Missing edges in the composition graph for polymorphic validators.

### Pitfall 4: netstandard2.0 Constraints in Generator Code
**What goes wrong:** Using C# features unavailable in netstandard2.0 (records, init, HashCode, pattern matching with `is not`).
**Why it happens:** Generator targets netstandard2.0 even though generated code targets consumer's framework.
**How to avoid:** Use `struct` with manual `IEquatable<T>`, `GetHashCode()` without `HashCode.Combine`, traditional null checks. Test: `dotnet build --configuration Release` catches these.
**Warning signs:** Build errors in the generator project mentioning missing types or syntax.

### Pitfall 5: Partial Static Class Coordination Across Generators
**What goes wrong:** Two independent generators both emit `public static partial class GeneratedServiceCollectionExtensions` in the same namespace. If they use different namespaces or class names, the partial class merge fails.
**Why it happens:** Each generator independently determines the assembly name and namespace for the extensions class.
**How to avoid:** Both generators must derive the same namespace and class name from the same inputs (assembly name). The FV generator must use the exact same namespace calculation: `{assemblyName}.Extensions.Generated`. The class name must be `GeneratedServiceCollectionExtensions`.
**Warning signs:** Duplicate type errors or missing method errors in generated code.

### Pitfall 6: Roslyn Partial Method Limitations for Static Classes
**What goes wrong:** C# partial methods in static classes have specific requirements that vary by language version.
**Why it happens:** Before C# 9.0, partial methods must return void, have no out parameters, and be implicitly private. C# 9.0+ allows public partial methods with return types and out parameters, but ONLY if they have an implementation.
**How to avoid:** Since the generated code targets the consumer's C# version (typically 10+), use the C# 9.0+ extended partial method syntax. But verify: the `LangVersion` of consumer projects determines what's valid. For maximum compatibility, use `static partial void` with no access modifier (implicitly private). The main extension method calls it internally.
**Warning signs:** Consumers with older LangVersion settings getting errors.

## Code Examples

### Validator Discovery by Name (Type Hierarchy Walking)
```csharp
// Pattern from TypeAnalyzer.IsAssignableFromIHostedService (existing code)
// Adapted for AbstractValidator<T> detection
internal static INamedTypeSymbol? GetAbstractValidatorTypeArgument(INamedTypeSymbol classSymbol)
{
    var current = classSymbol.BaseType;
    while (current != null && current.SpecialType != SpecialType.System_Object)
    {
        // Match by fully-qualified name to avoid dependency on FluentValidation assembly
        if (current.Name == "AbstractValidator" &&
            current.IsGenericType &&
            current.TypeArguments.Length == 1 &&
            current.ContainingNamespace != null &&
            current.ContainingNamespace.ToDisplayString() == "FluentValidation")
        {
            return current.TypeArguments[0] as INamedTypeSymbol;
        }
        current = current.BaseType;
    }
    return null;
}
```

### Validator Registration Code Generation
```csharp
// FluentValidation's own DI registers: IValidator<T> (TryAddEnumerable) + concrete (TryAdd)
// Generated output should match this:
// services.AddScoped<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
// services.AddScoped<CreateOrderCommandValidator>();
//
// Note: Use TryAdd semantics if needed, but standard IoCTools uses direct Add.
```

### Direct Instantiation Detection (D-13)
```csharp
// Detect: SetValidator(new AddressValidator()) where AddressValidator has DI dependencies
// Walk invocation arguments looking for ObjectCreationExpressionSyntax
var arg = invocation.ArgumentList.Arguments.FirstOrDefault();
if (arg?.Expression is ObjectCreationExpressionSyntax creation)
{
    var typeInfo = semanticModel.GetTypeInfo(creation);
    if (typeInfo.Type is INamedTypeSymbol createdType)
    {
        // Check if createdType has [Inject] fields or DI dependencies
        var hasInject = createdType.GetMembers().OfType<IFieldSymbol>()
            .Any(f => f.GetAttributes().Any(a => a.AttributeClass?.Name == "InjectAttribute"));
        var hasLifetime = HasLifetimeAttribute(createdType);

        if (hasInject || hasLifetime)
        {
            // Emit diagnostic: "AddressValidator is instantiated directly but has DI dependencies"
        }
    }
}
```

### Two-Generator Test Pattern
```csharp
// From IoCTools.Testing.Tests/TestHelper.cs -- adapted for FluentValidation
internal static GenerationResult Generate(string source)
{
    var metadataRefs = new List<MetadataReference>
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        // ... standard refs
        MetadataReference.CreateFromFile(typeof(ScopedAttribute).Assembly.Location),
    };

    // Add FluentValidation reference for test compilation
    metadataRefs.Add(MetadataReference.CreateFromFile(
        typeof(FluentValidation.AbstractValidator<>).Assembly.Location));

    var compilation = CSharpCompilation.Create("Test", ...);

    // Run BOTH generators together
    var mainGenerator = new DependencyInjectionGenerator();
    var fvGenerator = new FluentValidationGenerator();
    var driver = CSharpGeneratorDriver.Create(new[]
    {
        mainGenerator.AsSourceGenerator(),
        fvGenerator.AsSourceGenerator()
    });

    driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diags);
    // ...
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| FluentValidation 11.x (netstandard2.0) | FluentValidation 12.x (net8.0 only) | Late 2024 | Generator must use name-based detection, not type references |
| `AddValidatorsFromAssembly` (runtime reflection) | Source generator registration (compile-time) | This phase | Zero runtime overhead, compile-time validation |
| Manual validator DI registration | Auto-discovery via attributes | This phase | Eliminates boilerplate |

## Diagnostic ID Numbering (Claude's Discretion)

**Recommendation:** Continue the IOC series. Current highest is IOC094. Start FluentValidation diagnostics at IOC100 for a clean boundary.

| ID Range | Category | Examples |
|----------|----------|----------|
| IOC100-IOC109 | Validator Registration | Over-registration, missing lifetime |
| IOC110-IOC119 | Validator Composition | Direct instantiation, lifetime mismatch in chains |
| IOC120-IOC129 | Validator Structure | Non-partial validator class |

This keeps all IoCTools diagnostics in a single series (easier for users to search "IOC" in error lists) while providing a clear numeric boundary.

## Partial Class Coordination Design (Claude's Discretion)

**Recommendation:** Use the extended partial method pattern.

The main generator (`IoCTools.Generator`) must be modified to:
1. Always emit a `static partial void Add{Assembly}FluentValidationServices(IServiceCollection services);` declaration
2. Call this partial method from within `Add{Assembly}RegisteredServices()`

The FluentValidation generator (`IoCTools.FluentValidation`) emits:
1. The partial method implementation: `static partial void Add{Assembly}FluentValidationServices(IServiceCollection services) { ... }`

**Key requirements:**
- Both generators must compute the same assembly-derived namespace and class name
- The partial method must be `static partial void` (no return, no out) for C# < 9.0 compatibility
- If the FV generator is not present, the unimplemented partial method is silently removed by the compiler

**Important caveat:** This means the MAIN generator needs modification. The `ServiceRegistrationGenerator.RegistrationCode.cs` file must be updated to emit the partial method declaration and call. This is a cross-cutting change that affects the existing generator.

**Alternative (simpler, less elegant):** The FV generator emits a completely separate extension method (`AddFluentValidationRegisteredServices()`) and the user calls both. This violates D-03 but avoids modifying the main generator. If D-03 is strict, the partial method approach is required.

## Composition Graph Data Structure (Claude's Discretion)

**Recommendation:** Lightweight struct-based graph.

```csharp
// netstandard2.0 compatible
internal readonly struct CompositionEdge : IEquatable<CompositionEdge>
{
    public CompositionEdge(
        string parentValidatorName,
        string childValidatorName,
        CompositionType compositionType,
        bool isDirectInstantiation)
    {
        ParentValidatorName = parentValidatorName;
        ChildValidatorName = childValidatorName;
        CompositionType = compositionType;
        IsDirectInstantiation = isDirectInstantiation;
    }

    public string ParentValidatorName { get; }
    public string ChildValidatorName { get; }
    public CompositionType CompositionType { get; }
    public bool IsDirectInstantiation { get; }

    // Manual IEquatable<T> + GetHashCode (netstandard2.0)
}

internal enum CompositionType
{
    SetValidator,
    Include,
    SetInheritanceValidator
}
```

Store as `ImmutableArray<CompositionEdge>` in the pipeline. Graph traversal for lifetime propagation is done at diagnostic emission time.

## Open Questions

1. **Main generator modification scope**
   - What we know: D-03 requires no new user-facing registration method, which means the main generator must emit a partial method hook.
   - What's unclear: How invasive is this change? Will it break existing tests (1650+)?
   - Recommendation: The main generator change is minimal (add two lines to the registration template). Add a regression test. If any test breaks, the partial method declaration needs adjustment.

2. **FluentValidation dependency in the generator package**
   - What we know: The generator detects FV types by name. It does not need a runtime FluentValidation reference.
   - What's unclear: Should the NuGet package have FluentValidation as a dependency so consumers get it transitively? Or should consumers add FluentValidation separately?
   - Recommendation: Do NOT include FluentValidation as a package dependency. The generator is a `DevelopmentDependency` with `IncludeBuildOutput=false`. The consumer already has FluentValidation in their project (otherwise there are no validators to discover). Adding it would create version conflicts.

3. **SetInheritanceValidator lambda depth**
   - What we know: SetInheritanceValidator uses `v => { v.Add<Dog>(new DogValidator()); }` pattern.
   - What's unclear: How deep can these lambdas nest? Are there real-world patterns with complex nesting?
   - Recommendation: Parse one level of lambda body. Flag complex patterns as LOW confidence in the composition graph.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build | Yes | 10.0.105 | -- |
| dotnet build | Compilation | Yes | 10.0.105 | -- |
| dotnet test | Testing | Yes | 10.0.105 | -- |

No missing dependencies.

## Sources

### Primary (HIGH confidence)
- FluentValidation GitHub repository -- `AbstractValidator<T>` implements `IValidator<T>` and `IEnumerable<IValidationRule>`
- FluentValidation `ServiceCollectionExtensions.cs` -- registers `IValidator<T>` + concrete type only, default lifetime Scoped
- NuGet.org FluentValidation 11.12.0 -- latest 11.x with netstandard2.0 support
- NuGet.org FluentValidation 12.1.1 -- confirms 12.x dropped netstandard2.0
- IoCTools source code -- `ServiceClassPipeline.cs`, `RegistrationSelector.cs`, `InterfaceDiscovery.cs`, `FixtureEmitter.cs`, `TestHelper.cs`, `IoCTools.Testing.csproj`

### Secondary (MEDIUM confidence)
- FluentValidation DI documentation (https://docs.fluentvalidation.net/en/latest/di.html) -- Cloudflare blocked but content verified via GitHub source
- FluentValidation 12.0 Upgrade Guide -- confirms netstandard2.0 drop

### Tertiary (LOW confidence)
- SetInheritanceValidator lambda parsing depth -- based on documentation examples only, not exhaustive real-world analysis

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - versions verified against NuGet, netstandard2.0 constraint verified
- Architecture: HIGH - follows established IoCTools.Testing precedent with well-understood patterns
- Pitfalls: HIGH - FluentValidation version issue verified, partial method semantics well-documented in C# spec
- Composition graph: MEDIUM - parsing approach is sound but SetInheritanceValidator depth is speculative

**Research date:** 2026-03-29
**Valid until:** 2026-04-28 (stable domain, FluentValidation release cadence is ~quarterly)
