namespace IoCTools.Generator.Generator.Diagnostics.Validators;

/// <summary>
///     IOC097 (defensive): a profile type used in any auto-deps profile-referencing attribute
///     should implement <c>IAutoDepsProfile</c>. The C# generic constraint
///     <c>where TProfile : IAutoDepsProfile</c> already enforces this at compile time; this validator
///     is a safety net for edge cases such as metadata/source mismatches or partial-class scenarios
///     where the interface implementation can be observed as missing.
/// </summary>
internal static class ProfileMarkerValidator
{
    private const string AutoDepsProfileMarkerFullName = "IoCTools.Abstractions.Annotations.IAutoDepsProfile";

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

            if (ImplementsAutoDepsProfile(profileType)) continue;

            ReportIoc097(context, attribute, profileType);
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

                    if (ImplementsAutoDepsProfile(profileType)) continue;

                    ReportIoc097(context, attribute, profileType);
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

    private static bool ImplementsAutoDepsProfile(ITypeSymbol profileType)
    {
        if (profileType is IErrorTypeSymbol) return true; // skip unresolved symbols
        if (profileType.TypeKind == TypeKind.Error) return true;

        foreach (var iface in profileType.AllInterfaces)
        {
            if (iface.ToDisplayString() == AutoDepsProfileMarkerFullName) return true;
        }

        return false;
    }

    private static void ReportIoc097(SourceProductionContext context,
        AttributeData attribute,
        ITypeSymbol profileType)
    {
        var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;
        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.ProfileMissingMarker,
            location,
            profileType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        context.ReportDiagnostic(diagnostic);
    }
}
