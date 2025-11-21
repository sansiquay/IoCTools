namespace IoCTools.Generator.Analysis;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

using Utilities;

/// <summary>
///     Focused logic for [DependsOn] attribute processing and field-name generation.
/// </summary>
internal static class DependsOnFieldAnalyzer
{
    public static List<(ITypeSymbol ServiceType, string FieldName)> GetRawDependsOnFieldsForType(
        INamedTypeSymbol typeSymbol)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName)>();

        var originalTypeDefinition = typeSymbol.OriginalDefinition;
        var dependsOnAttributes = originalTypeDefinition.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true)
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
                var substitutedType = TypeSubstitution.SubstituteTypeParameters(genericTypeArgument, typeSymbol);
                var explicitName = memberNames != null && index < memberNames.Length
                    ? memberNames[index]
                    : null;
                var fieldName = !string.IsNullOrWhiteSpace(explicitName)
                    ? explicitName!
                    : AttributeParser.GenerateFieldName(
                        TypeUtilities.GetMeaningfulTypeName(substitutedType), namingConvention, stripI, prefix);
                fields.Add((substitutedType, fieldName));
            }
        }

        return fields;
    }

    public static List<(ITypeSymbol ServiceType, string FieldName)> GetRawDependsOnFieldsForTypeWithSubstitution(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol targetTypeForSubstitution)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName)>();

        var dependsOnAttributes = typeSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString()
                .StartsWith("IoCTools.Abstractions.Annotations.DependsOnAttribute") == true)
            .Where(attr => !AttributeParser.IsDependsOnConfigurationAttribute(attr))
            .ToList();

        foreach (var attribute in dependsOnAttributes)
        {
            if (attribute.AttributeClass?.TypeArguments == null) continue;
            var (namingConvention, stripI, prefix, external, memberNames) =
                AttributeParser.GetDependsOnOptionsFromAttribute(attribute);
            var typeArgs = attribute.AttributeClass.TypeArguments;
            for (var index = 0; index < typeArgs.Length; index++)
            {
                var genericTypeArgument = typeArgs[index];
                var substitutedType = TypeSubstitution.ApplyInheritanceChainSubstitution(
                    genericTypeArgument, typeSymbol, targetTypeForSubstitution);
                var explicitName = memberNames != null && index < memberNames.Length
                    ? memberNames[index]
                    : null;
                var fieldName = !string.IsNullOrWhiteSpace(explicitName)
                    ? explicitName!
                    : AttributeParser.GenerateFieldName(
                        TypeUtilities.GetMeaningfulTypeName(substitutedType), namingConvention, stripI, prefix);
                fields.Add((substitutedType, fieldName));
            }
        }

        return fields;
    }

    public static List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)>
        GetRawDependsOnFieldsForTypeWithExternalFlag(
            INamedTypeSymbol typeSymbol,
            HashSet<string>? allRegisteredServices = null,
            Dictionary<string, List<INamedTypeSymbol>>? allImplementations = null)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)>();

        var originalTypeDefinition = typeSymbol.OriginalDefinition;
        var dependsOnAttributes = originalTypeDefinition.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true)
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
                var substitutedType = TypeSubstitution.SubstituteTypeParameters(genericTypeArgument, typeSymbol);
                var explicitName = memberNames != null && index < memberNames.Length
                    ? memberNames[index]
                    : null;
                var fieldName = !string.IsNullOrWhiteSpace(explicitName)
                    ? explicitName!
                    : AttributeParser.GenerateFieldName(
                        TypeUtilities.GetMeaningfulTypeName(substitutedType), namingConvention, stripI, prefix);
                var isExternal = external ||
                                 ExternalServiceAnalyzer.IsTypeExternal(substitutedType, allRegisteredServices,
                                     allImplementations);
                fields.Add((substitutedType, fieldName, isExternal));
            }
        }

        return fields;
    }

    // NOTE: moved to AttributeParser for reuse across analyzers
}
