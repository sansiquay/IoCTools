# Testing with IoCTools

The [IoCTools.Testing](https://www.nuget.org/packages/IoCTools.Testing) package auto-generates test fixtures, eliminating mock declaration and SUT construction boilerplate.

## Overview

IoCTools.Testing generates test fixture base classes that provide:

- **Mock fields** — `Mock<T>` for all constructor dependencies
- **Factory method** — `CreateSut()` to construct the system under test
- **Setup helpers** — Typed methods for configuring mocks: `SetupUserRepository(Action<Mock<IUserRepository>>)`
- **Configuration helpers** — For services using `[DependsOnConfiguration]` or compatibility-only `InjectConfiguration`

No more manual `new Mock<T>()` declarations or `new Service(mock.Object, ...)` constructors.

Authoring rule for `1.5.1`: never introduce new `[Inject]` or `InjectConfiguration` usage in services just to satisfy testing. Prefer `[DependsOn]`, `[DependsOnConfiguration]`, and `[DependsOnOptions]`.

## Installation

Add to your **test project only**:

```bash
dotnet add package IoCTools.Testing
```

Or in your test project `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="IoCTools.Testing" Version="1.5.1" />
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

        // Use generated factory
        var sut = CreateSut();

        // Act
        var result = sut.GetById(1);

        // Assert
        result.Should().NotBeNull();
    }
}
```

**What was generated:**
- `protected readonly Mock<IUserRepository> _mockUserRepository = new();`
- `protected readonly Mock<ILogger<UserService>> _mockLogger = new();`
- `public UserService CreateSut() => new(_mockUserRepository.Object, _mockLogger.Object);`
- `protected void SetupUserRepository(Action<Mock<IUserRepository>> configure) => configure(_mockUserRepository);`
- `protected void SetupLogger(Action<Mock<ILogger<UserService>>> configure) => configure(_mockLogger);`

---

## Generated Members

### Mock Fields

For each dependency `T`, a protected mock field is generated:

```csharp
protected readonly Mock<T> _mock{SanitizedName} = new();
```

Field naming strips `I` prefix and applies camelCase:
- `IUserRepository` → `_mockUserRepository`
- `ILogger<T>` → `_mockLogger`
- `IDetailedInvoiceAuditor` → `_mockDetailedInvoiceAuditor`

Access these fields directly in your tests for custom setups.

### CreateSut() Factory

Constructs the system under test with all mock `.Object` values:

```csharp
public {ServiceName} CreateSut() => new({allMockObjects});
```

**Inheritance support:** For services with base class dependencies, `CreateSut()` includes all dependencies from the entire inheritance chain.

### Setup Helpers

Typed setup methods for each dependency:

```csharp
protected void Setup{SanitizedName}(Action<Mock<T>> configure) => configure(_mock{T});
```

**Benefits:**
- IDE auto-completion shows all available helpers
- Type-safe mock configuration
- Cleaner test code

```csharp
SetupUserRepository(m => m.Setup(r => r.GetById(1)).Returns(user));
SetupLogger(m => m.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<object>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()), Times.Once));
```

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
        SetupAppConfiguration(m => m.Setup(c => c.GetValue("Key")).Returns("value"));
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
    protected readonly Mock<ILogger<DatabaseService>> _mockLogger = new();
    protected readonly Mock<IConfiguration> _mockConfiguration = new();

    // Configuration helper
    protected void ConfigureIConfiguration(Func<string, object?> valueProvider)
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
    ConfigureIConfiguration(key => key switch
    {
        "Database:ConnectionString" => "Server=localhost;",
        _ => throw new ArgumentException($"Unexpected key: {key}")
    });

    var sut = CreateSut();
    // ...
}
```

---

## Test Diagnostics

IoCTools.Testing includes analyzer diagnostics to suggest fixture usage:

| Diagnostic | Cause | Fix |
|-----------|-------|-----|
| [TDIAG-01](diagnostics.md#tdiag-01) | Manual `Mock<T>` field with `[Cover<T>]` | Remove manual mock, use generated field |
| [TDIAG-02](diagnostics.md#tdiag-02) | Manual SUT construction with `CreateSut()` available | Use `CreateSut()` instead |
| [TDIAG-03](diagnostics.md#tdiag-03) | Mock fields match service dependencies | Add `[Cover<T>]` to test class |
| [TDIAG-04](diagnostics.md#tdiag-04) | Service in `[Cover<T>]` has no generated constructor | Make service partial with lifetime/dependencies |
| [TDIAG-05](diagnostics.md#tdiag-05) | Test class with `[Cover<T>]` is not partial | Add `partial` modifier to test class |

---

## Requirements

### Test Class Requirements

1. **Mark as `partial`** — Required for fixture generation ([TDIAG-05](diagnostics.md#tdiag-05))
2. **Add `[Cover<T>]`** — Specifies which service to generate a fixture for
3. **Use test framework** — Works with xUnit, NUnit, MSTest

### Service Requirements

The service in `[Cover<T>]` must:

1. **Be `partial`** — Required for constructor generation
2. **Have service intent** — Lifetime attribute, `[DependsOn]` attributes, or existing compatibility markers already present in the service

If requirements aren't met, [TDIAG-04](diagnostics.md#tdiag-04) is raised.

---

## Limitations

- **Constructor pattern:** Fixture generation works with IoCTools-generated constructors. Manual constructors are not supported.
- **Test project only:** IoCTools.Testing must be referenced by test projects, not production code.
- **Moq dependency:** Test projects must reference Moq 4.20.72 or higher (bring your own).

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
        var sut = CreateSut();
        var result = sut.GetById(1);

        // Assert
        result.Should().Be(expectedUser);
        SetupUserRepository.Verify(m => m.GetById(1), Times.Once);
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
- Service under test must have an `IValidator<T>` constructor parameter (prefer `[DependsOn]`; existing `[Inject]` remains compatible)
- Both `Validate()` and `ValidateAsync()` are mocked together — no need to set up each separately

---

## Related

- [Getting Started](getting-started.md) — IoCTools introduction
- [Attribute Reference](attributes.md) — All IoCTools attributes
- [Diagnostics Reference](diagnostics.md) — All diagnostics including TDIAG-01 through TDIAG-05
- [FluentValidation Diagnostics](diagnostics.md#fluentvalidation-diagnostics) — IOC100-IOC102
- [CLI Validator Commands](cli-reference.md#validators) — Inspect validators from command line

---

**Need help?** Check the [sample project](https://github.com/nathan-p-lane/IoCTools/tree/main/IoCTools.Sample) for complete working examples.
