namespace IoCTools.FluentValidation.Diagnostics;

using Microsoft.CodeAnalysis;

/// <summary>
/// Diagnostic descriptors for the IoCTools FluentValidation source generator.
/// Starts at IOC100 to avoid collision with existing IoCTools diagnostics (IOC001-IOC094, TDIAG-01-TDIAG-05).
/// </summary>
internal static class FluentValidationDiagnosticDescriptors
{
    private const string Category = "IoCTools.FluentValidation";
    private const string HelpLinkBase = "https://github.com/nate123456/IoCTools/blob/main/docs/diagnostics.md";

    /// <summary>
    /// IOC100: Validator directly instantiates a DI-managed child validator.
    /// Detects when a validator creates child validators via 'new' instead of constructor injection.
    /// </summary>
    public static readonly DiagnosticDescriptor ValidatorDirectInstantiation = new DiagnosticDescriptor(
        "IOC100",
        "Validator directly instantiates DI-managed child validator",
        "Validator '{0}' directly instantiates child validator '{1}' which is registered with DI. Use constructor injection instead.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "FluentValidation validators that are registered with IoCTools should receive child validators via constructor injection rather than direct instantiation. This ensures proper lifetime management and testability.",
        helpLinkUri: HelpLinkBase + "#ioc100");

    /// <summary>
    /// IOC101: Validator composition creates a lifetime mismatch.
    /// Detects when a validator depends on a child validator with an incompatible lifetime.
    /// </summary>
    public static readonly DiagnosticDescriptor ValidatorLifetimeMismatch = new DiagnosticDescriptor(
        "IOC101",
        "Validator composition creates lifetime mismatch",
        "Validator '{0}' ({1}) depends on child validator '{2}' ({3}). {4}",
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
}
