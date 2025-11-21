namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System.Linq;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis;

using Models;

internal static class CollectionDependencyValidator
{
    private static readonly string[] AllowedCollectionDefs =
    {
        "global::System.Collections.Generic.IReadOnlyCollection<T>"
    };

    internal static void Validate(SourceProductionContext context,
        Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        var hasArrayDiagnostic = false;

        if (hierarchyDependencies?.RawAllDependencies != null)
            foreach (var dep in hierarchyDependencies.RawAllDependencies)
            {
                if (dep.ServiceType == null) continue;

                var display = dep.ServiceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                var isArray = IsArrayType(dep.ServiceType) ||
                              display.EndsWith("[]", System.StringComparison.Ordinal);

                if (isArray)
                {
                    hasArrayDiagnostic = true;
                    var element = dep.ServiceType is IArrayTypeSymbol at
                        ? at.ElementType.ToDisplayString()
                        : display.Length > 2
                            ? display.Substring(0, display.Length - 2)
                            : "element";
                    Report(context, classSymbol, dep.ServiceType.ToDisplayString(), "array of " + element);
                    continue;
                }

                if (dep.ServiceType is not INamedTypeSymbol named) continue;

                var def = named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var isDirectAllowed = AllowedCollectionDefs.Contains(def);
                if (isDirectAllowed) continue;

                var implementsAllowed = named.AllInterfaces.Any(i =>
                    AllowedCollectionDefs.Contains(i.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));

                var isCollection = def.StartsWith("global::System.Collections.Generic.") ||
                                  def.StartsWith("global::System.Collections.");

                if (isCollection || implementsAllowed)
                {
                    Report(context, classSymbol, named.ToDisplayString(), def);
                }
            }

        // Syntax fallback only when semantic pass found no arrays
        if (!hasArrayDiagnostic)
            WarnForArrayTypeArguments(context, classSymbol, classDeclaration);
    }

    private static void Report(SourceProductionContext context, INamedTypeSymbol classSymbol, string displayedType,
        string displayedDef)
    {
        var location = classSymbol.Locations.FirstOrDefault();
        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnsupportedCollectionDependency,
            location,
            displayedType,
            classSymbol.Name,
            displayedDef);
        context.ReportDiagnostic(diagnostic);
    }

    private static void WarnForArrayTypeArguments(SourceProductionContext context, INamedTypeSymbol classSymbol,
        Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax classDeclaration)
    {
        // Syntax-based detection across all partial declarations
        var attrSyntaxes = classDeclaration.AttributeLists.SelectMany(al => al.Attributes);
        foreach (var attrSyntax in attrSyntaxes)
        {
            var nameText = attrSyntax.Name.ToString();
            if (nameText.IndexOf("DependsOn", System.StringComparison.Ordinal) < 0) continue;

            Microsoft.CodeAnalysis.CSharp.Syntax.GenericNameSyntax? gName = null;
            if (attrSyntax.Name is Microsoft.CodeAnalysis.CSharp.Syntax.GenericNameSyntax direct)
                gName = direct;
            else if (attrSyntax.Name is Microsoft.CodeAnalysis.CSharp.Syntax.QualifiedNameSyntax qn &&
                     qn.Right is Microsoft.CodeAnalysis.CSharp.Syntax.GenericNameSyntax right)
                gName = right;

            if (gName != null)
                foreach (var arg in gName.TypeArgumentList.Arguments)
                    if (arg is Microsoft.CodeAnalysis.CSharp.Syntax.ArrayTypeSyntax arrayTypeSyntax)
                    {
                        var location = attrSyntax.GetLocation();
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnsupportedCollectionDependency,
                            location,
                            arrayTypeSyntax.ToString(),
                            classSymbol.Name,
                            "array");
                        context.ReportDiagnostic(diagnostic);
                    }

            // Also inspect typeof() arguments for array types
            if (attrSyntax.ArgumentList != null)
                foreach (var arg in attrSyntax.ArgumentList.Arguments)
                    if (arg.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.TypeOfExpressionSyntax tof &&
                        tof.Type is Microsoft.CodeAnalysis.CSharp.Syntax.ArrayTypeSyntax arrTypeSyntax)
                    {
                        var location = arg.GetLocation();
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnsupportedCollectionDependency,
                            location,
                            arrTypeSyntax.ToString(),
                            classSymbol.Name,
                            "array");
                        context.ReportDiagnostic(diagnostic);
                    }
        }
    }

    private static bool IsArrayType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol) return true;
        var display = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (display.EndsWith("[]", System.StringComparison.Ordinal)) return true;
        return type.Kind == SymbolKind.ArrayType;
    }

}
