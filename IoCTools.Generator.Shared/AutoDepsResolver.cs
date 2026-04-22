namespace IoCTools.Generator.Shared;

using System;
using Microsoft.CodeAnalysis;

public static partial class AutoDepsResolver
{
    private const string MelIloggerMetadataName = "Microsoft.Extensions.Logging.ILogger`1";

    public static bool IsBuiltinILoggerAvailable(Compilation compilation)
    {
        if (compilation == null) throw new ArgumentNullException(nameof(compilation));
        return compilation.GetTypeByMetadataName(MelIloggerMetadataName) is { };
    }

    internal static INamedTypeSymbol? GetBuiltinILoggerSymbol(Compilation compilation) =>
        compilation.GetTypeByMetadataName(MelIloggerMetadataName);
}
