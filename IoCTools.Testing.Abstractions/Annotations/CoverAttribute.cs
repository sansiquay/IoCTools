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

    /// <summary>
    /// Controls how concrete (non-interface) constructor dependencies are materialized in the
    /// generated fixture. Default <see cref="ConcreteHandling.Auto"/> preserves the historical
    /// behavior of constructing a real instance with a Configure&lt;T&gt; helper. Set to
    /// <see cref="ConcreteHandling.ForceMock"/> to suppress the auto-concrete promotion path
    /// and emit Mock&lt;T&gt; substitutes for every non-special constructor parameter — useful
    /// when the SUT composes a concrete collaborator from port mocks and the test wants to
    /// preserve depth-2/3 mock coverage.
    /// </summary>
    public ConcreteHandling ConcreteHandling { get; set; } = ConcreteHandling.Auto;
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

/// <summary>
/// Controls how concrete (non-interface) constructor dependencies are handled in generated fixtures.
/// </summary>
public enum ConcreteHandling
{
    /// <summary>
    /// Default. Concrete classes with an accessible parameterless constructor are emitted as
    /// real instances with a Configure&lt;T&gt; helper; everything else is mocked.
    /// </summary>
    Auto,

    /// <summary>
    /// Opt out of the auto-concrete promotion: every non-special constructor parameter is
    /// emitted as a Mock&lt;T&gt;, even when it is a concrete class with a parameterless ctor.
    /// </summary>
    ForceMock,
}
