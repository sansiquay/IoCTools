namespace IoCTools.Generator.Generator.Diagnostics.Validators;

internal static class ManualConstructorMixingValidator
{
    /// <summary>
    ///     Detects classes that declare IoCTools-managed dependencies but also define manual constructors.
    ///     Mixing these states results in unpredictable constructor generation, so we surface a blocking error
    ///     and short-circuit other dependency diagnostics.
    /// </summary>
    /// <returns>true when a conflict was reported.</returns>
    internal static bool ReportIfMixed(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        if (classSymbol == null) return false;

        // IoCTools-managed dependency indicators. These require generated constructors.
        var hasInjectFields = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(classSymbol);
        var hasInjectConfiguration = ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
        var hasDependsOn = classSymbol.GetAttributes()
            .Any(a => a.AttributeClass?.Name?.StartsWith("DependsOn", StringComparison.Ordinal) == true);

        if (!hasInjectFields && !hasInjectConfiguration && !hasDependsOn) return false;

        // Any user defined instance constructor (including primary constructors) counts as manual.
        var manualConstructors = classSymbol.InstanceConstructors
            .Where(ctor => ctor.MethodKind == MethodKind.Constructor)
            // Only constructors that have user-authored syntax count as manual; synthesized ctors are ignored.
            .Where(ctor => ctor.DeclaringSyntaxReferences.Length > 0)
            .ToList();

        var parameterListSyntax = GetParameterList(classDeclaration);
        var hasPrimaryConstructorSyntax = parameterListSyntax?.Parameters.Count > 0;

        if (manualConstructors.Count == 0 && !hasPrimaryConstructorSyntax)
            return false;

        // Prefer the constructor with parameters for reporting clarity.
        var targetCtor = manualConstructors
            .OrderByDescending(c => c.Parameters.Length)
            .ThenBy(c => c.DeclaredAccessibility)
            .FirstOrDefault();

        var signature = targetCtor != null
            ? BuildConstructorSignature(targetCtor)
            : BuildPrimaryConstructorSignatureFromSyntax(classDeclaration, parameterListSyntax);

        var location = targetCtor?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation() ??
                       parameterListSyntax?.GetLocation() ??
                       classDeclaration.Identifier.GetLocation();

        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.ManualConstructorConflict, location,
            classSymbol.Name, signature);
        context.ReportDiagnostic(diagnostic);

        return true;
    }

    private static string BuildConstructorSignature(IMethodSymbol ctor)
    {
        var parameterList = string.Join(", ", ctor.Parameters
            .Select(p => $"{TypeHelpers.FormatTypeNameForDiagnostic(p.Type)} {p.Name}"));
        return $"{ctor.ContainingType.Name}({parameterList})";
    }

    private static string BuildPrimaryConstructorSignatureFromSyntax(TypeDeclarationSyntax classDeclaration,
        ParameterListSyntax? parameterListSyntax)
    {
        var parameters = parameterListSyntax?.Parameters.Select(p => p.ToString()) ?? Enumerable.Empty<string>();
        return $"{classDeclaration.Identifier.Text}({string.Join(", ", parameters)})";
    }

    private static ParameterListSyntax? GetParameterList(TypeDeclarationSyntax classDeclaration)
    {
        // Record declarations have a ParameterList in current Roslyn versions
        if (classDeclaration is RecordDeclarationSyntax recordDeclaration)
            return recordDeclaration.ParameterList;

        // Future Roslyn versions surface ParameterList on classes/structs for primary constructors.
        // Use reflection to avoid compile-time dependency so the generator still builds on older Roslyn.
        var parameterListProperty = classDeclaration.GetType().GetProperty("ParameterList");
        if (parameterListProperty != null)
        {
            var value = parameterListProperty.GetValue(classDeclaration);
            if (value is ParameterListSyntax pls) return pls;
        }

        return null;
    }
}
