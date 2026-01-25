using IoCTools.Generator.Utilities;

namespace IoCTools.Generator.Tests;

/// <summary>
///     Tests for LifetimeCompatibilityChecker utility.
///     Tests centralized lifetime compatibility logic.
/// </summary>
public class LifetimeCompatibilityCheckerTests
{
    #region GetViolationType - Singleton Consumer

    [Fact]
    public void GetViolationType_SingletonScoped_ReturnsSingletonDependsOnScoped()
    {
        var result = LifetimeCompatibilityChecker.GetViolationType("Singleton", "Scoped");

        result.Should().Be(LifetimeViolationType.SingletonDependsOnScoped);
    }

    [Fact]
    public void GetViolationType_SingletonTransient_ReturnsSingletonDependsOnTransient()
    {
        var result = LifetimeCompatibilityChecker.GetViolationType("Singleton", "Transient");

        result.Should().Be(LifetimeViolationType.SingletonDependsOnTransient);
    }

    [Fact]
    public void GetViolationType_SingletonSingleton_ReturnsCompatible()
    {
        var result = LifetimeCompatibilityChecker.GetViolationType("Singleton", "Singleton");

        result.Should().Be(LifetimeViolationType.Compatible);
    }

    #endregion

    #region GetViolationType - Scoped Consumer

    [Fact]
    public void GetViolationType_ScopedSingleton_ReturnsCompatible()
    {
        var result = LifetimeCompatibilityChecker.GetViolationType("Scoped", "Singleton");

        result.Should().Be(LifetimeViolationType.Compatible);
    }

    [Fact]
    public void GetViolationType_ScopedScoped_ReturnsCompatible()
    {
        var result = LifetimeCompatibilityChecker.GetViolationType("Scoped", "Scoped");

        result.Should().Be(LifetimeViolationType.Compatible);
    }

    [Fact]
    public void GetViolationType_ScopedTransient_ReturnsCompatible()
    {
        var result = LifetimeCompatibilityChecker.GetViolationType("Scoped", "Transient");

        result.Should().Be(LifetimeViolationType.Compatible);
    }

    #endregion

    #region GetViolationType - Transient Consumer

    [Fact]
    public void GetViolationType_TransientSingleton_ReturnsCompatible()
    {
        var result = LifetimeCompatibilityChecker.GetViolationType("Transient", "Singleton");
        result.Should().Be(LifetimeViolationType.Compatible);
    }

    [Fact]
    public void GetViolationType_TransientScoped_ReturnsTransientDependsOnScoped()
    {
        var result = LifetimeCompatibilityChecker.GetViolationType("Transient", "Scoped");
        result.Should().Be(LifetimeViolationType.TransientDependsOnScoped);
    }

    [Fact]
    public void GetViolationType_TransientTransient_ReturnsCompatible()
    {
        var result = LifetimeCompatibilityChecker.GetViolationType("Transient", "Transient");
        result.Should().Be(LifetimeViolationType.Compatible);
    }

    #endregion

    #region GetViolationType - Null and Invalid Inputs

    [Fact]
    public void GetViolationType_NullConsumer_ReturnsCompatible()
    {
        var result = LifetimeCompatibilityChecker.GetViolationType(null, "Scoped");

        result.Should().Be(LifetimeViolationType.Compatible);
    }

    [Fact]
    public void GetViolationType_NullDependency_ReturnsCompatible()
    {
        var result = LifetimeCompatibilityChecker.GetViolationType("Singleton", null);

        result.Should().Be(LifetimeViolationType.Compatible);
    }

    [Fact]
    public void GetViolationType_InvalidLifetime_ReturnsCompatible()
    {
        var result = LifetimeCompatibilityChecker.GetViolationType("Invalid", "Scoped");

        result.Should().Be(LifetimeViolationType.Compatible);
    }

    #endregion

    #region ParseServiceLifetime

    [Fact]
    public void ParseServiceLifetime_Singleton_ReturnsSingleton()
    {
        var result = LifetimeCompatibilityChecker.ParseServiceLifetime("Singleton");

        result.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void ParseServiceLifetime_Scoped_ReturnsScoped()
    {
        var result = LifetimeCompatibilityChecker.ParseServiceLifetime("Scoped");

        result.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void ParseServiceLifetime_Transient_ReturnsTransient()
    {
        var result = LifetimeCompatibilityChecker.ParseServiceLifetime("Transient");

        result.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void ParseServiceLifetime_Null_ReturnsNull()
    {
        var result = LifetimeCompatibilityChecker.ParseServiceLifetime(null);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseServiceLifetime_Invalid_ReturnsNull()
    {
        var result = LifetimeCompatibilityChecker.ParseServiceLifetime("Invalid");

        result.Should().BeNull();
    }

    #endregion
}
