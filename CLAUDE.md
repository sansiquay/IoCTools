# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IoCTools is a .NET source generator library that simplifies dependency injection and service registration in .NET applications. The project has evolved to include comprehensive feature coverage with robust diagnostic validation and production-ready architecture.

**Core Components:**
- **IoCTools.Abstractions**: Individual lifetime attributes (`[Scoped]`, `[Singleton]`, `[Transient]`) plus dependency injection annotations (`InjectAttribute`, `DependsOn`, etc.)
- **IoCTools.Generator**: Advanced source generator with intelligent service registration, full inheritance support, and comprehensive diagnostics (IOC001-IOC086)
- **IoCTools.Sample**: Comprehensive demonstration project with complete feature coverage across 18 service example files

**Sample Application Coverage:**
The sample project provides complete coverage of all IoCTools features:
- Basic usage patterns with modern lifetime attributes (`[Scoped]`, `[Singleton]`, `[Transient]`)
- Intelligent service registration with automatic partial class detection
- Advanced patterns with complex scenarios and architectural enhancements
- Inheritance hierarchies with proper constructor generation
- Generic services with enhanced collection injection patterns
- Configuration injection with comprehensive binding examples
- Multi-interface registration patterns with automatic collection wrappers
- Selective interface registration with RegisterAs<T> attributes supporting InstanceSharing (Separate/Shared) and RegisterAs-only service patterns
- Conditional service deployment scenarios
- Background service integration with unified IHostedService detection
- Collection injection with intelligent dependency resolution
- External service integration with cross-assembly awareness
- Mixed dependency patterns (field + configuration injection)
- Unregistered service examples with enhanced diagnostics
- Comprehensive diagnostic examples (IOC001-IOC086) with configurable severity
- Performance optimizations and architectural best practices

## Development Commands

### Building
```bash
dotnet build
dotnet build --configuration Release
```

### Running the Sample
```bash
dotnet run --project IoCTools.Sample
```

### Creating NuGet Packages
```bash
dotnet pack
```

### Running Tests
The project includes both formal test suites and comprehensive sample integration tests:

**Formal Test Suite:**
```bash
cd IoCTools.Generator.Tests
dotnet test  # Runs 1244+ comprehensive test files covering all generator scenarios
```

**CLI Test Suite:**
```bash
cd IoCTools.Tools.Cli.Tests
dotnet test  # Runs CLI integration tests
```

**Sample Application Integration Tests:**
The comprehensive sample application serves as an extensive integration test suite with 18 service example files demonstrating every feature and edge case.

### Testing Internal Members

For testing internal methods (e.g., helper utilities in the CLI project), use the `InternalsVisibleTo` attribute:

**In the source project (e.g., `IoCTools.Tools.Cli.csproj`):**
```xml
<ItemGroup>
  <InternalsVisibleTo Include="IoCTools.Tools.Cli.Tests" />
</ItemGroup>
```

This pattern allows test projects to access `internal` methods without exposing them publicly, enabling comprehensive unit testing of implementation details while maintaining proper encapsulation.

### Testing Patterns

#### Enum and Value Type Testing

When testing methods that process enums or other value types with finite value sets, follow this pattern (see `LifetimeCompatibilityCheckerTests.cs` as an example):

**1. Cover all enum value combinations:**
```csharp
[Fact]
public void GetViolationType_SingletonScoped_ReturnsSingletonDependsOnScoped()
{
    var result = LifetimeCompatibilityChecker.GetViolationType("Singleton", "Scoped");
    result.Should().Be(LifetimeViolationType.SingletonDependsOnScoped);
}
```

**2. Test null input handling:**
```csharp
[Fact]
public void GetViolationType_NullConsumer_ReturnsCompatible()
{
    var result = LifetimeCompatibilityChecker.GetViolationType(null, "Scoped");
    result.Should().Be(LifetimeViolationType.Compatible);
}
```

