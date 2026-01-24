namespace IoCTools.Generator.Generator.Diagnostics.Validators;

internal static class MemberNameRedundancyValidator
{
    internal static void Validate(SourceProductionContext context, INamedTypeSymbol classSymbol)
    {
        foreach (var attribute in classSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is null) continue;

            try
            {
                if (attribute.AttributeClass.Name.StartsWith("DependsOnAttribute", StringComparison.Ordinal) &&
                    !AttributeParser.IsDependsOnConfigurationAttribute(attribute))
                {
                    if (!HasMemberNames(attribute)) continue;
                    ValidateDependsOn(context, classSymbol, attribute);
                    continue;
                }

                if (AttributeParser.IsDependsOnConfigurationAttribute(attribute))
                {
                    if (!HasConfigurationMemberNames(attribute)) continue;
                    ValidateConfiguration(context, classSymbol, attribute);
                }
            }
            catch
            {
                // Ignore malformed attributes; other diagnostics will cover them.
            }
        }
    }

    private static bool HasMemberNames(AttributeData attribute)
    {
        var (_, _, _, _, memberNames) = AttributeParser.GetDependsOnOptionsFromAttribute(attribute);
        return memberNames is { Length: > 0 };
    }

    private static bool HasConfigurationMemberNames(AttributeData attribute) =>
        attribute.NamedArguments.Any(arg => arg.Key == "MemberNames" && arg.Value.Values is { Length: > 0 });

    private static void ValidateDependsOn(SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        AttributeData attribute)
    {
        var (namingConvention, stripI, prefix, _, memberNames) =
            AttributeParser.GetDependsOnOptionsFromAttribute(attribute);
        if (memberNames is null or { Length: 0 }) return;
        if (attribute.AttributeClass?.TypeArguments is null) return;

        var typeArguments = attribute.AttributeClass.TypeArguments;
        for (var index = 0; index < typeArguments.Length; index++)
        {
            if (index >= memberNames.Length) break;
            var explicitName = memberNames[index];
            if (string.IsNullOrWhiteSpace(explicitName)) continue;

            var substitutedType = TypeSubstitution.SubstituteTypeParameters(typeArguments[index], classSymbol);
            var defaultName = AttributeParser.GenerateFieldName(
                TypeUtilities.GetMeaningfulTypeName(substitutedType),
                namingConvention,
                stripI,
                prefix);

            if (!string.Equals(explicitName, defaultName, StringComparison.Ordinal)) continue;

            Report(context, attribute, explicitName, substitutedType, classSymbol);
        }
    }

    private static void ValidateConfiguration(SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        AttributeData attribute)
    {
        var memberNames = GetStringArray(attribute, "MemberNames");
        if (memberNames is null or { Length: 0 }) return;
        if (attribute.AttributeClass?.TypeArguments is null) return;

        var (namingConvention, stripI, prefix, stripSettingsSuffix) =
            AttributeParser.GetConfigurationNamingOptionsFromAttribute(attribute);

        var constructorKeys = GetConstructorStringArguments(attribute);
        var configKeys = GetStringArray(attribute, "ConfigurationKeys") ?? constructorKeys;

        var typeArguments = attribute.AttributeClass.TypeArguments;
        for (var index = 0; index < typeArguments.Length; index++)
        {
            if (index >= memberNames.Length) break;
            var explicitName = memberNames[index];
            if (string.IsNullOrWhiteSpace(explicitName)) continue;

            var substitutedType = TypeSubstitution.SubstituteTypeParameters(typeArguments[index], classSymbol);
            var configKey = configKeys != null && index < configKeys.Length ? configKeys[index] : null;
            var inferredNameToken = !string.IsNullOrWhiteSpace(configKey)
                ? AttributeParser.DeriveNameTokenFromConfigurationKey(configKey)
                : TypeUtilities.GetMeaningfulTypeName(substitutedType);

            var defaultName = AttributeParser.GenerateConfigurationFieldName(
                inferredNameToken,
                namingConvention,
                stripI,
                prefix,
                stripSettingsSuffix);

            if (!string.Equals(explicitName, defaultName, StringComparison.Ordinal)) continue;

            Report(context, attribute, explicitName, substitutedType, classSymbol);
        }
    }

    private static void Report(SourceProductionContext context,
        AttributeData attribute,
        string explicitName,
        ITypeSymbol dependencyType,
        INamedTypeSymbol classSymbol)
    {
        var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                       ?? classSymbol.Locations.FirstOrDefault();
        if (location == null) return;

        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.RedundantMemberName,
            location,
            explicitName,
            dependencyType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            classSymbol.Name);

        context.ReportDiagnostic(diagnostic);
    }

    private static string[] GetStringArray(AttributeData attribute, string argumentName)
    {
        var named = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == argumentName);
        if (named.Value.Values is { Length: > 0 } values)
            return values.Select(v => v.Value?.ToString() ?? string.Empty).ToArray();

        // For configuration attributes, configuration keys can also arrive via constructor params array
        if (argumentName == "ConfigurationKeys")
        {
            var ctorKeys = GetConstructorStringArguments(attribute);
            if (ctorKeys.Length > 0) return ctorKeys;
        }

        return Array.Empty<string>();
    }

    private static string[] GetConstructorStringArguments(AttributeData attribute)
    {
        var constructorKeys = new List<string>();
        foreach (var arg in attribute.ConstructorArguments)
        {
            if (arg.Kind == TypedConstantKind.Primitive && arg.Value is string s)
            {
                constructorKeys.Add(s);
            }
            else if (arg.Kind == TypedConstantKind.Array && arg.Values.Length > 0)
            {
                constructorKeys.AddRange(arg.Values.Select(v => v.Value?.ToString() ?? string.Empty));
            }
        }

        return constructorKeys.ToArray();
    }
}
