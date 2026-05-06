namespace IoCTools.Testing.Annotations;

using System;

/// <summary>
/// Marks a test class to receive auto-generated test fixture members for the specified service.
/// The test class must be partial. Generated members include:
/// - Mock&lt;T&gt; fields for each constructor dependency
/// - A lazy Sut property for the common single-instance test path
/// - CreateSut() factory method wiring all mocks
/// - Typed Setup{Dependency}(Action&lt;Mock&lt;T&gt;&gt;) helper methods
/// - Configuration, options, concrete-instance, time-provider, and FluentValidation helpers where applicable
/// </summary>
/// <typeparam name="TService">The service class whose constructor signature defines the fixture dependencies</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CoverAttribute<TService> : Attribute
    where TService : class
{
    /// <summary>
    /// Controls generated fixture handling for ILogger&lt;T&gt; constructor dependencies.
    /// </summary>
    public FixtureLoggerProfile Logger { get; set; } = FixtureLoggerProfile.Mock;
}

/// <summary>
/// Logger handling profile for generated test fixtures.
/// </summary>
public enum FixtureLoggerProfile
{
    /// <summary>Generate Mock&lt;ILogger&lt;T&gt;&gt; fields and setup helpers.</summary>
    Mock,

    /// <summary>Generate NullLogger&lt;T&gt;.Instance fields without mock setup helpers.</summary>
    NullLogger,
}