**3. Test invalid/unexpected input:**
```csharp
[Fact]
public void GetViolationType_InvalidLifetime_ReturnsCompatible()
{
    var result = LifetimeCompatibilityChecker.GetViolationType("Invalid", "Scoped");
    result.Should().Be(LifetimeViolationType.Compatible);
}
```

**4. Use `#region` directives to organize related tests:**
```csharp
#region GetViolationType - Singleton Consumer
// Tests for Singleton consumer scenarios
#endregion

#region GetViolationType - Null and Invalid Inputs
// Tests for edge cases
#endregion
```

This ensures:
- All enum value combinations are tested
- Null inputs are handled gracefully
- Invalid inputs don't cause crashes
- Edge cases are explicitly validated
- Tests are organized and maintainable

### Diagnostics and Validation
IoCTools includes a comprehensive diagnostic system (IOC001-IOC086) that validates dependency injection configuration at build time with cross-assembly awareness. The diagnostic system provides configurable MSBuild severity levels and catches common DI mistakes before runtime.

**Quick Diagnostic Test:**
```bash
cd IoCTools.Sample
dotnet build  # Shows diagnostic warnings for deliberately problematic examples
```

**Configure Diagnostic Severity:**
Add these MSBuild properties to your project file:

```xml
<PropertyGroup>
  <!-- Configure severity for missing implementations (default: Error) -->
  <IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>

  <!-- Configure severity for unregistered implementations (default: Error) -->
  <IoCToolsManualSeverity>Info</IoCToolsManualSeverity>

  <!-- Disable all dependency validation diagnostics (default: false) -->
  <IoCToolsDisableDiagnostics>true</IoCToolsDisableDiagnostics>
</PropertyGroup>
```

**Configure Cross-Assembly Interface Patterns:**

When using clean architecture patterns where interfaces are defined in one assembly but implemented in another (unreferenced) assembly, you can configure patterns to ignore IOC001/IOC002 diagnostics for these cross-assembly interfaces:

```xml
<PropertyGroup>
  <!--
    Patterns for cross-assembly interfaces to ignore (semicolon-separated).
    Supports * wildcard for matching any characters.
    Default: *.Abstractions.*;*.Contracts.*;*.Interfaces.*;*.ILoggerService<*
  -->
  <IoCToolsIgnoredTypePatterns>*.Abstractions.*;*.Contracts.*;*.Interfaces.*;*.MyProject.Services.*</IoCToolsIgnoredTypePatterns>
</PropertyGroup>
```

**Severity Options**: `Error`, `Warning`, `Info`, `Hidden`

**Key Diagnostics:**
- **IOC001/IOC002**: Cross-assembly aware missing/unregistered implementation detection with intelligent fix suggestions (configurable via `IoCToolsNoImplementationSeverity` / `IoCToolsManualSeverity`)
- **IOC003**: Circular dependency detected (configurable via `IoCToolsLifetimeValidationSeverity`)
- **IOC011**: IHostedService partial class validation with enhanced detection
- **IOC012/IOC013**: Lifetime violation errors (Singleton→Scoped/Transient) with inheritance chain analysis (IOC012 configurable via `IoCToolsLifetimeValidationSeverity`)
- **IOC014**: Enhanced background service lifetime validation with unified IHostedService detection
- **IOC015**: Inheritance chain lifetime conflicts with deep hierarchy support (configurable via `IoCToolsLifetimeValidationSeverity`)
- **IOC006-IOC009**: Registration conflict detection with deduplication strategies
- **IOC029-IOC031**: RegisterAs attribute validation (interface implementation, non-interface types, duplicates)
- **IOC080**: Missing partial keyword on classes using code-generating attributes (`[Inject]`, `[DependsOn]`, etc.)
- **Enhanced Error Messages**: All diagnostics now reference individual lifetime attributes (`[Scoped]`, `[Singleton]`, `[Transient]`)

### Diagnostic Severity Reference

