namespace IoCTools.Generator.Shared;

using System;
using Microsoft.CodeAnalysis;

public readonly struct SymbolIdentity : IEquatable<SymbolIdentity>
{
    public SymbolIdentity(string metadataName, string containingAssemblyName)
    {
        MetadataName = metadataName;
        ContainingAssemblyName = containingAssemblyName;
    }

    public string MetadataName { get; }
    public string ContainingAssemblyName { get; }

    public static SymbolIdentity From(ITypeSymbol symbol) =>
        new SymbolIdentity(
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            symbol.ContainingAssembly?.Identity.Name ?? string.Empty);

    public bool Equals(SymbolIdentity other) =>
        string.Equals(MetadataName, other.MetadataName, StringComparison.Ordinal) &&
        string.Equals(ContainingAssemblyName, other.ContainingAssemblyName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SymbolIdentity other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (MetadataName.GetHashCode() * 397) ^ ContainingAssemblyName.GetHashCode();
        }
    }
}
