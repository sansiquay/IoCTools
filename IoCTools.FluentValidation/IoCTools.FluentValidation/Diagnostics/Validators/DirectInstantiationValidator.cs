namespace IoCTools.FluentValidation.Diagnostics.Validators;

using System;
using System.Collections.Immutable;
using System.Linq;

using Generator.CompositionGraph;

using Microsoft.CodeAnalysis;

using Models;

/// <summary>
/// Detects when a validator directly instantiates a DI-managed child validator (D-13, D-14).
/// Reports IOC100 when <c>SetValidator(new ChildValidator())</c> or <c>Include(new ChildValidator())</c>
/// is used for a child that should be injected via the constructor.
/// </summary>
internal static class DirectInstantiationValidator
{
    /// <summary>
    /// Validates composition edges for direct instantiation of DI-managed validators.
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

        foreach (var edge in validator.CompositionEdges)
        {
            if (!edge.IsDirectInstantiation)
                continue;

            // Look up child in discovered DI-managed validators
            var childValidator = FindValidator(allValidators, edge.ChildValidatorName);

            if (childValidator != null)
            {
                // Child is a discovered DI-managed validator - always report
                var dependencyInfo = BuildDependencyChainMessage(childValidator.Value);
                var message = string.IsNullOrEmpty(dependencyInfo)
                    ? $"Inject it through the constructor instead (registered as {childValidator.Value.Lifetime})"
                    : dependencyInfo;

                reportDiagnostic(Diagnostic.Create(
                    FluentValidationDiagnosticDescriptors.ValidatorDirectInstantiation,
                    edge.Location ?? Location.None,
                    edge.ChildValidatorTypeName,
                    message));
            }
            else
            {
                // Child not in discovered validators - check if it has [Inject] fields
                // indicating it has DI dependencies even if not discovered as a validator
                if (HasInjectAttributes(edge, validator))
                {
                    reportDiagnostic(Diagnostic.Create(
                        FluentValidationDiagnosticDescriptors.ValidatorDirectInstantiation,
                        edge.Location ?? Location.None,
                        edge.ChildValidatorTypeName,
                        "Inject it through the constructor instead"));
                }
            }
        }
    }

    /// <summary>
    /// Finds a validator by its fully qualified name in the discovered validators array.
    /// </summary>
    private static ValidatorClassInfo? FindValidator(
        ImmutableArray<ValidatorClassInfo> allValidators,
        string childValidatorName)
    {
        foreach (var v in allValidators)
        {
            if (v.FullyQualifiedName == childValidatorName)
                return v;
        }

        return null;
    }

    /// <summary>
    /// Builds a message listing the child validator's DI dependencies (D-14).
    /// </summary>
    private static string BuildDependencyChainMessage(ValidatorClassInfo childValidator)
    {
        var classSymbol = childValidator.ClassSymbol;
        if (classSymbol == null)
            return string.Empty;

        // Check for [Inject] fields
        var injectDeps = classSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.GetAttributes().Any(a =>
                a.AttributeClass?.Name == "InjectAttribute" ||
                a.AttributeClass?.Name == "Inject"))
            .Select(f => f.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
            .ToList();

        // Check for [DependsOn] attributes
        var dependsOnDeps = classSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.Name?.StartsWith("DependsOnAttribute") == true ||
                        a.AttributeClass?.Name?.StartsWith("DependsOn") == true)
            .Where(a => a.AttributeClass?.IsGenericType == true)
            .SelectMany(a => a.AttributeClass!.TypeArguments)
            .Select(t => t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
            .ToList();

        var allDeps = injectDeps.Concat(dependsOnDeps).Distinct().ToList();

        if (allDeps.Count == 0)
            return string.Empty;

        var depList = string.Join(", ", allDeps);
        return $"{childValidator.ClassSymbol.Name} depends on {depList} which won't be resolved";
    }

    /// <summary>
    /// Checks if the child type referenced by the edge has [Inject] attributes,
    /// by resolving the type through the parent's semantic model.
    /// </summary>
    private static bool HasInjectAttributes(CompositionEdge edge, ValidatorClassInfo parentValidator)
    {
        if (parentValidator.SemanticModel == null)
            return false;

        // Try to find the child type in the compilation
        var compilation = parentValidator.SemanticModel.Compilation;
        var childType = compilation.GetTypeByMetadataName(edge.ChildValidatorName);

        if (childType == null)
        {
            // Try without global:: prefix
            var cleanName = edge.ChildValidatorName;
            if (cleanName.StartsWith("global::"))
                cleanName = cleanName.Substring("global::".Length);
            childType = compilation.GetTypeByMetadataName(cleanName);
        }

        if (childType == null)
            return false;

        return childType.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(f => f.GetAttributes().Any(a =>
                a.AttributeClass?.Name == "InjectAttribute" ||
                a.AttributeClass?.Name == "Inject"));
    }
}
