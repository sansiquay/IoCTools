namespace IoCTools.Generator.Generator.Diagnostics.Validators;

internal static class IConfigurationUsageValidator
{
    internal static void Validate(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        var raw = hierarchyDependencies.RawAllDependencies;
        if (raw == null || raw.Count == 0) return;

        foreach (var dep in raw)
        {
            // Only user-declared dependencies on this class (level 0) count
            if (dep.Level != 0) continue;
            var display = dep.ServiceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (display is not "global::Microsoft.Extensions.Configuration.IConfiguration") continue;

            var location = FindDependencyLocation(classDeclaration, classSymbol, dep.FieldName) ??
                           classDeclaration.Identifier.GetLocation();

            var diag = Diagnostic.Create(DiagnosticDescriptors.IConfigurationDependencyDiscouraged,
                location,
                classSymbol.Name);
            context.ReportDiagnostic(diag);
        }
    }

    private static Location? FindDependencyLocation(TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string? fieldName)
    {
        if (!string.IsNullOrEmpty(fieldName))
        {
            var field = classSymbol.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.Name == fieldName && SymbolEqualityComparer.Default.Equals(f.ContainingType, classSymbol));
            var loc = field?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation();
            if (loc != null) return loc;
        }

        return classDeclaration.Identifier.GetLocation();
    }
}
