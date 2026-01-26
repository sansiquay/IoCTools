namespace IoCTools.Generator.Utilities;

using System.Text.RegularExpressions;

internal static class AttributeParser
{
    private static string ParseNamingConventionEnum(object? enumValue)
    {
        if (enumValue == null) return "CamelCase";
        return enumValue switch
        {
            0 => "CamelCase",
            1 => "PascalCase",
            2 => "SnakeCase",
            _ => "CamelCase"
        };
    }

    private static string ParseRegistrationModeEnum(object? enumValue)
    {
        if (enumValue == null) return "All";
        return enumValue switch
        {
            0 => "DirectOnly",
            1 => "All",
            2 => "Exclusionary",
            _ => "All"
        };
    }

    public static (string namingConvention, bool stripI, string prefix) GetNamingConventionOptionsFromAttribute(
        AttributeData attribute)
    {
        var namingConvention = "CamelCase";
        var stripI = true;
        var prefix = "_";

        // Check constructor arguments first (like ExtractLifetime method)
        var constructorArgs = attribute.ConstructorArguments;
        if (constructorArgs.Length > 0)
        {
            // First parameter is namingConvention
            namingConvention = ParseNamingConventionEnum(constructorArgs[0].Value);
        }

        if (constructorArgs.Length > 1)
        {
            // Second parameter is stripI
            var stripIValue = constructorArgs[1].Value;
            if (stripIValue is bool b) stripI = b;
        }

        if (constructorArgs.Length > 2)
        {
            // Third parameter is prefix
            var prefixValue = constructorArgs[2].Value;
            if (prefixValue != null) prefix = prefixValue.ToString() ?? "_";
        }

        // Also check named arguments as fallback (for backwards compatibility)
        foreach (var namedArg in attribute.NamedArguments)
            switch (namedArg.Key)
            {
                case "NamingConvention":
                    namingConvention = ParseNamingConventionEnum(namedArg.Value.Value);
                    break;
                case "StripI":
                    stripI = namedArg.Value.Value as bool? ?? true;
                    break;
                case "Prefix":
                    prefix = namedArg.Value.Value?.ToString() ?? "_";
                    break;
            }

        return (namingConvention, stripI, prefix);
    }

    public static (string namingConvention, bool stripI, string prefix, bool external, string[] memberNames)
        GetDependsOnOptionsFromAttribute(AttributeData attribute)
    {
        var namingConvention = "CamelCase";
        var stripI = true;
        var prefix = "_";
        var external = false;
        var memberNames = Array.Empty<string>();

        var constructorArgs = attribute.ConstructorArguments;

        // The constructor shape is:
        // (NamingConvention, bool stripI, string prefix, bool external, string? memberName1, ...)
        // Member-name slots align with the generic arity; we read the provided strings past index 3.
        if (constructorArgs.Length > 0)
        {
            // First parameter is namingConvention
            namingConvention = ParseNamingConventionEnum(constructorArgs[0].Value);
        }

        if (constructorArgs.Length > 1)
        {
            // Second parameter is stripI
            var stripIValue = constructorArgs[1].Value;
            if (stripIValue is bool b) stripI = b;
        }

        if (constructorArgs.Length > 2)
        {
            // Third parameter is prefix
            var prefixValue = constructorArgs[2].Value;
            if (prefixValue != null) prefix = prefixValue.ToString() ?? "_";
        }

        if (constructorArgs.Length > 3)
        {
            // Fourth parameter is external
            var externalValue = constructorArgs[3].Value;
            if (externalValue is bool ext) external = ext;
        }

        var memberNameArgs = new List<string>();
        if (constructorArgs.Length > 4)
        {
            for (var i = 4; i < constructorArgs.Length; i++)
            {
                var arg = constructorArgs[i];
                if (arg.Kind == TypedConstantKind.Primitive && arg.Value is string s && !string.IsNullOrEmpty(s))
                    memberNameArgs.Add(s);
            }
        }

        if (memberNameArgs.Count > 0)
            memberNames = memberNameArgs.ToArray();

        // Also check named arguments for backwards compatibility with old MemberNames; keep reading naming/external
        foreach (var namedArg in attribute.NamedArguments)
            switch (namedArg.Key)
            {
                case "NamingConvention":
                    namingConvention = ParseNamingConventionEnum(namedArg.Value.Value);
                    break;
                case "StripI":
                    stripI = namedArg.Value.Value as bool? ?? true;
                    break;
                case "Prefix":
                    prefix = namedArg.Value.Value?.ToString() ?? "_";
                    break;
                case "External":
                    external = namedArg.Value.Value as bool? ?? false;
                    break;
                case "MemberNames":
                case "memberNames":
                    if (namedArg.Value.Values is { Length: > 0 } mnames)
                        memberNames = mnames.Select(v => v.Value?.ToString() ?? string.Empty).ToArray();
                    break;
            }

        return (namingConvention, stripI, prefix, external, memberNames);
    }

