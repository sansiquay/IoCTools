namespace IoCTools.Generator.Generator.Diagnostics.Validators;

internal static class InjectDeprecationValidator
{
    internal static void Validate(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        DiagnosticConfiguration config)
    {
        if (!config.DiagnosticsEnabled) return;

        foreach (var field in classSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (!SymbolEqualityComparer.Default.Equals(field.ContainingType, classSymbol)) continue;
            if (!HasInjectAttribute(field)) continue;

            var location = field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation()
                           ?? classDeclaration.GetLocation();

            var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                DiagnosticDescriptors.InjectDeprecated,
                config.InjectDeprecationSeverity);

            var diagnostic = Diagnostic.Create(
                descriptor,
                location,
                field.Name,
                field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

            context.ReportDiagnostic(diagnostic);
        }
    }

    private const string InjectAttributeMetadataName = "IoCTools.Abstractions.Annotations.InjectAttribute";

    private static bool HasInjectAttribute(IFieldSymbol fieldSymbol)
    {
        foreach (var attribute in fieldSymbol.GetAttributes())
        {
            if (string.Equals(attribute.AttributeClass?.ToDisplayString(), InjectAttributeMetadataName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
