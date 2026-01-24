namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using Microsoft.CodeAnalysis.CSharp;

/// <summary>
///     Validates that classes using IoCTools attributes that require code generation are marked as partial.
///     Reports IOC080 when a class has [Inject], [DependsOn], [DependsOnConfiguration], or [InjectConfiguration]
///     attributes but is not marked as partial.
/// </summary>
internal static class MissingPartialKeywordValidator
{
    /// <summary>
    ///     Validates that a class is marked as partial if it uses IoCTools code-generating attributes.
    /// </summary>
    public static void Validate(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        // Check if class is already partial
        var isPartial = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
        if (isPartial) return;

        // Collect which code-generating attributes are present
        var codeGeneratingAttributes = new List<string>();

        // Check for [Inject] fields
        var hasInjectFields = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "InjectAttribute"));
        if (hasInjectFields)
            codeGeneratingAttributes.Add("[Inject]");

        // Check for [InjectConfiguration] fields
        var hasInjectConfigurationFields = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                             "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute"));
        if (hasInjectConfigurationFields)
            codeGeneratingAttributes.Add("[InjectConfiguration]");

        // Check for [DependsOn] attributes
        var hasDependsOnAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOnAttribute") == true);
        if (hasDependsOnAttribute)
            codeGeneratingAttributes.Add("[DependsOn]");

        // Check for [DependsOnConfiguration] attributes
        var hasDependsOnConfigurationAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOnConfigurationAttribute") == true);
        if (hasDependsOnConfigurationAttribute)
            codeGeneratingAttributes.Add("[DependsOnConfiguration]");

        // If no code-generating attributes found, skip
        if (codeGeneratingAttributes.Count == 0) return;

        // Report the diagnostic
        var attributesList = string.Join(", ", codeGeneratingAttributes);
        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.ServiceClassMustBePartial,
            classDeclaration.Identifier.GetLocation(),
            classSymbol.Name,
            attributesList);
        context.ReportDiagnostic(diagnostic);
    }
}
