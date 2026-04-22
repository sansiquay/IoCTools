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

    private static bool HasInjectAttribute(IFieldSymbol fieldSymbol)
    {
        foreach (var attribute in fieldSymbol.GetAttributes())
        {
            var attributeName = attribute.AttributeClass?.Name;
            if (string.Equals(attributeName, "InjectAttribute", StringComparison.Ordinal)) return true;
        }

        return false;
    }
}
