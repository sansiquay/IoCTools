namespace IoCTools.Generator.Analysis;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Models;

using Utilities;

/// <summary>
///     Focused logic for discovering [InjectConfiguration] fields and parsing their options.
/// </summary>
internal static class ConfigurationFieldAnalyzer
{
    public static List<ConfigurationInjectionInfo> GetConfigurationInjectedFieldsForType(
        INamedTypeSymbol typeSymbol,
        SemanticModel semanticModel)
    {
        var configFields = new List<ConfigurationInjectionInfo>();

        foreach (var declaringSyntaxRef in typeSymbol.DeclaringSyntaxReferences)
            try
            {
                if (declaringSyntaxRef.GetSyntax() is not TypeDeclarationSyntax typeDeclaration)
                    continue;

                foreach (var fieldDeclaration in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    // Skip static and const fields
                    var modifiers = fieldDeclaration.Modifiers;
                    if (modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.ConstKeyword)))
                        continue;

                    // Check for [InjectConfiguration]
                    AttributeSyntax? injectConfigAttribute = null;
                    foreach (var attributeList in fieldDeclaration.AttributeLists)
                        foreach (var attribute in attributeList.Attributes)
                        {
                            var attributeText = attribute.Name.ToString();
                            if (attributeText == "InjectConfiguration" ||
                                attributeText == "InjectConfigurationAttribute" ||
                                attributeText.EndsWith("InjectConfiguration") ||
                                attributeText.EndsWith("InjectConfigurationAttribute"))
                            {
                                injectConfigAttribute = attribute;
                                break;
                            }
                        }

                    if (injectConfigAttribute == null) continue;

                    foreach (var variable in fieldDeclaration.Declaration.Variables)
                    {
                        var fieldName = variable.Identifier.Text;

                        // Prefer symbol to preserve nullability
                        var fieldSymbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                        if (fieldSymbol?.Type == null) continue;

                        var substitutedType = TypeSubstitution.SubstituteTypeParameters(fieldSymbol.Type, typeSymbol);
                        var (configKey, defaultValue, required, supportsReloading) =
                            ParseInjectConfigurationAttribute(injectConfigAttribute, semanticModel);

                        configFields.Add(new ConfigurationInjectionInfo(
                            fieldName,
                            substitutedType,
                            configKey,
                            defaultValue,
                            required,
                            supportsReloading));
                    }
                }
            }
            catch
            {
                // Continue with what we have
            }

        configFields.AddRange(GetDependsOnConfigurationDeclarations(typeSymbol));

        return configFields;
    }

    private static (string? configKey, object? defaultValue, bool required, bool supportsReloading)
        ParseInjectConfigurationAttribute(
            AttributeSyntax attributeSyntax,
            SemanticModel semanticModel)
    {
        string? configKey = null;
        object? defaultValue = null;
        var required = true;
        var supportsReloading = false;

        if (attributeSyntax.ArgumentList != null)
            foreach (var argument in attributeSyntax.ArgumentList.Arguments)
                if (argument.NameEquals != null)
                {
                    var parameterName = argument.NameEquals.Name.Identifier.ValueText;
                    switch (parameterName)
                    {
                        case "DefaultValue":
                            defaultValue = GetConstantValue(argument.Expression, semanticModel);
                            break;
                        case "Required":
                            if (GetConstantValue(argument.Expression, semanticModel) is bool req)
                                required = req;
                            break;
                        case "SupportsReloading":
                            if (GetConstantValue(argument.Expression, semanticModel) is bool reload)
                                supportsReloading = reload;
                            break;
                    }
                }
                else
                {
                    if (configKey == null && GetConstantValue(argument.Expression, semanticModel) is string key)
                        configKey = key;
                }

        return (configKey, defaultValue, required, supportsReloading);
    }

    private static object? GetConstantValue(ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        try
        {
            var constantValue = semanticModel.GetConstantValue(expression);
            if (constantValue.HasValue) return constantValue.Value;

            if (expression is LiteralExpressionSyntax literal)
            {
                if (literal.Token.IsKind(SyntaxKind.TrueKeyword) || literal.Token.IsKind(SyntaxKind.FalseKeyword))
                    return literal.Token.IsKind(SyntaxKind.TrueKeyword);
                if (literal.Token.IsKind(SyntaxKind.NumericLiteralToken))
                {
                    var t = literal.Token.ValueText;
                    if (int.TryParse(t, out var i)) return i;
                    if (double.TryParse(t, out var d)) return d;
                    if (decimal.TryParse(t, out var m)) return m;
                }

                return literal.Token.ValueText;
            }
        }
        catch
        {
        }

        return null;
    }

    private static IEnumerable<ConfigurationInjectionInfo> GetDependsOnConfigurationDeclarations(
        INamedTypeSymbol typeSymbol)
    {
        var results = new List<ConfigurationInjectionInfo>();
        var attributes = typeSymbol.GetAttributes()
            .Where(AttributeParser.IsDependsOnConfigurationAttribute)
            .ToList();

        if (attributes.Count == 0) return results;

        var existingFieldNames = new HashSet<string>(typeSymbol.GetMembers().OfType<IFieldSymbol>()
            .Select(f => f.Name));

        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass?.TypeArguments.Length is null or 0) continue;

            var substitutedTypes = attribute.AttributeClass.TypeArguments
                .Select(arg => TypeSubstitution.SubstituteTypeParameters(arg, typeSymbol))
                .ToArray();

            if (substitutedTypes.Length == 0) continue;

            var constructorKeys = GetConstructorStringArguments(attribute);
            var configKeys = GetStringArray(attribute, "ConfigurationKeys") ?? constructorKeys;
            var memberNames = GetStringArray(attribute, "MemberNames");
            var defaultValues = GetObjectArray(attribute, "DefaultValues");
            var requiredFlags = GetBoolArray(attribute, "RequiredFlags");
            var supportsReloadingFlags = GetBoolArray(attribute, "SupportsReloadingFlags");
            var defaultValue = GetNamedArgumentValue(attribute, "DefaultValue");
            var required = GetBoolNamedArgument(attribute, "Required") ?? true;
            var supportsReloading = GetBoolNamedArgument(attribute, "SupportsReloading") ?? false;
            var (namingConvention, stripI, prefix, stripSettingsSuffix) =
                AttributeParser.GetConfigurationNamingOptionsFromAttribute(attribute);

            for (var index = 0; index < substitutedTypes.Length; index++)
            {
                var fieldType = substitutedTypes[index];
                var configKey = configKeys != null && index < configKeys.Length ? configKeys[index] : null;
                var explicitName = memberNames != null && index < memberNames.Length
                    ? memberNames[index]
                    : null;

                var inferredNameToken = !string.IsNullOrWhiteSpace(configKey)
                    ? AttributeParser.DeriveNameTokenFromConfigurationKey(configKey)
                    : TypeUtilities.GetMeaningfulTypeName(fieldType);
                var generatedName = !string.IsNullOrWhiteSpace(explicitName)
                    ? explicitName!
                    : AttributeParser.GenerateConfigurationFieldName(inferredNameToken, namingConvention, stripI, prefix,
                        stripSettingsSuffix);
                var fieldName = EnsureUniqueFieldName(generatedName, existingFieldNames);

                var slotDefault = defaultValues != null && index < defaultValues.Length && defaultValues[index] != null
                    ? defaultValues[index]
                    : defaultValue;
                var slotRequired = requiredFlags != null && index < requiredFlags.Length
                    ? requiredFlags[index]
                    : required;
                var slotReload = supportsReloadingFlags != null && index < supportsReloadingFlags.Length
                    ? supportsReloadingFlags[index]
                    : supportsReloading;

                results.Add(new ConfigurationInjectionInfo(fieldName,
                    fieldType,
                    configKey,
                    slotDefault,
                    slotRequired,
                    slotReload,
                    generatedField: true));
            }
        }

        return results;
    }

    private static string EnsureUniqueFieldName(string desiredName,
        HashSet<string> existingFieldNames)
    {
        var candidate = desiredName;
        var suffix = 1;
        while (existingFieldNames.Contains(candidate))
        {
            candidate = desiredName + suffix;
            suffix++;
        }

        existingFieldNames.Add(candidate);
        return candidate;
    }

    private static string?[]? GetStringArray(AttributeData attribute,
        string argumentName)
    {
        var argument = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == argumentName);
        if (argument.Key == null || argument.Value.Kind != TypedConstantKind.Array) return null;

        return argument.Value.Values.Select(v => v.Value as string).ToArray();
    }

    private static object?[]? GetObjectArray(AttributeData attribute,
        string argumentName)
    {
        var argument = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == argumentName);
        if (argument.Key == null || argument.Value.Kind != TypedConstantKind.Array) return null;

        return argument.Value.Values.Select(v => v.Value).ToArray();
    }

    private static bool[]? GetBoolArray(AttributeData attribute,
        string argumentName)
    {
        var argument = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == argumentName);
        if (argument.Key == null || argument.Value.Kind != TypedConstantKind.Array) return null;

        return argument.Value.Values.Select(v => v.Value as bool? ?? false).ToArray();
    }

    private static object? GetNamedArgumentValue(AttributeData attribute,
        string argumentName)
    {
        var argument = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == argumentName);
        return argument.Key == null ? null : argument.Value.Value;
    }

    private static bool? GetBoolNamedArgument(AttributeData attribute,
        string argumentName)
    {
        var argument = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == argumentName);
        return argument.Key == null ? null : argument.Value.Value as bool?;
    }

    private static string?[]? GetConstructorStringArguments(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0) return null;

        // If the last argument is the params string[] it will come through as an array typed constant.
        var lastArg = attribute.ConstructorArguments[attribute.ConstructorArguments.Length - 1];
        if (lastArg.Kind == TypedConstantKind.Array && lastArg.Values is { Length: > 0 } values &&
            lastArg.Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String })
            return values.Select(v => v.Value as string).ToArray();

        // Fallback: collect string constructor arguments while ignoring other option parameters.
        var strings = attribute.ConstructorArguments
            .Select(arg => arg.Value as string)
            .Where(v => v != null)
            .ToArray();

        return strings.Length == 0 ? null : strings;
    }
}
