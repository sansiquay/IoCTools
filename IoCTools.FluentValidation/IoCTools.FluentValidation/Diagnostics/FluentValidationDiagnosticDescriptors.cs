namespace IoCTools.FluentValidation.Diagnostics;

using Microsoft.CodeAnalysis;

/// <summary>
/// Diagnostic descriptors for the IoCTools FluentValidation source generator.
/// Starts at IOC100 to avoid collision with existing IoCTools diagnostics (IOC001-IOC094, TDIAG-01-TDIAG-05).
/// IOC103 and IOC104 are reserved by IoCTools.AutoDeps (AutoDepsApplyGlobInvalid / ProfileIsGeneric).
/// Fail-loud infrastructure diagnostics use IOC111/IOC112 to avoid the collision.
/// </summary>
internal static class FluentValidationDiagnosticDescriptors
{
    private const string Category = "IoCTools.FluentValidation";
    private const string HelpLinkBase = "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md";

    /// <summary>
    /// IOC100: Validator directly instantiates a DI-managed child validator.
    /// Detects when a validator creates child validators via 'new' instead of constructor injection.
    /// {0} = child validator type name, {1} = additional dependency chain info
    /// </summary>
    public static readonly DiagnosticDescriptor ValidatorDirectInstantiation = new DiagnosticDescriptor(
        "IOC100",
        "Validator directly instantiates DI-managed child validator",
        "{0} is directly instantiated but has DI dependencies that won't be resolved. {1}.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "FluentValidation validators that are registered with IoCTools should receive child validators via constructor injection rather than direct instantiation. This ensures proper lifetime management and testability.",
        helpLinkUri: HelpLinkBase + "#ioc100");

    /// <summary>
    /// IOC101: Validator composition creates a lifetime mismatch.
    /// Detects when a validator depends on a child validator with an incompatible lifetime.
    /// {0} = parent name, {1} = parent lifetime, {2} = child name, {3} = child lifetime
    /// </summary>
    public static readonly DiagnosticDescriptor ValidatorLifetimeMismatch = new DiagnosticDescriptor(
        "IOC101",
        "Validator composition creates lifetime mismatch",
        "Validator '{0}' ({1}) composes '{2}' ({3}) creating a captive dependency. The child's shorter lifetime will be captured by the parent's longer lifetime.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Validator lifetime mismatches can cause issues similar to captive dependency problems. A Singleton validator should not depend on a Scoped or Transient child validator.",
        helpLinkUri: HelpLinkBase + "#ioc101");

    /// <summary>
    /// IOC102: Validator class is missing the partial modifier.
    /// Validators using IoCTools attributes need the partial modifier for constructor generation.
    /// </summary>
    public static readonly DiagnosticDescriptor ValidatorMissingPartial = new DiagnosticDescriptor(
        "IOC102",
        "Validator class missing partial modifier",
        "Validator '{0}' extends AbstractValidator<{1}> and has IoCTools attributes but is not marked partial. Add the 'partial' modifier to enable constructor generation.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "FluentValidation validators that use IoCTools lifetime attributes or [Inject]/[DependsOn] must be marked partial to allow constructor generation. Add 'partial' to the class declaration.",
        helpLinkUri: HelpLinkBase + "#ioc102");

    /// <summary>
    /// IOC111: CompositionGraphBuilder encountered an internal analysis error while building edges.
    /// Emitted instead of silently skipping, so the failure is visible to the consumer.
    /// Severity is Error because a missing edge can cause false-negative IOC100/IOC101 results —
    /// silent data loss is worse than a visible error.
    /// {0} = parent validator FQN, {1} = exception message
    /// </summary>
    internal static readonly DiagnosticDescriptor CompositionGraphAnalysisError = new DiagnosticDescriptor(
        "IOC111",
        "Composition graph analysis error",
        "IoCTools: composition graph analysis for '{0}' encountered an internal error and one or more edges may be missing: {1}. This is likely a generator bug — please file an issue.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CompositionGraphBuilder caught an unexpected exception while analyzing a validator's composition edges. The affected validator's edges were skipped, which may cause false-negative results for IOC100/IOC101. Raised as Error because missing edges silently suppress real diagnostics.",
        helpLinkUri: HelpLinkBase + "#ioc111");

    /// <summary>
    /// IOC112: ValidatorDiagnosticsPipeline encountered an internal error for a validator.
    /// Emitted instead of silently skipping, so a broken validator rule is visible.
    /// Severity is Error because a pipeline failure drops all diagnostics for the affected validator,
    /// potentially masking real IOC100/IOC101 violations.
    /// {0} = validator FQN, {1} = exception message
    /// </summary>
    internal static readonly DiagnosticDescriptor ValidatorPipelineError = new DiagnosticDescriptor(
        "IOC112",
        "Validator diagnostic pipeline error",
        "IoCTools: diagnostics pipeline for validator '{0}' encountered an internal error and its diagnostics may be incomplete: {1}. This is likely a generator bug — please file an issue.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ValidatorDiagnosticsPipeline caught an unexpected exception while running validators against a ValidatorClassInfo. The affected validator's diagnostics were skipped, which may cause false-negative results for IOC100/IOC101. Raised as Error because dropped validators silently mask real violations.",
        helpLinkUri: HelpLinkBase + "#ioc112");
}
