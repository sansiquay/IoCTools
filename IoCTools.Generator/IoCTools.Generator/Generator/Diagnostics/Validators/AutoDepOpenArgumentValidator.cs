namespace IoCTools.Generator.Generator.Diagnostics.Validators;

/// <summary>
///     Validates the <c>Type</c> constructor argument of <see cref="IoCTools.Abstractions.Annotations.AutoDepOpenAttribute" />.
///     Emits IOC106 when the type is a multi-arity unbound generic, and IOC107 when the type is not generic.
/// </summary>
internal static class AutoDepOpenArgumentValidator
{
    internal static void Validate(SourceProductionContext context,
        Compilation compilation,
        DiagnosticConfiguration config)
    {
        if (!config.DiagnosticsEnabled) return;

        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            var name = attribute.AttributeClass?.Name;
            if (!string.Equals(name, "AutoDepOpenAttribute", StringComparison.Ordinal)) continue;

            if (attribute.ConstructorArguments.Length == 0) continue;

            var arg = attribute.ConstructorArguments[0];
            if (arg.Value is not INamedTypeSymbol namedType) continue;

            var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

            if (!namedType.IsGenericType)
            {
                // IOC107: AutoDepOpen on a non-generic type
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.AutoDepOpenNonGeneric,
                    location,
                    namedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                context.ReportDiagnostic(diagnostic);
                continue;
            }

            // typeof(T<,>) surfaces as an unbound generic via roslyn
            if (namedType.IsUnboundGenericType && namedType.TypeParameters.Length > 1)
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.AutoDepOpenMultiArity,
                    location,
                    namedType.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    namedType.TypeParameters.Length);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
