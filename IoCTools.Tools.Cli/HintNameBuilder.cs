namespace IoCTools.Tools.Cli;

using Microsoft.CodeAnalysis;
using System;

internal static class HintNameBuilder
{
    public static string GetConstructorHint(INamedTypeSymbol symbol)
    {
        var canonical = symbol.ToDisplayString();
        var sanitized = Sanitize(canonical);
        return $"{sanitized}_Constructor.g.cs";
    }

    public static string GetExtensionHint(Project project)
    {
        var assemblyName = project.AssemblyName ?? "UnknownAssembly";
        var safeRoot = assemblyName.Replace("-", "_").Replace(" ", "_");
        var safeAssemblyName = safeRoot.Replace(".", string.Empty);
        return $"ServiceRegistrations_{safeAssemblyName}.g.cs";
    }

    public static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "_";
        }

        var trimmed = input.Trim();
        while (trimmed.EndsWith("."))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "_";
        }

        var chars = trimmed.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c < 32 ||
                c == '<' || c == '>' ||
                c == '.' || c == ',' || c == ' ' ||
                c == ':' || c == '|' || c == '?' || c == '*' || c == '"' || c == '/' || c == '\\')
            {
                chars[i] = '_';
            }
        }

        var result = new string(chars);

        if (IsReserved(result))
        {
            return "_" + result;
        }

        return result;
    }

    private static bool IsReserved(string name)
    {
        var upper = name.ToUpperInvariant();
        var reserved = new[] { "CON", "PRN", "AUX", "NUL",
                               "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                               "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        foreach (var r in reserved)
        {
            if (upper == r) return true;
        }
        return false;
    }
}