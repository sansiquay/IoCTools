namespace IoCTools.Sample;

using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Demonstrates IoCTools.Testing fixture generation.
/// NOTE: These examples show the structure of test fixture usage.
/// In a real test project with IoCTools.Testing package referenced,
/// the [Cover&lt;T&gt;] attribute would auto-generate Mock&lt;T&gt; fields,
/// CreateSut() factories, and typed setup helpers.
///
/// To use these examples:
/// 1. Create a test project (e.g., MyProject.Tests)
/// 2. Add package reference: &lt;PackageReference Include="IoCTools.Testing" Version="1.5.0" /&gt;
/// 3. Copy the service and test class patterns below
/// 4. The fixture generator will automatically generate the supporting members
/// </summary>

/*
#region Basic Service Fixture Example

[Scoped]
public partial class SampleUserService
{
    [Inject] private readonly ISampleUserRepository _userRepository;
    [Inject] private readonly ILogger<SampleUserService> _logger;

    public SampleUser? GetById(int id) => _userRepository.GetById(id);
}

public interface ISampleUserRepository
{
    SampleUser? GetById(int id);
}

public record SampleUser(int Id, string Name);

// In a test project with IoCTools.Testing package:
// The [Cover<SampleUserService>] attribute generates:
// - protected readonly Mock<ISampleUserRepository> _mockSampleUserRepository = new();
// - protected readonly Mock<ILogger<SampleUserService>> _mockLogger = new();
// - public SampleUserService CreateSut() => new(_mockSampleUserRepository.Object, _mockLogger.Object);
// - protected void SetupSampleUserRepository(Action<Mock<ISampleUserRepository>> configure) => configure(_mockSampleUserRepository);
// - protected void SetupLogger(Action<Mock<ILogger<SampleUserService>>> configure) => configure(_mockLogger);

[Cover<SampleUserService>]
public partial class SampleUserServiceTests
{
    [Fact]
    public void GetById_ShouldReturnUser_FromRepository()
    {
        // Arrange - use generated helper
        SetupSampleUserRepository(m => m
            .Setup(r => r.GetById(1))
            .Returns(new SampleUser(1, "Test User")));

        // Act - use generated factory
        var sut = CreateSut();

        // Assert
        var result = sut.GetById(1);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test User");
    }
}

#endregion

#region Inheritance Fixture Example

[Scoped]
public partial class BaseRepositoryService
{
    [Inject] protected readonly IAppConfiguration _configuration;
    [Inject] protected readonly ILogger<BaseRepositoryService> _logger;
}

public interface IAppConfiguration { string? GetValue(string key); }

[Scoped]
public partial class UserRepositoryService : BaseRepositoryService
{
    [Inject] private readonly ISampleCacheService _cache;

    public string? GetCachedUser(string key)
    {
        _configuration.GetValue("ConnectionString");
        return _cache.Get<string>(key);
    }
}

public interface ISampleCacheService { T? Get<T>(string key); }

// The fixture generator includes ALL dependencies from base and derived classes:
// - protected readonly Mock<IAppConfiguration> _mockAppConfiguration = new();
// - protected readonly Mock<ILogger<BaseRepositoryService>> _mockBaseLogger = new();
// - protected readonly Mock<ISampleCacheService> _mockSampleCacheService = new();
// - protected readonly Mock<ILogger<UserRepositoryService>> _mockDerivedLogger = new();

[Cover<UserRepositoryService>]
public partial class UserRepositoryServiceTests
{
    [Fact]
    public void Constructor_ShouldWire_AllDependencies()
    {
        // Arrange - setup all dependencies including base class deps
        SetupAppConfiguration(m => m
            .Setup(c => c.GetValue("ConnectionString"))
            .Returns("TestConnection"));

        SetupSampleCache(m => m
            .Setup(c => c.Get<string>("user:1"))
            .Returns("cached"));

        // Act - CreateSut wires base and derived dependencies
        var sut = CreateSut();

        // Assert
        sut.Should().NotBeNull();
    }

    [Fact]
    public void GetCachedUser_ShouldUseCache()
    {
        // Arrange
        SetupSampleCache(m => m
            .Setup(c => c.Get<string>("user:1"))
            .Returns("cached_user"));

        var sut = CreateSut();

        // Act
        var result = sut.GetCachedUser("user:1");

        // Assert
        result.Should().Be("cached_user");
    }
}

#endregion

#region Configuration Injection Fixture Example

[Scoped]
public partial class ConfigurableDatabaseService
{
    [InjectConfiguration("Database:ConnectionString")]
    private readonly string _connectionString;

    [Inject] private readonly ILogger<ConfigurableDatabaseService> _logger;

    public string GetConnectionInfo() => $"Connection: {_connectionString}";
}

// For services with [InjectConfiguration], the fixture generates:
// - protected readonly Mock<ILogger<ConfigurableDatabaseService>> _mockLogger = new();
// - protected readonly Mock<Microsoft.Extensions.Configuration.IConfiguration> _mockConfiguration = new();
// - protected void ConfigureIConfiguration(Func<string, object?> valueProvider) { ... }

[Cover<ConfigurableDatabaseService>]
public partial class ConfigurableDatabaseServiceTests
{
    [Fact]
    public void GetConnectionInfo_ShouldUse_ConfiguredConnectionString()
    {
        // Arrange - use generated configuration helper
        ConfigureIConfiguration(key => key switch
        {
            "Database:ConnectionString" => "Server=localhost;Database=test;",
            _ => throw new ArgumentException($"Unexpected key: {key}")
        });

        // Act
        var sut = CreateSut();
        var result = sut.GetConnectionInfo();

        // Assert
        result.Should().Contain("Server=localhost");
    }
}

#endregion
*/