| Code | Severity | Title |
|------|----------|-------|
| IOC001 | Error | No implementation found for interface |
| IOC002 | Error | Implementation exists but not registered |
| IOC003 | Error | Circular dependency detected (¹) |
| IOC004 | Error | RegisterAsAll attribute requires Service attribute |
| IOC005 | Warning | SkipRegistration attribute has no effect without RegisterAsAll |
| IOC006 | Warning | Duplicate dependency type in DependsOn attributes |
| IOC007 | Warning | DependsOn type conflicts with Inject field |
| IOC008 | Warning | Duplicate type in single DependsOn attribute |
| IOC009 | Warning | SkipRegistration for interface not registered by RegisterAsAll |
| IOC010 | Warning | Background service with non-Singleton lifetime (deprecated, use IOC014) |
| IOC011 | Error | Background service class must be partial |
| IOC012 | Error | Singleton service depends on Scoped service (¹) |
| IOC013 | Warning | Singleton service depends on Transient service |
| IOC014 | Error | Background service with non-Singleton lifetime |
| IOC015 | Error | Service lifetime mismatch in inheritance chain (¹) |
| IOC016 | Error | Invalid configuration key |
| IOC017 | Warning | Unsupported configuration type |
| IOC018 | Error | InjectConfiguration requires partial class |
| IOC019 | Warning | InjectConfiguration on static field not supported |
| IOC020 | Warning | Conditional service has conflicting conditions |
| IOC021 | Error | ConditionalService attribute requires Service attribute |
| IOC022 | Warning | ConditionalService attribute has no conditions |
| IOC023 | Warning | ConfigValue specified without Equals or NotEquals |
| IOC024 | Warning | Equals or NotEquals specified without ConfigValue |
| IOC025 | Warning | ConfigValue is empty or whitespace |
| IOC026 | Warning | Multiple ConditionalService attributes on same class |
| IOC027 | Info | Potential duplicate service registration |
| IOC028 | Error | RegisterAs attribute requires service indicators |
| IOC029 | Error | RegisterAs specifies unimplemented interface |
| IOC030 | Warning | RegisterAs contains duplicate interface |
| IOC031 | Error | RegisterAs specifies non-interface type |
| IOC032 | Warning | RegisterAs attribute is redundant |
| IOC033 | Warning | Scoped lifetime attribute is redundant |
| IOC034 | Warning | RegisterAsAll already registers every interface |
| IOC035 | Warning | Inject field can be simplified to DependsOn |
| IOC036 | Warning | Multiple lifetime attributes declared |
| IOC037 | Warning | SkipRegistration overrides other registration attributes |
| IOC038 | Warning | SkipRegistration for interfaces has no effect in RegisterAsAll(DirectOnly) |
| IOC039 | Warning | Dependency declared but never used |
| IOC040 | Warning | Redundant dependency declarations |
| IOC041 | Error | Manual constructor conflicts with IoCTools dependencies |
| IOC042 | Warning | External dependency flag is not required |
| IOC043 | Warning | IOptions\<T\> should use DependsOnConfiguration |
| IOC044 | Warning | Dependency type is not a service |
| IOC045 | Warning | Collection dependency type is not supported |
| IOC046 | Warning | Overlapping configuration bindings |
| IOC047 | Info | Use params-style attribute arguments |
| IOC048 | Warning | Dependencies must be non-nullable |
| IOC049 | Error | Dependency sets must be metadata-only |
| IOC050 | Error | Dependency set cycle detected |
| IOC051 | Error | Dependency set expansion collided with an existing dependency |
| IOC052 | Warning | Dependency sets must not be registered as services |
| IOC053 | Info | Repeated dependency cluster could be a dependency set |
| IOC054 | Info | Service nearly matches an existing dependency set |
| IOC055 | Info | Shared dependency cluster on related services |
| IOC056 | Info | Use a single configuration binding style per section |
| IOC057 | Warning | Configuration binding not found |
| IOC058 | Info | Apply lifetime attribute to shared base class |
| IOC059 | Warning | Singleton lifetime attribute is redundant |
| IOC060 | Warning | Transient lifetime attribute is redundant |
| IOC061 | Warning | Dependency set already applied in base class |
| IOC062 | Info | Move shared dependency set to base class |
| IOC063 | Warning | RegisterAs attribute is redundant on derived class |
| IOC064 | Info | Move shared RegisterAs to base class |
| IOC065 | Warning | RegisterAsAll attribute is redundant on derived class |
| IOC067 | Warning | ConditionalService attribute is redundant on derived class |
| IOC068 | Info | Constructor parameters could be expressed with [DependsOn] and lifetime attribute |
| IOC069 | Warning | RegisterAs requires a lifetime attribute |
| IOC070 | Warning | DependsOn/Inject used without lifetime |
| IOC071 | Warning | ConditionalService missing lifetime |
| IOC072 | Warning | Hosted service lifetime should be implicit |
| IOC074 | Info | Multi-interface class could use RegisterAsAll |
| IOC075 | Warning | Inconsistent lifetimes across inherited services |
| IOC076 | Warning | Property redundantly wraps IoCTools dependency field |
| IOC077 | Error | Manual field shadows IoCTools-generated dependency |
| IOC078 | Warning | MemberNames entry is suppressed by existing field |
| IOC079 | Warning | Prefer DependsOnConfiguration over IConfiguration |
| IOC080 | Error | Service class must be partial |
| IOC081 | Warning | Manual registration duplicates IoCTools registration |
| IOC082 | Error | Manual registration lifetime differs from IoCTools |
| IOC083 | Error | Manual options registration duplicates IoCTools binding |
| IOC084 | Warning | Lifetime attribute duplicates inherited lifetime |
| IOC085 | Warning | Member name matches default |
| IOC086 | Warning | Manual registration could use IoCTools attributes |

