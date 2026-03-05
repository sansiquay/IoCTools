namespace IoCTools.Generator.Utilities;

using System.Linq;

using Microsoft.CodeAnalysis;

internal static class DependencySetUtilities
{
    public static bool IsDependencySet(INamedTypeSymbol symbol)
    {
        if (symbol == null) return false;

        if (AttributeTypeChecker.IsType(symbol, AttributeTypeChecker.DependencySetInterface)) return true;

        return symbol.AllInterfaces.Any(i => AttributeTypeChecker.IsType(i, AttributeTypeChecker.DependencySetInterface));
    }

    public static INamedTypeSymbol? GetDependencySetInterface(Compilation compilation) =>
        compilation.GetTypeByMetadataName(AttributeTypeChecker.DependencySetInterface);
}
