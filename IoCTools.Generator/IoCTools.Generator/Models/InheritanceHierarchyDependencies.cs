using IoCTools.Generator.Utilities;

namespace IoCTools.Generator.Models;

internal class InheritanceHierarchyDependencies(
    List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> allDependencies,
    List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> baseDependencies,
    List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> derivedDependencies,
    List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)> rawAllDependencies,
    List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)>
        allDependenciesWithExternalFlag)
{
    public List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> AllDependencies { get; } =
        allDependencies;

    public List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> BaseDependencies { get; } =
        baseDependencies;

    public List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> DerivedDependencies { get; } =
        derivedDependencies;

    public List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)> RawAllDependencies
    {
        get;
    } = rawAllDependencies;

    public List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)>
        AllDependenciesWithExternalFlag
    { get; } = allDependenciesWithExternalFlag;
}
