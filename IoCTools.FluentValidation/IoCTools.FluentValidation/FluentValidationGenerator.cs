namespace IoCTools.FluentValidation;

using Microsoft.CodeAnalysis;

/// <summary>
/// Source generator for IoCTools FluentValidation integration.
/// Discovers validators extending AbstractValidator&lt;T&gt;, refines registrations,
/// and detects anti-patterns in validator composition.
/// </summary>
[Generator]
public sealed class FluentValidationGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the generator pipeline.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // TODO: Wire pipeline stages - validator discovery, registration refinement, diagnostics
    }
}
