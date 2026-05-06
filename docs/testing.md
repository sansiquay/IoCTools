# Testing with IoCTools

The [IoCTools.Testing](https://www.nuget.org/packages/IoCTools.Testing) package auto-generates test fixtures, eliminating mock declaration and SUT construction boilerplate.

## Overview

IoCTools.Testing generates test fixture base classes that provide:

- **Mock fields** — `Mock<T>` for interface and abstract constructor dependencies
- **Concrete dependency helpers** — Real instances for public parameterless concrete dependencies
- **Lazy SUT property** — `Sut` for the common single-instance test path
- **Factory method** — `CreateSut()` when a test needs explicit construction
- **Setup helpers** — Typed methods for configuring mocks: `SetupUserRepository(Action<Mock<IUserRepository>>)`
- **Configuration helpers** — For services using `[DependsOnConfiguration]` or compatibility-only `InjectConfiguration`

No more manual `new Mock<T>()` declarations, context object defaults, or `new Service(mock.Object, ...)` constructors.

Authoring rule for 1.6.0+: `[Inject]` is deprecated (fires `IOC095`; removed in 2.0). Use `[DependsOn]`, `[DependsOnConfiguration]`, and `[DependsOnOptions]`. `IoCTools.Testing` is itself migrated off `[Inject]`.

## Installation

Add to your **test project only**:

```bash
dotnet add package IoCTools.Testing
```

Or in your test project `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="IoCTools.Testing" Version="1.7.2" />
</ItemGroup>
```

**Note:** IoCTools.Testing does not add Moq as a transitive dependency to your production project.

## Quick Example

### Before (manual mocks)

```csharp
public class UserServiceTests
{
    private readonly Mock<IUserRepository> _mockRepo = new();
    private readonly Mock<ILogger<UserService>> _mockLogger = new();

    public UserServiceTests()
    {
        // Manual mock setup
        _mockRepo.Setup(r => r.GetById(1)).Returns(new User(1, "Test"));
    }

    [Fact]
    public void GetById_ShouldReturnUser()
    {
        // Manual SUT construction
        var sut = new UserService(_mockRepo.Object, _mockLogger.Object);
        var result = sut.GetById(1);
        result.Should().NotBeNull();
    }
}
```

### After (auto-generated fixture)

```csharp
using IoCTools.Testing.Annotations;

[Cover<UserService>]
public partial class UserServiceTests
{
    [Fact]
    public void GetById_ShouldReturnUser()
    {
        // Use generated setup helper
        SetupUserRepository(m => m
            .Setup(r => r.GetById(1))
            .Returns(new User(1, "Test")));

        // Act
        var result = Sut.GetById(1);

        // Assert
        result.Should().NotBeNull();
    }
}
```

**What was generated:**
- `private readonly Mock<IUserRepository> _mockUserRepository = new();`
- `private readonly Mock<ILogger<UserService>> _mockLogger = new();`
- `private Mock<IUserRepository> UserRepositoryMock => _mockUserRepository;` (accessor property)
- `private Mock<ILogger<UserService>> LoggerMock => _mockLogger;` (accessor property)
- `private UserService? _sut;`
- `private UserService Sut => _sut ??= CreateSut();` (lazy property)
- `public UserService CreateSut() => new(_mockUserRepository.Object, _mockLogger.Object);`
- `private void SetupUserRepository(Action<Mock<IUserRepository>> configure) => configure(_mockUserRepository);`
- `private void SetupLogger(Action<Mock<ILogger<UserService>>> configure) => configure(_mockLogger);`

Both `Sut` (lazy) and `CreateSut()` (explicit) are available. Use `Sut` for common single-instance tests; use `CreateSut()` when you need multiple instances.

---

## Generated Members

### Mock Fields

For each dependency `T`, a private mock field is generated into the same
partial test class:

```csharp
private readonly Mock<T> _mock{SanitizedName} = new();
```

Access these fields directly in your partial test class for custom setups.
The underscore-prefix fields remain for backward compatibility with existing
arrange code; the accessor properties below are preferred for new tests.

### Mock Accessor Properties (preferred API)

For each mock dependency, a readable accessor property wraps the field:

```csharp
private Mock<T> {SanitizedName}Mock => _mock{SanitizedName};
```

This is the preferred API for accessing mock members — no underscore prefix, IDE friendly.

**Examples:**
- `IUserRepository` → `UserRepositoryMock`
- `ILogger<UserService>` → `LoggerUserServiceMock`
- `IRepository<Customer>` → `RepositoryCustomerMock`

```csharp
UserRepositoryMock.Setup(r => r.GetById(1)).Returns(user);
```

### Lazy Sut Property

A lazy-cached `Sut` property avoids explicit `CreateSut()` calls for the common single-instance case:

```csharp
private ServiceType? _sut;
private ServiceType Sut => _sut ??= CreateSut();
```

`Sut` is **lazy** — it calls `CreateSut()` on first access and caches the instance for the test
class lifetime. Set up all mocks before the first access.

```csharp
SetupUserRepository(m => m.Setup(r => r.GetById(1)).Returns(user));
var result = Sut.GetById(1);  // CreateSut() called here, once
```

> **Warning:** Calling fixture helpers (Setup*, Configure*, Use*) after accessing `Sut` has no
effect — [TDIAG07](diagnostics.md#tdiag07) warns about this.

### CreateSut() Factory

Explicit factory for multi-instance scenarios or deferred construction:

```csharp
public {ServiceName} CreateSut() => new({allMockObjects});
```

**Inheritance support:** For services with base class dependencies, `CreateSut()` includes all dependencies from the entire inheritance chain.

### Setup Helpers

Typed setup methods for each dependency:

```csharp
private void Setup{SanitizedName}(Action<Mock<T>> configure) => configure(_mock{T});
```

**Benefits:**
- IDE auto-completion shows all available helpers
- Type-safe mock configuration
- Cleaner test code

```csharp
SetupUserRepository(m => m.Setup(r => r.GetById(1)).Returns(user));
SetupLogger(m => m.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()), Times.Once));
```

### Concrete Dependency Helpers

Concrete class dependencies with a public parameterless constructor are generated as real instances,
not mocks. This fits request/context objects that are test state, not external collaborators.

```csharp
private RequestContext RequestContext { get; private set; } = new();
private RequestContext UseRequestContext(RequestContext value)
private RequestContext ConfigureRequestContext(Action<RequestContext> configure)
```

**Usage:**
```csharp
ConfigureRequestContext(c =>
{
    c.TenantId = "tenant-1";
    c.UserId = "user-42";
});

var result = Sut.Handle(command);
```

Concrete dependencies without a public parameterless constructor are not auto-instantiated by the
fixture generator. Keep those explicit or model them as IoCTools-managed dependencies first.

### Options Helpers

For `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>` dependencies, two helpers are generated.
Both helpers **return the configured value** for inline assertion or further configuration:

```csharp
// Set the value directly — returns it
private TOptions Use{OptionsName}(TOptions value)

// Configure via action — returns configured instance
private TOptions Configure{OptionsName}(Action<TOptions> configure)
```

For `IOptionsSnapshot<T>`, a named variant is also generated:

```csharp
private TOptions Configure{OptionsName}(string name, Action<TOptions> configure)
```

For `IOptionsMonitor<T>`, the configure helper sets up both `CurrentValue` and `Get(It.IsAny<string>())`.

**Usage:**
```csharp
var opts = ConfigureDbOptions(o => o.ConnectionString = "Server=local;");
opts.ConnectionString.Should().Be("Server=local;");
```

### Configuration Helper

For `IConfiguration` dependencies, two overloads are generated:

```csharp
// Function-based value provider (extensibility)
private void ConfigureIConfiguration(Func<string, object?> valueProvider)
private void ConfigureConfiguration(Func<string, object?> valueProvider)

// Tuple-based convenience
private void ConfigureIConfiguration(params (string Key, object? Value)[] values)
private void ConfigureConfiguration(params (string Key, object? Value)[] values)
```

**Tuple usage:**
```csharp
ConfigureConfiguration(
    ("Database:ConnectionString", "Server=localhost;"),
    ("Feature:Enabled", true)
);
```

**Function usage:**
```csharp
ConfigureConfiguration(key => key switch
{
    "Database:ConnectionString" => "Server=localhost;",
    _ => throw new ArgumentException($"Unexpected key: {key}")
});
```

### Time Provider

`System.TimeProvider` dependencies receive a real `TimeProvider.System` instance by default (not a mock).
Use `UseTimeProvider()` to override:

```csharp
private TimeProvider TimeProvider { get; set; } = System.TimeProvider.System;
private void UseTimeProvider(TimeProvider timeProvider) => TimeProvider = timeProvider;
```

**Usage:**
```csharp
var frozenTime = new FrozenTimeProvider();
UseTimeProvider(frozenTime);
var result = Sut.GetCurrentTime();  // returns frozenTime value
```

### Logger Profile

By default, `ILogger<T>` dependencies get `Mock<ILogger<T>>` fields with typed setup helpers.
An optional `NullLogger<T>` profile is available for teams that do not assert log calls.
The CLI scaffold emits `[Cover<T>(Logger = FixtureLoggerProfile.NullLogger)]` by default because scaffolded smoke tests should not assert log calls. Use the default `[Cover<T>]` form when you want Moq logger setup helpers.

---

## Advanced Scenarios

### Inheritance Hierarchies

For services with base class dependencies, the fixture includes **all** dependencies from the entire chain:

```csharp
// Base class
[Scoped]
[DependsOn<IAppConfiguration, ILogger<RepositoryBase>>]
public partial class RepositoryBase
{
}

// Derived service
[Scoped]
[DependsOn<ISampleCacheService>]
public partial class UserRepository : RepositoryBase, IUserRepository
{
}

// Test fixture
[Cover<UserRepository>]
public partial class UserRepositoryTests
{
    [Fact]
    public void Test()
    {
        // Base class deps available
        ConfigureConfiguration(("Key", "value"));
        SetupLogger(m => m.Verify(/* ... */));

        // Derived deps available
        SetupSampleCache(m => m.Setup(c => m.Get<string>("key")).Returns("cached"));

        var sut = CreateSut(); // Wires all 4 dependencies
    }
}
```

**Generated fields:**
- `_mockAppConfiguration` (base)
- `_mockBaseLogger` (base)
- `_mockSampleCacheService` (derived)
- `_mockLogger` (derived)

### Configuration Injection

For services using `[DependsOnConfiguration]`, a configuration helper is generated:

```csharp
[Scoped]
[DependsOn<ILogger<DatabaseService>>]
[DependsOnConfiguration<string>("Database:ConnectionString", Required = true)]
public partial class DatabaseService : IDatabaseService
{
}
```

**Generated fixture:**
```csharp
[Cover<DatabaseService>]
public partial class DatabaseServiceTests
{
    // Mock fields
    private readonly Mock<ILogger<DatabaseService>> _mockLogger = new();
    private readonly Mock<IConfiguration> _mockConfiguration = new();

    // Configuration helper
    private void ConfigureConfiguration(Func<string, object?> valueProvider)
    {
        // Implementation provides values to _mockConfiguration
    }

    // Factory
    public DatabaseService CreateSut() { /* ... */ }
}
```

**Usage:**
```csharp
[Fact]
public void Test()
{
    ConfigureConfiguration(key => key switch
    {
        "Database:ConnectionString" => "Server=localhost;",
        _ => throw new ArgumentException($"Unexpected key: {key}")
    });

    var result = Sut.Load();
}
```

---

## Test Diagnostics

IoCTools.Testing includes analyzer diagnostics to suggest fixture usage:

| Diagnostic | Cause | Fix |
|-----------|-------|-----|
| [TDIAG01](diagnostics.md#tdiag01) | Manual `Mock<T>` field with `[Cover<T>]` | Remove manual mock, use generated field |
| [TDIAG02](diagnostics.md#tdiag02) | Manual SUT construction with `CreateSut()` available | Use `CreateSut()` instead |
| [TDIAG03](diagnostics.md#tdiag03) | Mock fields match service dependencies | Add `[Cover<T>]` to test class |
| [TDIAG04](diagnostics.md#tdiag04) | Service in `[Cover<T>]` has no generated constructor | Make service partial with lifetime/dependencies |
| [TDIAG05](diagnostics.md#tdiag05) | Test class with `[Cover<T>]` is not partial | Add `partial` modifier to test class |
| [TDIAG06](diagnostics.md#tdiag06) | Generated fixture member names collide | Rename dependencies or add explicit test fixture setup |
| [TDIAG07](diagnostics.md#tdiag07) | Fixture helper called after Sut access | Rearrange Arrange phase: helpers before Sut |
| [TDIAG08](diagnostics.md#tdiag08) | Manual construction of IoCTools-managed service | Add `[Cover<T>]` to test class |

---

## Requirements

### Test Class Requirements

1. **Mark as `partial`** — Required for fixture generation ([TDIAG05](diagnostics.md#tdiag05))
2. **Add `[Cover<T>]`** — Specifies which service to generate a fixture for
3. **Use test framework** — Works with xUnit, NUnit, MSTest

### Service Requirements

The service in `[Cover<T>]` must:

1. **Be `partial`** — Required for constructor generation
2. **Have service intent** — Lifetime attribute, `[DependsOn]` attributes, or existing compatibility markers already present in the service

If requirements aren't met, [TDIAG04](diagnostics.md#tdiag04) is raised.

---

## Limitations

- **Constructor pattern:** Fixture generation works with IoCTools-generated constructors. Manual constructors are not supported.
- **Test project only:** IoCTools.Testing must be referenced by test projects, not production code.
- **Moq dependency:** Test projects must reference Moq 4.20.72 or higher (bring your own).

---

## CLI Scaffold

The `ioc-tools test scaffold` command generates a partial test class with `[Cover<T>]` and a smoke test:

```bash
ioc-tools test scaffold --project src/MyApp/MyApp.csproj --type MyApp.Services.UserService --dry-run
```

**Options:**
- `--project <csproj>` — Production project (required)
- `--test-project <csproj>` — Test project used for namespace/output inference
- `--type <typename>` — Fully-qualified service type name (required)
- `--test-framework xunit|nunit|mstest` or `--framework xunit|nunit|mstest` — Test framework (default: xunit)
- `--mocking moq` — Mocking framework (default: moq)
- `--assertions fluentassertions|shouldly|none` — Assertion style (default: none)
- `--output <path>` — Output file or directory
- `--dry-run` — Preview without writing
- `--json` — Emit scaffold metadata and warnings in JSON mode
- `--force` — Overwrite existing files

**Generated scaffold:**
```csharp
using IoCTools.Testing.Annotations;
using Xunit;

namespace MyApp.Services.Tests;

[Cover<MyApp.Services.UserService>(Logger = FixtureLoggerProfile.NullLogger)]
public partial class UserServiceTests
{
    [Fact]
    public void Sut_ShouldConstruct()
    {
        Assert.NotNull(Sut);
    }
}
```

## CLI Fixture Evidence

Use `ioc-tools evidence --test-fixtures` against a test project to find
manual service-test setup that can move to `[Cover<T>]`:

```bash
ioc-tools evidence \
  --project tests/MyApp.Tests/MyApp.Tests.csproj \
  --production-project src/MyApp/MyApp.csproj \
  --test-fixtures \
  --json
```

Evidence mode detects manual `Mock<T>` fields matching constructor
dependencies, manual `new Service(...)` and `CreateSut() => new Service(...)`
helpers, logger/null-logger wiring, `Options.Create(...)` patterns, and
test classes that already use `[Cover<T>]` but still carry duplicate setup.
It classifies each candidate as safe migration, partial migration, already
covered, not a target, or unknown/manual review. It does not generate or
recommend business assertions.

---

## Complete Example

```csharp
// Production code
using IoCTools.Abstractions.Annotations;

[Scoped]
[DependsOn<IUserRepository, IEmailService, ILogger<UserService>>]
public partial class UserService : IUserService
{
    public User? GetById(int id) => _userRepository.GetById(id);
}

// Test code
using IoCTools.Testing.Annotations;
using Xunit;

[Cover<UserService>]
public partial class UserServiceTests
{
    [Fact]
    public void GetById_ReturnsUser_FromRepository()
    {
        // Arrange
        var expectedUser = new User(1, "Alice");
        SetupUserRepository(m => m
            .Setup(r => r.GetById(1))
            .Returns(expectedUser));

        // Act
        var result = Sut.GetById(1);

        // Assert
        result.Should().Be(expectedUser);
        UserRepositoryMock.Verify(m => m.GetById(1), Times.Once);
    }

    [Fact]
    public void Constructor_ShouldWire_AllDependencies()
    {
        // Act
        var sut = CreateSut();

        // Assert
        sut.Should().NotBeNull();
    }
}
```

---

## FluentValidation Helpers

When your test project references FluentValidation, IoCTools.Testing auto-generates validation setup helpers for any `IValidator<T>` constructor parameter. No additional configuration needed — detection is automatic.

### Generated Methods

For each `IValidator<T>` parameter in the service under test, two helpers are generated:

| Method | Description |
|--------|-------------|
| `Setup{ParamName}ValidationSuccess()` | Configures both `Validate()` and `ValidateAsync()` to return an empty `ValidationResult` |
| `Setup{ParamName}ValidationFailure(params string[] errorMessages)` | Configures both sync and async to return a `ValidationResult` with the specified failures |

### Example

```csharp
// Production code
[Scoped]
[DependsOn<IValidator<Order>, IOrderRepository>]
public partial class OrderHandler
{
    public async Task Handle(Order order)
    {
        var result = await _validator.ValidateAsync(order);
        if (!result.IsValid) throw new ValidationException(result.Errors);
        await _orderRepository.Save(order);
    }
}

// Test code
[Cover<OrderHandler>]
public partial class OrderHandlerTests
{
    [Fact]
    public async Task Handle_ValidOrder_Saves()
    {
        // Generated helper -- sets up both Validate() and ValidateAsync()
        SetupValidatorValidationSuccess();

        var sut = CreateSut();
        await sut.Handle(new Order());

        // Verify save was called
    }

    [Fact]
    public async Task Handle_InvalidOrder_Throws()
    {
        // Generated helper with custom error messages
        SetupValidatorValidationFailure("Name is required", "Amount must be positive");

        var sut = CreateSut();
        await Assert.ThrowsAsync<ValidationException>(() => sut.Handle(new Order()));
    }
}
```

### Requirements

- Test project must reference FluentValidation (helpers are only generated when FluentValidation is in compilation references)
- Service under test must have an `IValidator<T>` constructor parameter (declare via `[DependsOn<IValidator<T>>]`; `[Inject]` is deprecated in 1.6.0)
- Both `Validate()` and `ValidateAsync()` are mocked together — no need to set up each separately

---

## Related

- [Getting Started](getting-started.md) — IoCTools introduction
- [Attribute Reference](attributes.md) — All IoCTools attributes
- [Diagnostics Reference](diagnostics.md) — All diagnostics including TDIAG01 through TDIAG08
- [FluentValidation Diagnostics](diagnostics.md#fluentvalidation-diagnostics) — IOC100-IOC102
- [CLI Validator Commands](cli-reference.md#validators) — Inspect validators from command line

---

**Need help?** Check the [sample project](https://github.com/sansiquay/IoCTools/tree/main/IoCTools.Sample) for complete working examples.