    public static bool IsDependsOnConfigurationAttribute(AttributeData attribute) =>
        AttributeTypeChecker.IsType(attribute.AttributeClass?.BaseType, AttributeTypeChecker.DependsOnConfigurationAttributeBase) ||
        attribute.AttributeClass?.ToDisplayString().StartsWith(
            AttributeTypeChecker.DependsOnConfigurationAttributeGeneric) == true;

    public static (string namingConvention, bool stripI, string prefix, bool stripSettingsSuffix)
        GetConfigurationNamingOptionsFromAttribute(AttributeData attribute)
    {
        var namingConvention = "CamelCase";
        var stripI = true;
        var prefix = "_";
        var stripSettingsSuffix = true;

        var constructorArgs = attribute.ConstructorArguments;

        if (constructorArgs.Length > 0 && constructorArgs[0].Kind != TypedConstantKind.Array)
        {
            namingConvention = ParseNamingConventionEnum(constructorArgs[0].Value);
        }

        if (constructorArgs.Length > 1 && constructorArgs[1].Kind != TypedConstantKind.Array)
            stripI = constructorArgs[1].Value as bool? ?? stripI;

        if (constructorArgs.Length > 2 && constructorArgs[2].Kind != TypedConstantKind.Array)
        {
            var prefixValue = constructorArgs[2].Value;
            if (prefixValue != null) prefix = prefixValue.ToString() ?? prefix;
        }

        if (constructorArgs.Length > 3 && constructorArgs[3].Kind != TypedConstantKind.Array)
            stripSettingsSuffix = constructorArgs[3].Value as bool? ?? stripSettingsSuffix;

        foreach (var namedArg in attribute.NamedArguments)
            switch (namedArg.Key)
            {
                case "NamingConvention":
                    namingConvention = ParseNamingConventionEnum(namedArg.Value.Value);
                    break;
                case "StripI":
                    if (namedArg.Value.Value is bool strip)
                        stripI = strip;
                    break;
                case "Prefix":
                    prefix = namedArg.Value.Value?.ToString() ?? prefix;
                    break;
                case "StripSettingsSuffix":
                    if (namedArg.Value.Value is bool stripSettings)
                        stripSettingsSuffix = stripSettings;
                    break;
            }

        return (namingConvention, stripI, prefix, stripSettingsSuffix);
    }

    public static string GetRegistrationMode(AttributeData attribute)
    {
        const string defaultMode = "All";
        if (attribute == null) return defaultMode;

        if (attribute.ConstructorArguments.Length > 0)
        {
            var ctorValue = attribute.ConstructorArguments[0].Value;
            if (ctorValue is int enumValue)
                return ParseRegistrationModeEnum(enumValue);

            if (int.TryParse(ctorValue?.ToString(), out var parsedCtor))
                return ParseRegistrationModeEnum(parsedCtor);
        }

        var modeArg = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == "Mode");
        if (modeArg.Key != null)
        {
            if (modeArg.Value.Value is int namedEnum)
                return ParseRegistrationModeEnum(namedEnum);

            var namedString = modeArg.Value.Value?.ToString();
            if (namedString is "DirectOnly" or "All" or "Exclusionary") return namedString;
        }

