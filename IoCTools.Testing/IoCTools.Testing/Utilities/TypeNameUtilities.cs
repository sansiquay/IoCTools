namespace IoCTools.Testing.Utilities;

using System;
using System.Linq;
using Microsoft.CodeAnalysis;

internal static class TypeNameUtilities
{
    /// <summary>
    /// Generates a mock field name from a type, e.g., IUserRepository -> _mockUserRepository
    /// </summary>
    public static string GetMockFieldName(ITypeSymbol type)
    {
        var baseName = GetSimpleTypeName(type);
        return $"_mock{baseName}";
    }

    /// <summary>
    /// Generates a setup method name from a type, e.g., IUserRepository -> SetupUserRepository
    /// </summary>
    public static string GetSetupMethodName(ITypeSymbol type)
    {
        var baseName = GetSimpleTypeName(type);
        return $"Setup{baseName}";
    }

    /// <summary>
    /// Extracts a readable type name for mock/setup helpers, handling generic and interface types.
    /// </summary>
    private static string GetSimpleTypeName(ITypeSymbol type)
    {
        // Handle generic types: ILogger<UserService> -> LoggerUserService
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var genericArgs = string.Join(null, namedType.TypeArguments.Select(GetSimpleTypeName));
            var baseName = StripInterfacePrefix(namedType.Name);
            return $"{baseName}{genericArgs}";
        }

        // Handle interfaces: IUserRepository -> UserRepository
        var name = StripInterfacePrefix(type.Name);
        return name;
    }

    /// <summary>
    /// Removes common interface prefixes (I, II) from type names.
    /// </summary>
    private static string StripInterfacePrefix(string name)
    {
        if (name.StartsWith("II", StringComparison.Ordinal) && name.Length > 2 && char.IsUpper(name[2]))
            return name.Substring(1);
        if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
            return name.Substring(1);
        return name;
    }
}
