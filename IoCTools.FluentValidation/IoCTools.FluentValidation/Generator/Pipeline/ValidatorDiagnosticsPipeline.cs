namespace IoCTools.FluentValidation.Generator.Pipeline;

using System;
using System.Collections.Immutable;

using Diagnostics.Validators;

using Microsoft.CodeAnalysis;

using Models;

/// <summary>
/// Attaches diagnostic validation to the incremental generator pipeline.
/// Runs DirectInstantiationValidator and CompositionLifetimeValidator against
/// all discovered validators.
/// </summary>
internal static class ValidatorDiagnosticsPipeline
{
    /// <summary>
    /// Attaches the diagnostics pipeline to the generator context.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    /// <param name="validators">Collected provider of all discovered validators.</param>
    internal static void Attach(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableArray<ValidatorClassInfo>> validators)
    {
        context.RegisterSourceOutput(validators, static (ctx, allValidators) =>
        {
            foreach (var validator in allValidators)
            {
                try
                {
                    DirectInstantiationValidator.Validate(validator, allValidators, ctx.ReportDiagnostic);
                    CompositionLifetimeValidator.Validate(validator, allValidators, ctx.ReportDiagnostic);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
                {
                    // Generator never throws — silently skip failed validators
                }
            }
        });
    }
}