        return defaultMode;
    }

    /// <summary>
    ///     Generates a field name from a type name, handling naming conventions, prefixes, and C# reserved keywords.
    /// </summary>
    /// <param name="originalTypeName">
    ///     The original type name (e.g., "IService", "string", "MyOptions").
    /// </param>
    /// <param name="namingConvention">
    ///     The naming convention to apply: "CamelCase", "PascalCase", or "SnakeCase".
    ///     Default is "CamelCase".
    /// </param>
    /// <param name="stripI">
    ///     This parameter is deprecated and no longer affects semantic field naming.
    ///     Field names are always derived semantically (e.g., IService → _service).
    /// </param>
    /// <param name="prefix">
    ///     The prefix to add to the field name. Special values:
    ///     <list type="bullet">
    ///         <item><description>"":</description> Empty prefix applies naming convention only (no underscores)</item>
    ///         <item><description>"_":</description> Default prefix adds underscore before the convention</item>
    ///         <item><description>Custom prefix ending with "_":</description> Used as-is without additional underscore</item>
    ///         <item><description>Custom prefix not ending with "_":</description> Gets "_" + convention applied</item>
    ///     </list>
    /// </param>
    /// <returns>
    ///     A valid C# identifier for use as a field name.
    /// </returns>
    /// <remarks>
    ///     <para><b>Reserved Keyword Handling:</b></para>
    ///     <para>
    ///         Reserved C# keywords are escaped ONLY when the final identifier would be a reserved keyword.
    ///         This means:
    ///     </para>
    ///     <list type="bullet">
    ///         <item><description>"" + "string" (empty prefix + camelCase) → "_stringValue" (escaped because "string" is reserved)</description></item>
    ///         <item><description>"_" + "string" (default prefix + camelCase) → "_string" (NOT escaped because "_string" is not reserved)</description></item>
    ///         <item><description>"custom_" + "string" (custom prefix ending with "_") → "custom_string" (NOT escaped because "custom_string" is not reserved)</description></item>
    ///         <item><description>"custom" + "string" (custom prefix NOT ending in "_") → "_customStringValue" (escaped because "customStringValue" starts with "custom" which is reserved)</description></item>
    ///     </list>
    ///     <para>
    ///         The key insight: escaping applies to the FINAL identifier, not intermediate values.
    ///         An underscore prefix followed by a reserved keyword does NOT require escaping because "_keyword" is valid.
    ///     </para>
    ///
    ///     <para><b>Interface Type Handling:</b></para>
    ///     <para>
    ///         Interface types (starting with 'I' followed by an uppercase letter) always have the 'I' stripped:
    ///     </para>
    ///     <list type="bullet">
    ///         <item><description>"IService" → "service"</description></item>
    ///         <item><description>"IDerivedService" → "derivedService"</description></item>
    ///         <item><description>"IMy" (single letter) → "IMy" (not an interface pattern, kept as-is)</description></item>
    ///     </list>
    ///
    ///     <para><b>Examples:</b></para>
    ///     <code>
    ///         // Interface type, default prefix, camelCase
    ///         GenerateFieldName("IService", "CamelCase", true, "_") → "_service"
    ///
    ///         // Non-interface type, default prefix, camelCase
    ///         GenerateFieldName("MyService", "CamelCase", true, "_") → "_myService"
    ///
    ///         // Reserved keyword "string" as interface, default prefix
    ///         GenerateFieldName("Istring", "CamelCase", true, "") → "stringValue"
    ///
    ///         // Reserved keyword "string", empty prefix (gets escaped)
    ///         GenerateFieldName("string", "CamelCase", true, "") → "stringValue"
    ///
    ///         // Reserved keyword "string" with "_" prefix (NOT escaped)
    ///         GenerateFieldName("string", "CamelCase", true, "_") → "_string"
    ///     </code>
    /// </remarks>
    public static string GenerateFieldName(string originalTypeName,
        string namingConvention,
        bool stripI,
        string prefix)
    {
        // Extract semantic base name for the field
        var fieldBaseName = ExtractSemanticFieldName(originalTypeName);

        // Apply naming convention and prefix handling
        var fieldName = ApplyPrefixToFieldName(fieldBaseName, namingConvention, prefix);

        // Handle C# reserved keywords by adding a suffix
        return EscapeReservedKeyword(fieldName);
    }

    private static string ExtractSemanticFieldName(string originalTypeName)
    {
        // CRITICAL FIX: Always use semantic naming for field generation
        // Field names should be semantically meaningful regardless of stripI parameter
        // stripI only affects the naming convention application, not the fundamental semantic naming
        if (originalTypeName.StartsWith("I") && originalTypeName.Length > 1 && char.IsUpper(originalTypeName[1]))
            // For interface types, always use semantic naming (strip 'I') for field names
            // This ensures consistent field naming: IService -> _service, IDerivedService -> _derivedService
            return originalTypeName.Substring(1);

        // For non-interface types, use the original type name
        return originalTypeName;
    }

    private static string ApplyNamingConvention(string name, string namingConvention)
    {
        return namingConvention switch
        {
            "CamelCase" => char.ToLowerInvariant(name[0]) + name.Substring(1),
            "PascalCase" => char.ToUpperInvariant(name[0]) + name.Substring(1),
            "SnakeCase" => Regex.Replace(name, @"(?<!^)([A-Z])", "_$1").ToLower(),
            _ => name
        };
    }

    private static string ApplyPrefixToFieldName(string fieldBaseName, string namingConvention, string prefix)
    {
        if (prefix == "")
        {
            // Empty prefix: apply naming convention to type name, no prefixes at all
            return ApplyNamingConvention(fieldBaseName, namingConvention);
        }

        if (prefix == "_")
        {
            // Default prefix: apply naming convention to type name, then add underscore prefix
            return "_" + ApplyNamingConvention(fieldBaseName, namingConvention);
        }

        if (prefix.EndsWith("_"))
        {
            // Custom prefix ending with underscore: apply naming convention to type name, use prefix as-is
            return prefix + ApplyNamingConvention(fieldBaseName, namingConvention);
        }

        // Custom prefix not ending with underscore: format prefix+type together, then add underscore
        var combinedName = prefix + fieldBaseName;
        return "_" + ApplyNamingConvention(combinedName, namingConvention);
    }

    public static string GenerateConfigurationFieldName(string originalTypeName,
        string namingConvention,
        bool stripI,
        string prefix,
        bool stripSettingsSuffix)
    {
        var workingName = stripSettingsSuffix ? ConfigurationNamingUtilities.StripConfigurationSuffixes(originalTypeName) : originalTypeName;
        if (string.IsNullOrWhiteSpace(workingName)) workingName = originalTypeName;
        return GenerateFieldName(workingName, namingConvention, stripI, prefix);
    }

    public static string DeriveNameTokenFromConfigurationKey(string? configurationKey)
    {
        var safeKey = configurationKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(safeKey)) return "ConfigurationValue";
        var segments = safeKey.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) segments = new[] { safeKey };

        var tokens = new List<char>();
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment)) continue;

            var capitalizeNext = true;
            foreach (var ch in segment)
            {
                if (!char.IsLetterOrDigit(ch))
                {
                    capitalizeNext = true;
                    continue;
                }

                tokens.Add(capitalizeNext ? char.ToUpperInvariant(ch) : ch);
                capitalizeNext = false;
            }
        }

        return tokens.Count > 0 ? new string(tokens.ToArray()) : "ConfigurationValue";
    }

    public static string ExtractLifetime(AttributeData serviceAttribute)
    {
        // Check constructor arguments first
        var constructorArgLifetime = serviceAttribute.ConstructorArguments.FirstOrDefault().Value;
        if (constructorArgLifetime != null)
        {
            // Assuming constructorArgLifetime is an enum represented as an int, map it to the string.
            // This example uses a switch expression for mapping.
            var lifetimeStr = constructorArgLifetime switch
            {
                0 => "Scoped",
                1 => "Transient",
                2 => "Singleton",
                _ => throw new Exception(
                    $"Couldn't parse lifetime value from constructor arguments: {constructorArgLifetime}")
            };
            return lifetimeStr;
        }

        // Then check named arguments if constructor argument wasn't used or didn't provide a valid value
        var lifetimeArg = serviceAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "Lifetime");

        if (lifetimeArg.Key == null) return "Scoped";

        var lifetimeValue = lifetimeArg.Value.Value?.ToString();
        if (lifetimeValue is "Scoped" or "Transient" or "Singleton")
            return lifetimeValue;
        if (lifetimeValue != null)
            throw new Exception("Couldn't parse lifetime value from named arguments: " + lifetimeValue);

        // Default to "Scoped" if neither constructor nor named arguments specified a lifetime
        return "Scoped";
    }

    /// <summary>
    ///     Escapes C# reserved keywords by appending a suffix to avoid compilation errors
    /// </summary>
    private static string EscapeReservedKeyword(string identifier)
    {
        // C# reserved keywords that could conflict with parameter names
        var reservedKeywords = new HashSet<string>
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "virtual",
            "void",
            "volatile",
            "while"
        };

        if (reservedKeywords.Contains(identifier)) return identifier + "Value";

        return identifier;
    }
}