**Note:** This table reflects the current diagnostic severities as defined in `DiagnosticDescriptors.cs`. Severities can be configured per-project using MSBuild properties (see "Configure Diagnostic Severity" above).

**¹** Severity configurable via `IoCToolsLifetimeValidationSeverity` MSBuild property.

Comprehensive diagnostic examples are available in `DiagnosticExamples.cs` within the sample application.

## Architecture

### Core Generator Logic
The main source generator is located in `IoCTools.Generator/DependencyInjectionGenerator.cs`. This advanced generator provides:

1. **Intelligent Service Discovery**: Automatically detects services through lifetime attributes (`[Scoped]`, `[Singleton]`, `[Transient]`), dependency injection patterns (`[Inject]`, `[DependsOn]`), and partial classes implementing interfaces
2. **Constructor Generation**: Creates constructors for classes with `[Inject]` fields and `[DependsOn]` attributes
3. **Inheritance Support**: Full inheritance chain analysis with proper base constructor calling
4. **Registration Methods**: Generates extension methods with cross-assembly type discovery
5. **Enhanced Lifetime Management**: Individual lifetime attributes (`[Scoped]`, `[Singleton]`, `[Transient]`) with comprehensive validation and inheritance chain analysis
6. **Advanced Generic Support**: Handles generic services with intelligent collection wrapper generation and automatic `IEnumerable<T>` injection
7. **Comprehensive Diagnostic Validation**: Cross-assembly aware compile-time validation (IOC001-IOC086) with configurable MSBuild severity levels
8. **Configuration Integration**: Enhanced configuration binding with primitive type support
9. **IHostedService Integration**: Unified detection and registration of background services

