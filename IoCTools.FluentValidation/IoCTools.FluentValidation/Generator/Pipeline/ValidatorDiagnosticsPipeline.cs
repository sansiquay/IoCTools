namespace IoCTools.FluentValidation.Generator.Pipeline;

using System;
using System.Collections.Immutable;

using Diagnostics;
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
                // IOC111: composition graph build caught an exception in the SyntaxProvider phase
                // (where no ReportDiagnostic sink is available). Surface it here.
                if (validator.GraphBuildError != null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        FluentValidationDiagnosticDescriptors.CompositionGraphAnalysisError,
                        location: null,
                        validator.FullyQualifiedName,
                        validator.GraphBuildError));
                }

                try
                {
                    DirectInstantiationValidator.Validate(validator, allValidators, ctx.ReportDiagnostic);
                    CompositionLifetimeValidator.Validate(validator, allValidators, ctx.ReportDiagnostic);
                }
                catch (OperationCanceledException)
                {
                    // Analyzer cancellation must propagate — do not convert to IOC112.
                    throw;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
                {
                    // IOC112: a validator rule threw — emit diagnostic instead of silently skipping.
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        FluentValidationDiagnosticDescriptors.ValidatorPipelineError,
                        location: null,
                        validator.FullyQualifiedName,
                        ex.Message));
                }
            }
        });
    }
}
