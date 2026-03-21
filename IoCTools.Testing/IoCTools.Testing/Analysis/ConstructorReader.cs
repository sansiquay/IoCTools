namespace IoCTools.Testing.Analysis;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

internal static class ConstructorReader
{
    /// <summary>
    /// Reads the generated constructor parameters for a service class.
    /// Returns constructor parameters ordered as they appear in the constructor signature.
    /// </summary>
    public static ImmutableArray<IParameterSymbol> GetConstructorParameters(INamedTypeSymbol serviceSymbol)
    {
        // Find the most-derived constructor (not from base types)
        var constructors = serviceSymbol.Constructors
            .Where(c => !c.IsStatic && c.Parameters.Length > 0)
            .ToImmutableArray();

        if (constructors.Length == 0)
            return ImmutableArray<IParameterSymbol>.Empty;

        // Prefer constructor with GeneratedCodeAttribute (our generated constructor)
        var generatedConstructor = constructors.FirstOrDefault(c =>
            c.GetAttributes().Any(a =>
                a?.AttributeClass?.Name == "GeneratedCodeAttribute" ||
                a?.AttributeClass?.ToDisplayString().Contains("System.CodeDom.Compiler") == true));

        if (generatedConstructor != null)
            return generatedConstructor.Parameters;

        // Fallback: use the constructor with the most parameters (likely the generated one)
        return constructors
            .OrderByDescending(c => c.Parameters.Length)
            .First()
            .Parameters;
    }

    /// <summary>
    /// Determines if a parameter is configuration-related (IConfiguration, IOptions<T>, IOptionsSnapshot<T>, IOptionsMonitor<T>)
    /// </summary>
    public static bool IsConfigurationParameter(IParameterSymbol parameter)
    {
        var typeName = parameter.Type.ToDisplayString();
        return typeName == "Microsoft.Extensions.Configuration.IConfiguration" ||
               typeName.StartsWith("Microsoft.Extensions.Options.IOptions<") ||
               typeName.StartsWith("Microsoft.Extensions.Options.IOptionsSnapshot<") ||
               typeName.StartsWith("Microsoft.Extensions.Options.IOptionsMonitor<");
    }

    /// <summary>
    /// Gets the options type T from IOptions<T>, IOptionsSnapshot<T>, or IOptionsMonitor<T>
    /// </summary>
    public static ITypeSymbol? GetOptionsType(IParameterSymbol parameter)
    {
        if (parameter.Type is INamedTypeSymbol namedType &&
            (namedType.Name == "IOptions" || namedType.Name == "IOptionsSnapshot" || namedType.Name == "IOptionsMonitor") &&
            namedType.TypeArguments.Length > 0)
        {
            return namedType.TypeArguments[0];
        }
        return null;
    }
}
