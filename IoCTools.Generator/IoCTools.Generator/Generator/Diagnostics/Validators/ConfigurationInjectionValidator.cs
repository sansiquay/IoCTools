namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using Microsoft.CodeAnalysis;

internal static class ConfigurationInjectionValidator
{
    /// <summary>
    ///     Result of cycle detection during configuration validation
    /// </summary>
    private sealed class CycleDetectionResult
    {
        public bool HasCycle { get; set; }
        public INamedTypeSymbol? CycleType { get; set; }
        public string? CycleProperty { get; set; }
    }
    internal static void ValidateConfigurationInjection(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var configurationFields = GetConfigurationFieldsFromHierarchy(classSymbol);
        if (!configurationFields.Any()) return;

        if (!classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.ConfigurationOnNonPartialClass,
                classDeclaration.GetLocation(), classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        foreach (var fieldSymbol in configurationFields)
        {
            if (fieldSymbol.IsStatic)
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.ConfigurationOnStaticField,
                    classDeclaration.GetLocation(), fieldSymbol.Name, classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
                continue;
            }

            var configAttribute = fieldSymbol.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() ==
                                        "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute");
            if (configAttribute == null) continue;

            // IOC089: Check if SupportsReloading is used on primitive types
            ValidateSupportsReloadingUsage(context, fieldSymbol, classDeclaration, configAttribute);

            if (configAttribute.ConstructorArguments.Length > 0)
            {
                var key = configAttribute.ConstructorArguments[0].Value?.ToString();
                var validationResult = ValidateConfigurationKey(key);
                if (!validationResult.IsValid)
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.InvalidConfigurationKey,
                        classDeclaration.GetLocation(), key ?? string.Empty, validationResult.ErrorMessage);
                    context.ReportDiagnostic(diagnostic);
                }
            }

            var fieldType = fieldSymbol.Type;

            // IOC088: Check for circular references in configuration types
            var recursionStack = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var cycleResult = CheckForCircularReference(fieldType, recursionStack, null);

            if (cycleResult?.HasCycle == true)
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.ConfigurationCircularReference,
                    classDeclaration.GetLocation(),
                    cycleResult.CycleType?.ToDisplayString() ?? fieldType.ToDisplayString(),
                    cycleResult.CycleProperty ?? "unknown");
                context.ReportDiagnostic(diagnostic);
                continue; // Skip unsupported type diagnostic if cycle detected
            }

            if (!IsSupportedConfigurationType(fieldType))
            {
                var reasonMessage = GetUnsupportedTypeReason(fieldType);
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnsupportedConfigurationType,
                    classDeclaration.GetLocation(), fieldType.ToDisplayString(), reasonMessage);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /// <summary>
    ///     Checks for circular references in configuration type hierarchy
    /// </summary>
    private static CycleDetectionResult? CheckForCircularReference(
        ITypeSymbol type,
        HashSet<INamedTypeSymbol> recursionStack,
        string? propertyName)
    {
        // Only check named types (classes, structs) that can have properties
        if (type is not INamedTypeSymbol namedType) return null;

        var originalDef = namedType.OriginalDefinition;

        // Handle Nullable<T>
        if (originalDef.SpecialType == SpecialType.System_Nullable_T && namedType.TypeArguments.Length > 0)
            return CheckForCircularReference(namedType.TypeArguments[0], recursionStack, propertyName);

        // Handle Options pattern - these don't need cycle checking
        var typeName = namedType.ToDisplayString();
        if (typeName.StartsWith("Microsoft.Extensions.Options.IOptions") ||
            typeName.StartsWith("Microsoft.Extensions.Options.IOptionsSnapshot") ||
            typeName.StartsWith("Microsoft.Extensions.Options.IOptionsMonitor"))
            return null;

        // Handle collection types - check element type for cycles
        if (namedType.TypeArguments.Length > 0)
        {
            // Only check first type argument (element type) for cycles
            var elementCycle = CheckForCircularReference(namedType.TypeArguments[0], recursionStack, propertyName);
            if (elementCycle?.HasCycle == true) return elementCycle;
        }

        // Handle array types
        if (type is IArrayTypeSymbol arrayType)
            return CheckForCircularReference(arrayType.ElementType, recursionStack, propertyName);

        // Only POCO classes can have circular references
        if (type.TypeKind != TypeKind.Class || type.IsValueType) return null;
        if (type.IsAbstract || type.TypeKind == TypeKind.Interface) return null;

        // Check for cycle using recursion stack
        if (!recursionStack.Add(namedType))
        {
            // Type already in stack - cycle detected!
            return new CycleDetectionResult
            {
                HasCycle = true,
                CycleType = namedType,
                CycleProperty = propertyName ?? "self"
            };
        }

        try
        {
            // Check all public, settable, non-static properties for cycles
            foreach (var member in namedType.GetMembers())
            {
                if (member is not IPropertySymbol prop) continue;
                if (prop.DeclaredAccessibility != Accessibility.Public) continue;
                if (prop.IsStatic) continue;
                if (prop.SetMethod == null) continue;

                var propCycle = CheckForCircularReference(prop.Type, recursionStack, prop.Name);
                if (propCycle?.HasCycle == true) return propCycle;
            }

            return null;
        }
        finally
        {
            // Remove from stack when backtracking (enables diamond dependencies)
            recursionStack.Remove(namedType);
        }
    }

    private static List<IFieldSymbol> GetConfigurationFieldsFromHierarchy(INamedTypeSymbol classSymbol)
    {
        var result = new List<IFieldSymbol>();
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers().OfType<IFieldSymbol>())
            {
                var hasInjectConfigurationAttribute = member.GetAttributes()
                    .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                                 "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute");
                if (hasInjectConfigurationAttribute) result.Add(member);
            }

            currentType = currentType.BaseType;
        }

        return result;
    }

    private static bool IsSupportedConfigurationType(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Object) return true;
        if (type.SpecialType == SpecialType.System_String) return true;

        if (type.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T &&
            type is INamedTypeSymbol nullableType)
            return IsSupportedConfigurationType(nullableType.TypeArguments.First());

        if (type is IArrayTypeSymbol arrayType) return IsSupportedConfigurationType(arrayType.ElementType);

        if (type is INamedTypeSymbol namedType)
        {
            var typeName = namedType.ToDisplayString();
            if (namedType.TypeKind == TypeKind.Enum) return true;

            var supportedBuiltinTypes = new[]
            {
                "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", "System.Guid", "System.Uri",
                "System.Decimal"
            };
            if (supportedBuiltinTypes.Contains(typeName)) return true;

            if (typeName.StartsWith("Microsoft.Extensions.Options.IOptions") ||
                typeName.StartsWith("Microsoft.Extensions.Options.IOptionsSnapshot") ||
                typeName.StartsWith("Microsoft.Extensions.Options.IOptionsMonitor"))
                return true;

            if (typeName.StartsWith("System.Collections.Generic.List<") ||
                typeName.StartsWith("System.Collections.Generic.IList<") ||
                typeName.StartsWith("System.Collections.Generic.ICollection<") ||
                typeName.StartsWith("System.Collections.Generic.IEnumerable<") ||
                typeName.StartsWith("System.Collections.Generic.Dictionary<") ||
                typeName.StartsWith("System.Collections.Generic.IDictionary<"))
                if (namedType.TypeArguments.Length > 0)
                    return namedType.TypeArguments.All(arg => IsSupportedConfigurationType(arg));

            if (namedType.TypeKind == TypeKind.Interface) return false;
            if (namedType.IsAbstract) return false;
            return namedType.Constructors.Any(c => c.Parameters.Length == 0);
        }

        return false;
    }

    private static string GetUnsupportedTypeReason(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            if (type is INamedTypeSymbol interfaceType)
            {
                var typeName = interfaceType.ToDisplayString();
                if (typeName.StartsWith("Microsoft.Extensions.Options."))
                    return "requires parameterless constructor";
            }

            return "Interfaces cannot be bound from configuration";
        }

        if (type.IsAbstract) return "Abstract types cannot be bound from configuration";
        if (type is IArrayTypeSymbol) return "Array element type is not supported for configuration binding";
        if (type is INamedTypeSymbol namedType2)
        {
            var typeName2 = namedType2.ToDisplayString();
            if (typeName2.StartsWith("System.Collections.Generic."))
                return "Collection element type is not supported for configuration binding";
        }

        return "requires parameterless constructor";
    }

    private static ConfigurationKeyValidationResult ValidateConfigurationKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return new ConfigurationKeyValidationResult { IsValid = false, ErrorMessage = "empty or whitespace-only" };
        if (string.IsNullOrWhiteSpace(key))
            return new ConfigurationKeyValidationResult { IsValid = false, ErrorMessage = "empty or whitespace-only" };
        if (key!.Contains("::"))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "contains double colons (::)"
            };
        if (key!.StartsWith(":") || key.EndsWith(":"))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "cannot start or end with a colon (:)"
            };
        if (key!.Any(c => c == '\0' || c == '\r' || c == '\n' || c == '\t'))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "contains invalid characters (null, carriage return, newline, or tab)"
            };
        return new ConfigurationKeyValidationResult { IsValid = true, ErrorMessage = string.Empty };
    }

    private struct ConfigurationKeyValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    ///     Validates that SupportsReloading is only used with Options pattern types
    /// </summary>
    private static void ValidateSupportsReloadingUsage(
        SourceProductionContext context,
        IFieldSymbol fieldSymbol,
        TypeDeclarationSyntax classDeclaration,
        AttributeData configAttribute)
    {
        // Check if SupportsReloading is set to true
        var supportsReloading = false;
        foreach (var kvp in configAttribute.NamedArguments)
        {
            if (kvp.Key == "SupportsReloading" && kvp.Value.Value is bool boolValue && boolValue)
            {
                supportsReloading = true;
                break;
            }
        }

        if (!supportsReloading) return;

        // Check if field type is a primitive (direct value binding)
        if (IsDirectValueBindingType(fieldSymbol.Type))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.SupportsReloadingOnPrimitiveType,
                classDeclaration.GetLocation(),
                fieldSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    ///     Determines if a type is a direct value binding type (primitive)
    ///     Primitives use GetValue<T>() while complex types use GetSection().Get<T>()
    /// </summary>
    private static bool IsDirectValueBindingType(ITypeSymbol type)
    {
        // Check for nullable types first
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            namedType.TypeArguments.Length == 1)
            return IsDirectValueBindingType(namedType.TypeArguments[0]);

        // Check for enum types
        if (type.TypeKind == TypeKind.Enum)
            return true;

        // Check for primitive types using SpecialType
        switch (type.SpecialType)
        {
            case SpecialType.System_String:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_Int16:
            case SpecialType.System_Byte:
            case SpecialType.System_Boolean:
            case SpecialType.System_Decimal:
            case SpecialType.System_Double:
            case SpecialType.System_Single:
                return true;
        }

        // Check by type name for non-SpecialType primitives
        var typeName = type.ToDisplayString();
        var metadataName = type.MetadataName;
        var namespaceAndMetadata = type.ContainingNamespace?.ToDisplayString() + "." + type.MetadataName;

        // Check all possible representations
        return typeName is "System.TimeSpan" or "System.DateTime" or "System.DateTimeOffset" or "System.Guid" or "System.Uri" ||
               metadataName is "TimeSpan" or "DateTime" or "DateTimeOffset" or "Guid" or "Uri" ||
               namespaceAndMetadata is "System.TimeSpan" or "System.DateTime" or "System.DateTimeOffset" or "System.Guid" or "System.Uri";
    }
}