### Key Patterns and Best Practices
- **Service Declaration**: Services must be marked `partial` to enable constructor generation
- **Modern Lifetime Specification**: Use individual attributes `[Scoped]`, `[Singleton]`, or `[Transient]` for clean lifetime specification
- **Intelligent Service Registration**: Partial classes implementing interfaces are automatically detected and registered
- **Field Injection**: `[Inject]` fields automatically get constructor parameters with type safety
- **Declarative Dependencies**: `[DependsOn<T1, T2>]` with naming conventions and multiple generic parameters
- **Configuration Binding**: `[InjectConfiguration]` for automatic configuration section binding with validation
- **External Services**: `[ExternalService]` marks manually registered dependencies with cross-assembly awareness
- **Interface Mapping**: `[RegisterAsAll]` with `RegistrationMode` control registration patterns
- **Selective Interface Registration**: `[RegisterAs<T1, T2, ...>]` with InstanceSharing control:
  - `InstanceSharing.Separate` (default): Different instances per interface  
  - `InstanceSharing.Shared`: Same instance across all interfaces (factory pattern)
  - Supports up to 8 type parameters and RegisterAs-only services
- **Background Services**: Automatic IHostedService detection with unified registration patterns
- **Collection Injection**: Automatic `IEnumerable<T>` wrapper generation for service collections
- **Mixed Dependencies**: Services can combine field injection, configuration injection, and DependsOn patterns

### Generated Code Location
Generated files are placed in the compilation output and include:
- Service registration extension methods
- Constructor generation for partial classes  
- Dependency field generation for `DependsOn` attributes

### RegisterAs InstanceSharing Examples

**InstanceSharing.Separate (Default):**
```csharp
[RegisterAs<IUserService, INotificationService>]  // or InstanceSharing.Separate
public partial class MyService : IUserService, INotificationService { }
// Generates: services.AddScoped<IUserService, MyService>(); 
//           services.AddScoped<INotificationService, MyService>();
```

**InstanceSharing.Shared (Factory Pattern):**  
```csharp
[Singleton] // Explicit non-default lifetime because state is shared
[RegisterAs<IUserService, INotificationService>(InstanceSharing.Shared)]
public partial class SharedService : IUserService, INotificationService { }
// Generates: services.AddScoped<SharedService>();
//           services.AddScoped<IUserService>(provider => provider.GetRequiredService<SharedService>());
//           services.AddScoped<INotificationService>(provider => provider.GetRequiredService<SharedService>());
```

**EF Core DbContext Integration:**
```csharp
[RegisterAs<ITransactionService>(InstanceSharing.Shared)]  // No [Scoped] - registered by EF Core
public partial class MyDbContext : DbContext, ITransactionService { }
// Generates: services.AddScoped<ITransactionService>(provider => provider.GetRequiredService<MyDbContext>());
// DbContext registered separately by services.AddDbContext<MyDbContext>()
```

## Project Structure

