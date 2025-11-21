namespace IoCTools.Tools.Cli;

using Microsoft.CodeAnalysis;

internal static class HintNameBuilder
{
    public static string GetConstructorHint(INamedTypeSymbol symbol)
    {
        var canonical = symbol.ToDisplayString();
        var sanitized = canonical.Replace("<", "_")
            .Replace(">", "_")
            .Replace(".", "_")
            .Replace(",", "_")
            .Replace(" ", "_");
        return $"{sanitized}_Constructor.g.cs";
    }

    public static string GetExtensionHint(Project project)
    {
        var assemblyName = project.AssemblyName ?? "UnknownAssembly";
        var safeRoot = assemblyName.Replace("-", "_").Replace(" ", "_");
        var safeAssemblyName = safeRoot.Replace(".", string.Empty);
        return $"ServiceRegistrations_{safeAssemblyName}.g.cs";
    }
}
