namespace IoCTools.Generator.Analysis;

/// <summary>
///     Specifies the mode for DependsOn field type substitution.
/// </summary>
internal enum DependsOnSubstitutionMode
{
    /// <summary>
    ///     Standard type parameter substitution using TypeSubstitution.SubstituteTypeParameters.
    ///     Used for most scenarios where generic type parameters are replaced with concrete types.
    /// </summary>
    Standard,

    /// <summary>
    ///     Inheritance chain substitution using TypeSubstitution.ApplyInheritanceChainSubstitution.
    ///     Used when resolving types through inheritance hierarchies.
    /// </summary>
    InheritanceChain
}

/// <summary>
///     Focused logic for [DependsOn] attribute processing and field-name generation.
/// </summary>
internal static class DependsOnFieldAnalyzer
{
    /// <summary>
    ///     Unified core method for collecting [DependsOn] fields with configurable result projection.
    ///     Supports different return types and substitution modes via delegate parameters.
    /// </summary>
    private static List<TResult> GetRawDependsOnFieldsCore<TResult>(
        INamedTypeSymbol typeSymbol,
        DependsOnSubstitutionMode substitutionMode,
        INamedTypeSymbol? targetTypeForSubstitution,
        bool includeExternalFlag,
        HashSet<string>? allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations,
        Func<ITypeSymbol, string, bool, TResult> resultSelector)
    {
        var fields = new List<TResult>();

        // Determine attribute source based on substitution mode
        var attributeSource = substitutionMode == DependsOnSubstitutionMode.InheritanceChain
            ? typeSymbol
            : typeSymbol.OriginalDefinition;

        // Build attribute filter based on mode
        Func<AttributeData, bool> attributeFilter = substitutionMode == DependsOnSubstitutionMode.InheritanceChain
            ? attr => attr.AttributeClass?.ToDisplayString()
                .StartsWith("IoCTools.Abstractions.Annotations.DependsOnAttribute") == true
            : attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true;

        var dependsOnAttributes = attributeSource.GetAttributes()
            .Where(attributeFilter)
            .Where(attr => !AttributeParser.IsDependsOnConfigurationAttribute(attr))
            .ToList();

        foreach (var attribute in dependsOnAttributes)
        {
            var genericTypeArguments = attribute.AttributeClass?.TypeArguments.ToList();
            if (genericTypeArguments == null) continue;

            var (namingConvention, stripI, prefix, external, memberNames) =
                AttributeParser.GetDependsOnOptionsFromAttribute(attribute);

            for (var index = 0; index < genericTypeArguments.Count; index++)
            {
                var genericTypeArgument = genericTypeArguments[index];
                var substitutedType = substitutionMode == DependsOnSubstitutionMode.InheritanceChain
                    ? TypeSubstitution.ApplyInheritanceChainSubstitution(
                        genericTypeArgument, typeSymbol, targetTypeForSubstitution!)
                    : TypeSubstitution.SubstituteTypeParameters(genericTypeArgument, typeSymbol);

                var explicitName = memberNames != null && index < memberNames.Length
                    ? memberNames[index]
                    : null;
                var fieldName = !string.IsNullOrWhiteSpace(explicitName)
                    ? explicitName!
                    : AttributeParser.GenerateFieldName(
                        TypeUtilities.GetMeaningfulTypeName(substitutedType), namingConvention, stripI, prefix);

                var isExternal = includeExternalFlag &&
                                 (external ||
                                  ExternalServiceAnalyzer.IsTypeExternal(substitutedType, allRegisteredServices,
                                      allImplementations));

                fields.Add(resultSelector(substitutedType, fieldName, isExternal));
            }
        }

        return fields;
    }

    public static List<(ITypeSymbol ServiceType, string FieldName)> GetRawDependsOnFieldsForType(
        INamedTypeSymbol typeSymbol)
    {
        return GetRawDependsOnFieldsCore(
            typeSymbol,
            DependsOnSubstitutionMode.Standard,
            null,
            false,
            null,
            null,
            (serviceType, fieldName, _) => (serviceType, fieldName));
    }

    public static List<(ITypeSymbol ServiceType, string FieldName)> GetRawDependsOnFieldsForTypeWithSubstitution(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol targetTypeForSubstitution)
    {
        return GetRawDependsOnFieldsCore(
            typeSymbol,
            DependsOnSubstitutionMode.InheritanceChain,
            targetTypeForSubstitution,
            false,
            null,
            null,
            (serviceType, fieldName, _) => (serviceType, fieldName));
    }

    public static List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)>
        GetRawDependsOnFieldsForTypeWithExternalFlag(
            INamedTypeSymbol typeSymbol,
            HashSet<string>? allRegisteredServices = null,
            Dictionary<string, List<INamedTypeSymbol>>? allImplementations = null)
    {
        return GetRawDependsOnFieldsCore(
            typeSymbol,
            DependsOnSubstitutionMode.Standard,
            null,
            true,
            allRegisteredServices,
            allImplementations,
            (serviceType, fieldName, isExternal) => (serviceType, fieldName, isExternal));
    }

    // NOTE: moved to AttributeParser for reuse across analyzers
}
