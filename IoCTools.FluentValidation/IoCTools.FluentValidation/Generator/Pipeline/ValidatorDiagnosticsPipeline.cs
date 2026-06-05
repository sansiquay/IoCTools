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
                EmitDiagnosticsForValidator(validator, allValidators, ctx.ReportDiagnostic);
        });
    }

    /// <summary>
    /// Runs all diagnostic checks for a single validator and emits results via <paramref name="report"/>.
    /// <para>
    /// IOC111: if <see cref="ValidatorClassInfo.GraphBuildError"/> is set (captured by
    /// <see cref="CompositionGraph.CompositionGraphBuilder.BuildEdges"/> during the SyntaxProvider phase),
    /// the error is surfaced here where a <c>ReportDiagnostic</c> sink is available.
    /// </para>
    /// <para>
    /// IOC112: if <see cref="DirectInstantiationValidator"/> or <see cref="CompositionLifetimeValidator"/>
    /// throw an unexpected exception, the exception is emitted as a diagnostic instead of silently
    /// dropping all diagnostics for the affected validator.
    /// </para>
    /// <para>
    /// OperationCanceledException always propagates — it is never converted to IOC111 or IOC112.
    /// </para>
    /// </summary>
    /// <param name="validator">The validator to check.</param>
    /// <param name="allValidators">All validators in the current compilation batch.</param>
    /// <param name="report">Sink that receives emitted diagnostics.</param>
    /// <param name="overrideValidate">
    /// Optional override for the validator-rule invocation — used in tests to inject a throwing
    /// substitute in place of <see cref="DirectInstantiationValidator"/> +
    /// <see cref="CompositionLifetimeValidator"/>. Production callers leave this null.
    /// </param>
    internal static void EmitDiagnosticsForValidator(
        ValidatorClassInfo validator,
        ImmutableArray<ValidatorClassInfo> allValidators,
        Action<Diagnostic> report,
        Action<ValidatorClassInfo, ImmutableArray<ValidatorClassInfo>, Action<Diagnostic>>? overrideValidate = null)
    {
        // IOC111: composition graph build caught an exception in the SyntaxProvider phase
        // (where no ReportDiagnostic sink is available). Surface it here.
        if (validator.GraphBuildError != null)
        {
            report(Diagnostic.Create(
                FluentValidationDiagnosticDescriptors.CompositionGraphAnalysisError,
                location: null,
                validator.FullyQualifiedName,
                validator.GraphBuildError));
        }

        try
        {
            if (overrideValidate != null)
                overrideValidate(validator, allValidators, report);
            else
            {
                DirectInstantiationValidator.Validate(validator, allValidators, report);
                CompositionLifetimeValidator.Validate(validator, allValidators, report);
            }
        }
        catch (OperationCanceledException)
        {
            // Analyzer cancellation must propagate — do not convert to IOC112.
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // IOC112: a validator rule threw — emit diagnostic instead of silently skipping.
            report(Diagnostic.Create(
                FluentValidationDiagnosticDescriptors.ValidatorPipelineError,
                location: null,
                validator.FullyQualifiedName,
                ex.Message));
        }
    }
}
