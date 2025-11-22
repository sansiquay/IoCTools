namespace IoCTools.Generator.Generator.Diagnostics.Validators;

internal static class NonServiceDependencyValidator
{
    internal static void Validate(SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        if (hierarchyDependencies?.RawAllDependencies == null) return;

        foreach (var dep in hierarchyDependencies.RawAllDependencies)
        {
            if (dep.Source == DependencySource.ConfigurationInjection) continue;
            if (dep.ServiceType == null) continue;

            // Arrays are handled by CollectionDependencyValidator (IOC045)
            if (dep.ServiceType is IArrayTypeSymbol) continue;

            var type = dep.ServiceType;
            if (!IsNonServiceType(type)) continue;

            var location = classSymbol.Locations.FirstOrDefault();
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.NonServiceDependencyType,
                location,
                type.ToDisplayString(),
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsNonServiceType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Void) return true;
        if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Object)
            return true; // primitives, string, decimal, etc.

        if (type.TypeKind == TypeKind.Enum || type.TypeKind == TypeKind.Struct) return true;

        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            var def = named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var isEnumerable = def is "global::System.Collections.Generic.IEnumerable<T>" or
                "global::System.Collections.Generic.ICollection<T>" or
                "global::System.Collections.Generic.IList<T>" or
                "global::System.Collections.Generic.List<T>" or
                "global::System.Collections.Generic.IReadOnlyList<T>" or
                "global::System.Collections.Generic.IReadOnlyCollection<T>";
            if (isEnumerable && named.TypeArguments.Length == 1)
            {
                var inner = named.TypeArguments[0];
                return IsNonServiceType(inner); // warn if inner is non-service
            }
        }

        return false;
    }
}