```
IoCTools/
├── IoCTools.Abstractions/           # Individual lifetime attributes and dependency injection annotations
│   ├── Annotations/                 # [Scoped], [Singleton], [Transient], InjectAttribute, DependsOn, etc.
│   └── Enumerations/                # RegistrationMode, NamingConvention, conditional enums
├── IoCTools.Generator/              # Advanced source generator with intelligent service registration
│   ├── Analysis/                    # DependencyAnalyzer, TypeAnalyzer with enhanced logic
│   ├── CodeGeneration/              # ServiceRegistrationGenerator, ConstructorGenerator
│   ├── Diagnostics/                 # Comprehensive IOC001-IOC086 diagnostic system
│   ├── Models/                      # ServiceRegistration, ConditionalServiceRegistration
│   ├── Utilities/                   # Helper classes for symbol analysis
│   └── IoCTools.Generator/          # Main generator with intelligent detection & cross-assembly validation
├── IoCTools.Generator.Tests/        # Formal test suite with 57+ comprehensive test files
│   └── **/*Tests.cs                # Complete coverage of generator scenarios
└── IoCTools.Sample/                 # Comprehensive demonstration project with architectural enhancements
    ├── Configuration/               # Configuration models and injection examples
    ├── Services/                    # 18 comprehensive service example files:
    │   ├── BasicUsageExamples.cs               # Core patterns with modern lifetime attributes
    │   ├── ArchitecturalEnhancementsExamples.cs # Showcases architectural enhancements
    │   ├── AdvancedPatternsDemo.cs             # Complex scenarios and edge cases  
    │   ├── InheritanceExamples.cs              # Multi-level inheritance chains
    │   ├── GenericServiceExamples.cs           # Generic patterns with collection injection
    │   ├── ConfigurationInjectionExamples.cs   # Configuration binding with validation
    │   ├── MultiInterfaceExamples.cs           # Interface registration with automatic collections
    │   ├── ConditionalServiceExamples.cs       # Conditional registration patterns
    │   ├── BackgroundServiceExamples.cs        # Hosted service patterns with unified detection
    │   ├── CollectionInjectionExamples.cs      # Enhanced collection dependency injection
    │   ├── ExternalServiceExamples.cs          # External service integration
    │   ├── TransientServiceExamples.cs         # Transient lifetime patterns
    │   ├── ManualServiceExamples.cs            # Manual registration scenarios
    │   ├── RegisterAsExamples.cs               # Selective interface registration with InstanceSharing (Separate/Shared) patterns
    │   ├── DiagnosticExamples.cs               # Comprehensive diagnostic system demonstration
    │   ├── DependsOnExamples.cs                # DependsOn attribute patterns
    │   ├── UnregisteredServiceExamples.cs      # Unregistered service scenarios
    │   └── SharedModels.cs                     # Common models and interfaces
    └── Interfaces/                  # Service contracts and abstractions
```

## Development Notes

### Project Status
- **Platform**: .NET 9.0 project using the latest C# language features
- **Compatibility**: Generator targets `netstandard2.0` for broad framework support
- **Packaging**: Both packages configured for automatic NuGet packaging (`GeneratePackageOnBuild`)
- **Maturity**: v1.3.0 release with production-ready architecture and comprehensive validation
- **Testing**: Comprehensive test coverage across formal test suite (1650+ tests) and sample application integration testing
- **Architecture**: Intelligent service registration with individual lifetime attributes and cross-assembly diagnostic validation

### Performance Considerations
- **Build Performance**: Generator optimized for incremental compilation
- **Runtime Overhead**: Zero runtime overhead - all code generated at compile time
- **Memory Usage**: Generated constructors use efficient dependency patterns
- **Diagnostic Cost**: Comprehensive validation with minimal build time impact

### Platform Constraints

**Generator Target Framework**: `IoCTools.Generator` targets `netstandard2.0` to ensure compatibility with a broad range of .NET frameworks (.NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+).

**Language Feature Limitations**: Due to the `netstandard2.0` target, the following modern C# features are **NOT available** in the generator code:

- **Record types** (`record class`, `record struct`) - Use regular classes/structs or tuples instead
- **Init-only properties** (`init` accessor) - Use private setters with constructor parameters
- **Required members** (`required` modifier) - Use constructor validation for required fields
- **Nullable reference types annotations** - Use reference type nullability analysis with caution
- **Pattern matching enhancements** (some advanced patterns) - Stick to basic pattern matching

**Workarounds**:
- For record-like behavior, use classes with readonly fields and constructor-based initialization
- For init-only property semantics, use private setters and validate in constructor
- For value tuples, use `System.ValueTuple` which is available in netstandard2.0
- For hash code computation, create manual `GetHashCode()` implementations (no `HashCode` type)

**Example** - Instead of:
```csharp
private readonly record struct ServiceKey(string Name, string Lifetime);
```

Use:
```csharp
private readonly struct ServiceKey : IEquatable<ServiceKey>
{
    public string Name { get; }
    public string Lifetime { get; }
    public ServiceKey(string name, string lifetime) { Name = name; Lifetime = lifetime; }
    public bool Equals(ServiceKey other) => Name == other.Name && Lifetime == other.Lifetime;
    public override bool Equals(object obj) => obj is ServiceKey sk && Equals(sk);
    public override int GetHashCode()
    {
        // Manual hash code since HashCode type isn't available
        return (Name?.GetHashCode() * 397) ^ (Lifetime?.GetHashCode() * 113);
    }
}
```

