namespace IoCTools.Generator.Generator.Diagnostics.Validators;

/// <summary>
///     IOC104: Profile type (TProfile) used in any of the auto-deps profile-referencing attributes must be
///     non-generic. Fires when the profile type's original definition has type parameters.
/// </summary>
internal static class ProfileIsGenericValidator
{
    internal static void Validate(SourceProductionContext context,
        Compilation compilation,
        DiagnosticConfiguration config)
    {
        if (!config.DiagnosticsEnabled) return;

        // Assembly-level attributes (AutoDepIn / AutoDepsApply / AutoDepsApplyGlob)
        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            var name = attribute.AttributeClass?.Name;
            if (name is null) continue;

            if (!IsProfileReferencingAttributeName(name)) continue;

            var profileType = GetProfileTypeArgument(attribute);
            if (profileType is null) continue;

            if (!IsGenericDefinition(profileType)) continue;

            ReportIoc104(context, attribute, profileType);
        }

        // Class-level AutoDeps<TProfile>
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol typeSymbol) continue;

                foreach (var attribute in typeSymbol.GetAttributes())
                {
                    var name = attribute.AttributeClass?.Name;
                    if (!string.Equals(name, "AutoDepsAttribute", StringComparison.Ordinal)) continue;

                    var profileType = GetProfileTypeArgument(attribute);
                    if (profileType is null) continue;

                    if (!IsGenericDefinition(profileType)) continue;

                    ReportIoc104(context, attribute, profileType);
                }
            }
        }
    }

    private static bool IsProfileReferencingAttributeName(string name) =>
        string.Equals(name, "AutoDepInAttribute", StringComparison.Ordinal) ||
        string.Equals(name, "AutoDepsApplyAttribute", StringComparison.Ordinal) ||
        string.Equals(name, "AutoDepsApplyGlobAttribute", StringComparison.Ordinal);

    private static ITypeSymbol? GetProfileTypeArgument(AttributeData attribute)
    {
        var attributeClass = attribute.AttributeClass;
        if (attributeClass is null) return null;
        if (attributeClass.TypeArguments.Length == 0) return null;
        return attributeClass.TypeArguments[0];
    }

    private static bool IsGenericDefinition(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol named)
        {
            // Closed generics (e.g. GenericProfile<int>) have OriginalDefinition with IsGenericType == true.
            var original = named.OriginalDefinition;
            return original.IsGenericType || original.TypeParameters.Length > 0;
        }

        return false;
    }

    private static void ReportIoc104(SourceProductionContext context,
        AttributeData attribute,
        ITypeSymbol profileType)
    {
        var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;
        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.ProfileIsGeneric,
            location,
            profileType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        context.ReportDiagnostic(diagnostic);
    }
}
