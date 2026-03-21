namespace IoCTools.Testing.Annotations;

using System;

/// <summary>
/// Marks a test class to receive auto-generated test fixture members for the specified service.
/// The test class must be partial. Generated members include:
/// - Mock&lt;T&gt; fields for each constructor dependency
/// - CreateSut() factory method wiring all mocks
/// - Typed Setup{Dependency}(Action&lt;Mock&lt;T&gt;&gt;) helper methods
/// </summary>
/// <typeparam name="TService">The service class whose constructor signature defines the fixture dependencies</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CoverAttribute<TService> : Attribute
    where TService : class
{
}
