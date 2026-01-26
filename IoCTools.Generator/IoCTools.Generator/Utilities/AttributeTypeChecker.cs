namespace IoCTools.Generator.Utilities;

using Microsoft.CodeAnalysis;
using System.Linq;

internal static class AttributeTypeChecker
{
    public const string ScopedAttribute = "IoCTools.Abstractions.Annotations.ScopedAttribute";
    public const string SingletonAttribute = "IoCTools.Abstractions.Annotations.SingletonAttribute";
    public const string TransientAttribute = "IoCTools.Abstractions.Annotations.TransientAttribute";
    public const string InjectAttribute = "IoCTools.Abstractions.Annotations.InjectAttribute";
    public const string RegisterAsAllAttribute = "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute";
    public const string RegisterAsAttribute = "IoCTools.Abstractions.Annotations.RegisterAsAttribute";
    public const string SkipRegistrationAttribute = "IoCTools.Abstractions.Annotations.SkipRegistrationAttribute";
    public const string ConditionalServiceAttribute = "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute";
    public const string ExternalServiceAttribute = "IoCTools.Abstractions.Annotations.ExternalServiceAttribute";
    public const string DependsOnConfigurationAttributeBase = "IoCTools.Abstractions.Annotations.DependsOnConfigurationAttributeBase";
    public const string DependsOnConfigurationAttributeGeneric = "IoCTools.Abstractions.Annotations.DependsOnConfigurationAttribute<";
    public const string DependencySetAttribute = "IoCTools.Abstractions.Annotations.DependencySetAttribute";
    public const string InjectConfigurationAttribute = "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute";
    
    // Interfaces
    public const string DependencySetInterface = "IoCTools.Abstractions.IDependencySet";
    
    // Microsoft Attributes/Interfaces
    public const string MicrosoftOptions = "Microsoft.Extensions.Options";
    public const string MicrosoftHostedService = "Microsoft.Extensions.Hosting.IHostedService";
    public const string MicrosoftBackgroundService = "Microsoft.Extensions.Hosting.BackgroundService";

    /// <summary>
    /// Checks if the attribute matches the specified type name using string comparison.
    /// This is the fallback method when Compilation/Symbols are not readily available for comparison.
    /// </summary>
    public static bool IsAttribute(AttributeData? attribute, string fullTypeName)
    {
        return attribute?.AttributeClass?.ToDisplayString() == fullTypeName;
    }

    /// <summary>
    /// Checks if the attribute matches any of the specified type names.
    /// </summary>
    public static bool IsAttribute(AttributeData? attribute, params string[] fullTypeNames)
    {
        if (attribute?.AttributeClass == null) return false;
        var displayString = attribute.AttributeClass.ToDisplayString();
        return fullTypeNames.Contains(displayString);
    }
    
    /// <summary>
    /// Checks if a symbol matches a specific type name (for interfaces, base classes, etc.)
    /// </summary>
    public static bool IsType(ITypeSymbol? symbol, string fullTypeName)
    {
        return symbol?.ToDisplayString() == fullTypeName;
    }
    
    /// <summary>
    /// Checks if a symbol matches a specific type name using SymbolEqualityComparer if target is provided.
    /// </summary>
    public static bool IsType(ITypeSymbol? symbol, INamedTypeSymbol? targetType)
    {
        if (symbol == null || targetType == null) return false;
        return SymbolEqualityComparer.Default.Equals(symbol, targetType);
    }

    /// <summary>
    /// Checks if an attribute is a RegisterAsAttribute of any arity.
    /// RegisterAsAttribute is generic (RegisterAsAttribute&lt;T&gt;, RegisterAsAttribute&lt;T1,T2&gt;, etc.)
    /// so we check the name matches exactly and verify it's a generic type.
    /// </summary>
    public static bool IsRegisterAsAttribute(AttributeData? attribute)
    {
        if (attribute?.AttributeClass == null) return false;

        // Name property returns the metadata name without arity for generic types
        // So RegisterAsAttribute<T1> has Name = "RegisterAsAttribute"
        var nameMatches = attribute.AttributeClass.Name == "RegisterAsAttribute";
        var isGeneric = attribute.AttributeClass.IsGenericType;

        return nameMatches && isGeneric;
    }

    /// <summary>
    /// Checks if an attribute is a SkipRegistrationAttribute (non-generic or generic).
    /// </summary>
    public static bool IsSkipRegistrationAttribute(AttributeData? attribute)
    {
        if (attribute?.AttributeClass == null) return false;

        var displayString = attribute.AttributeClass.ToDisplayString();

        // Check for non-generic SkipRegistrationAttribute
        if (displayString == SkipRegistrationAttribute)
            return true;

        // Check for generic SkipRegistrationAttribute<T>
        if (displayString.StartsWith(SkipRegistrationAttribute, StringComparison.Ordinal) &&
            attribute.AttributeClass.IsGenericType)
            return true;

        return false;
    }

    /// <summary>
    /// Checks if an attribute is the non-generic SkipRegistrationAttribute.
    /// </summary>
    public static bool IsNonGenericSkipRegistrationAttribute(AttributeData? attribute)
    {
        return attribute?.AttributeClass?.ToDisplayString() == SkipRegistrationAttribute;
    }

    /// <summary>
    /// Checks if an attribute is a generic SkipRegistrationAttribute.
    /// </summary>
    public static bool IsGenericSkipRegistrationAttribute(AttributeData? attribute)
    {
        if (attribute?.AttributeClass == null) return false;

        return attribute.AttributeClass.ToDisplayString()
                   .StartsWith(SkipRegistrationAttribute, StringComparison.Ordinal) &&
               attribute.AttributeClass.IsGenericType;
    }
}