### Working with the Sample Application
The sample application serves multiple purposes:
1. **Feature Documentation**: Live examples of every IoCTools capability
2. **Integration Testing**: Validates generator behavior across complex scenarios
3. **Diagnostic Demonstration**: Shows all diagnostic messages in action
4. **Best Practices Guide**: Demonstrates recommended usage patterns
5. **Performance Reference**: Includes performance-conscious implementations

**Key Sample Navigation:**
- **Start Here**: `ArchitecturalEnhancementsExamples.cs` for modern patterns and architectural enhancements
- **Core Patterns**: `BasicUsageExamples.cs` for fundamental usage with individual lifetime attributes
- **Diagnostics**: `DiagnosticExamples.cs` for comprehensive validation understanding (IOC001-IOC086)
- **Advanced Scenarios**: `InheritanceExamples.cs` for complex inheritance with enhanced constructor generation
- **Registration Patterns**: `RegisterAsExamples.cs` for selective interface registration with InstanceSharing modes
- **Collections**: `CollectionInjectionExamples.cs` for intelligent collection dependency injection
- **Complex Use Cases**: `AdvancedPatternsDemo.cs` for sophisticated architectural patterns

## Architectural Limits

After comprehensive development and testing, certain advanced scenarios are intentional architectural limits rather than bugs. These limits were identified during extensive testing phases that showed attempts to support these scenarios caused 25-95 test regressions, indicating fundamental architectural incompatibilities.

**Documented Limits:**
- **Complex field access modifiers** (protected, internal, public fields with `[Inject]`)
- **Advanced generic constraints** (unmanaged, complex constraint combinations)
- **Deep configuration injection nesting** with inheritance + generics combined
- **Extreme attribute combinations** that don't occur in real-world usage

**Architecture Philosophy:**
These limits represent deliberate engineering trade-offs prioritizing:
1. **Stability**: 90% use case reliability over edge case complexity
2. **Maintainability**: Clean architecture over feature completeness
3. **Performance**: Fast compilation over theoretical capability coverage

**Real-World Impact:**
- **Comprehensive test coverage** across formal test suite and sample application
- **Intelligent service registration** handles 90%+ of real-world scenarios automatically
- **Workarounds available** for every limited scenario (manual constructors, `[DependsOn]` alternatives)
- **Zero impact** on standard business application patterns with enhanced performance

The generator excels at standard patterns while maintaining architectural integrity. Workarounds are available for edge cases through manual constructors and `[DependsOn]` alternatives.

## Source Generator Development Insights

### Key Learnings from Comprehensive Implementation

**1. Inheritance Hierarchy Analysis**
- **Implementation**: Use `INamedTypeSymbol.BaseType` to traverse inheritance chains recursively
- **Best Practice**: Track hierarchy levels for proper dependency ordering and constructor generation
- **Termination**: Handle `System.Object` as termination condition for traversal loops
- **Real-world Success**: Supports unlimited inheritance depth with proper base constructor calling

```csharp
// Proven pattern from IoCTools implementation
while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
{
    // Collect dependencies at current level with proper ordering
    CollectDependenciesAtLevel(currentType, level);
    currentType = currentType.BaseType;
    level++;
}
```

**2. Comprehensive Symbol Analysis**
- **Primary Access**: Use `SemanticModel.GetDeclaredSymbol()` for reliable symbol information
- **Safety First**: Always null-check symbols - null references are common in edge cases
- **Type Comparisons**: Use `SymbolEqualityComparer.Default` for LINQ operations and deduplication
- **Attribute Access**: Prefer `ISymbol.GetAttributes()` over syntax analysis for reliability
- **Performance**: Cache symbol lookups where possible for complex inheritance scenarios

