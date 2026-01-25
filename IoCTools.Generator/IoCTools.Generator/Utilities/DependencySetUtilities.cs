namespace IoCTools.Generator.Utilities;

using Microsoft.CodeAnalysis;
using System.Linq;

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