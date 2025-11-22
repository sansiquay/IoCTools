namespace IoCTools.Generator.Utilities;

internal static class DependencySetUtilities
{
    private const string DependencySetMetadataName = "IoCTools.Abstractions.IDependencySet";

    public static bool IsDependencySet(INamedTypeSymbol symbol)
    {
        if (symbol == null) return false;

        if (symbol.ToDisplayString() == DependencySetMetadataName) return true;

        return symbol.AllInterfaces.Any(i => i.ToDisplayString() == DependencySetMetadataName);
    }

    public static INamedTypeSymbol? GetDependencySetInterface(Compilation compilation) =>
        compilation.GetTypeByMetadataName(DependencySetMetadataName);
}