**3. Advanced Generated File Management**
- **Debug Access**: Use `--property EmitCompilerGeneratedFiles=true --property CompilerGeneratedFilesOutputPath=generated`
- **Lifecycle**: Generated files are ephemeral - design debugging around this constraint
- **Build Integration**: Build errors reference generated paths but files may not persist
- **Testing Strategy**: Comprehensive sample applications provide better verification than isolated tests

**4. Sophisticated Constructor Generation**
- **Parameter Ordering**: Base dependencies first, then derived - critical for compilation success
- **Constructor Chaining**: `base(param1, param2)` syntax with exact parameter matching
- **Scope Limitation**: Field assignments only handle current class dependencies, never inherited ones
- **Naming Strategy**: Indexed variables (`d1`, `d2`, etc.) provide clarity and avoid conflicts
- **Inheritance Integration**: Generated constructors must properly call base constructors with correct parameters

**5. Advanced Dependency Management**
- **Deduplication Strategy**: Group by service type using `SymbolEqualityComparer.Default`
- **Level Prioritization**: Keep dependencies from the lowest inheritance level (closest to derived)
- **Collection Management**: Maintain separate collections for base vs. derived dependencies
- **Performance Optimization**: Efficient lookup patterns for complex inheritance scenarios
- **Diagnostic Integration**: Track dependency sources for comprehensive validation

**6. Comprehensive Testing and Validation**
- **Integration Testing**: Sample applications provide better coverage than isolated unit tests
- **Inheritance Testing**: Verify chains of 2, 3, 5+ levels with various complexity patterns
- **Runtime Verification**: Test both DI container resolution AND actual runtime behavior
- **Isolation Strategy**: Use simple mock implementations to avoid external dependencies
- **Diagnostic Testing**: Deliberately problematic examples verify diagnostic system behavior

**7. Enhanced Diagnostic System Architecture**
- **Comprehensive Coverage**: IOC001-IOC086 covering all common DI mistakes and architectural issues
- **Configurable MSBuild Severity**: Full integration allows per-project diagnostic customization with granular control
- **Cross-Assembly Awareness**: Validates dependencies across project boundaries with intelligent fix suggestions
- **Real-time Feedback**: Build-time validation catches issues before runtime with modern lifetime attribute references
- **Performance Impact**: Minimal build overhead despite comprehensive analysis and intelligent service detection

### Proven Architecture Patterns

**Full Inheritance Chain Support:**
```csharp
// Traverse inheritance hierarchy bottom-up (derived to base)
// Collect all dependencies with level tracking for proper ordering
// Generate constructors with proper base() calls and field assignments
// Support unlimited inheritance depth via iterative traversal with termination safety
```

**Comprehensive Diagnostic Integration:**
```csharp
// Analyze dependencies at build time for missing implementations
// Validate lifetime compatibility across inheritance chains
// Detect registration conflicts and duplicate dependencies
// Provide configurable severity levels for different diagnostic categories
```

**Advanced Configuration Binding:**
```csharp
// Automatic configuration section injection with validation
// Support for complex configuration models and collections
// Integration with .NET configuration system and dependency injection
// Proper scoping and lifetime management for configuration objects
```

### Development Lessons from Implementation

**Systematic Approach Success:**
- Delivered individual lifetime attributes (`[Scoped]`, `[Singleton]`, `[Transient]`) for modern, clean syntax
- Implemented intelligent service registration with automatic partial class detection
- Enhanced collection injection with intelligent `IEnumerable<T>` wrapper generation
- Performance profiling confirmed minimal build-time overhead despite comprehensive analysis

**Architectural Decision Validation:**
- Focus on 90% use cases delivered superior reliability over edge case support
- Diagnostic system integration provided significant developer productivity improvements

**Sample Application Strategy:**
- Expanded to 18 comprehensive example files showcasing all features
- Real-world scenario coverage validated practical applicability
- Formal test suite complements sample application for complete coverage

---

## Future Work

See `ideas.md` for a curated list of future improvements, diagnostics, and feature ideas.
