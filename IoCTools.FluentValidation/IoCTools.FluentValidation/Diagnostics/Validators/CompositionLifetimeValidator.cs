namespace IoCTools.FluentValidation.Diagnostics.Validators;

using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

using Models;

/// <summary>
/// Detects lifetime mismatches in validator composition chains (D-12).
/// Reports IOC101 when a longer-lived parent validator composes a shorter-lived child,
/// creating a captive dependency problem.
/// </summary>
internal static class CompositionLifetimeValidator
{
    // Lifetime hierarchy: Singleton (longest) > Scoped > Transient (shortest)
    private static int GetLifetimeRank(string? lifetime)
    {
        switch (lifetime)
        {
            case "Singleton": return 3;
            case "Scoped": return 2;
            case "Transient": return 1;
            default: return 0; // Unknown or null — skip validation
        }
    }

    /// <summary>
    /// Validates composition edges for lifetime mismatches between parent and child validators.
    /// </summary>
    /// <param name="validator">The parent validator to check.</param>
    /// <param name="allValidators">All discovered validators for cross-referencing.</param>
    /// <param name="reportDiagnostic">Callback to report diagnostics.</param>
    internal static void Validate(
        ValidatorClassInfo validator,
        ImmutableArray<ValidatorClassInfo> allValidators,
        Action<Diagnostic> reportDiagnostic)
    {
        if (validator.CompositionEdges.IsDefaultOrEmpty)
            return;

        // Parent must have a lifetime to validate against
        var parentRank = GetLifetimeRank(validator.Lifetime);
        if (parentRank == 0)
            return;

        foreach (var edge in validator.CompositionEdges)
        {
            // Only check injected validators (not directly instantiated ones — those get IOC100)
            if (edge.IsDirectInstantiation)
                continue;

            // Look up child in discovered validators
            ValidatorClassInfo? childValidator = null;
            foreach (var v in allValidators)
            {
                if (v.FullyQualifiedName == edge.ChildValidatorName)
                {
                    childValidator = v;
                    break;
                }
            }

            if (childValidator == null)
                continue;

            var childRank = GetLifetimeRank(childValidator.Value.Lifetime);
            if (childRank == 0)
                continue;

            // Captive dependency: parent lives longer than child
            // Singleton(3) > Scoped(2) or Transient(1) = problem
            // Scoped(2) > Transient(1) is technically a captive dependency too,
            // but we follow the standard IoCTools pattern which only flags Singleton->shorter
            if (parentRank > childRank && parentRank == 3) // Singleton parent with shorter-lived child
            {
                reportDiagnostic(Diagnostic.Create(
                    FluentValidationDiagnosticDescriptors.ValidatorLifetimeMismatch,
                    edge.Location ?? Location.None,
                    validator.ClassSymbol.Name,
                    validator.Lifetime,
                    childValidator.Value.ClassSymbol.Name,
                    childValidator.Value.Lifetime));
            }
        }
    }
}
