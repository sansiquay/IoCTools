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
    /// <remarks>
    /// <para>
    /// <b>Moq interception constraint:</b> <see cref="ConcreteHandling.ForceMock"/> emits
    /// <c>Mock&lt;TConcrete&gt;</c> for concrete-class dependencies, but Moq can only intercept
    /// <c>virtual</c> (or <c>abstract</c>) instance methods. If the concrete type's public
    /// methods are non-virtual (the default in C#), <c>Setup(...)</c> calls compile and silently
    /// no-op, the real method body executes with default/null backing fields, and the test
    /// typically NullReferenceExceptions at runtime. The generated fixture itself compiles
    /// regardless — the failure surfaces only when the test invokes the unintercepted method.
    /// </para>
    /// <para>
    /// <b>Recommendation:</b> use <c>ForceMock</c> when the concrete dependency either marks its
    /// public methods <c>virtual</c>, or is a POCO/record whose entire surface is properties
    /// (no behavior to intercept). For sealed-by-default service classes (e.g. IoCTools
    /// <c>[Scoped] partial class</c> shape with non-virtual public methods), prefer extracting
    /// an interface and consuming that — <see cref="ConcreteHandling.Auto"/> with an
    /// interface-typed dependency yields a working <c>Mock&lt;IDependency&gt;</c> without this caveat.
    /// </para>
    /// </remarks>
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
    /// <para>
    /// <b>Requires virtual methods on the concrete target type.</b> Moq can only intercept
    /// <c>virtual</c>/<c>abstract</c> instance methods on classes; <c>Setup(x =&gt; x.Method(...))</c>
    /// against a non-virtual public method compiles but no-ops, so the real implementation runs
    /// with default backing fields and typically NullReferenceExceptions at runtime. Fixture
    /// generation itself succeeds — the failure surfaces only when the test invokes the method.
    /// For sealed-by-default service classes, extract an interface and use
    /// <see cref="Auto"/> against the interface-typed dependency instead.
    /// </para>
    /// </summary>
    ForceMock,
}
