namespace IoCTools.FluentValidation;

using Generator;
using Generator.Pipeline;

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
        var validators = ValidatorPipeline.Build(context);
        var combined = validators.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (ctx, data) =>
        {
            var (validatorArray, compilation) = data;
            ValidatorRegistrationEmitter.Emit(ctx, validatorArray, compilation);
        });

        // Wire diagnostic validators for composition anti-pattern detection
        ValidatorDiagnosticsPipeline.Attach(context, validators);
    }
}
