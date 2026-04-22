namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using IoCTools.Generator.Shared;

/// <summary>
///     IOC103: validates the glob pattern ctor argument of
///     <see cref="IoCTools.Abstractions.Annotations.AutoDepsApplyGlobAttribute{T}" />. Fires when the
///     pattern is null, empty, or malformed (e.g., unterminated character class).
/// </summary>
internal static class AutoDepsApplyGlobPatternValidator
{
    internal static void Validate(SourceProductionContext context,
        Compilation compilation,
        DiagnosticConfiguration config)
    {
        if (!config.DiagnosticsEnabled) return;

        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            var name = attribute.AttributeClass?.Name;
            if (!string.Equals(name, "AutoDepsApplyGlobAttribute", StringComparison.Ordinal)) continue;

            if (attribute.ConstructorArguments.Length == 0) continue;

            var arg = attribute.ConstructorArguments[0];
            var pattern = arg.Value as string;

            // Probe the pattern using the shared glob implementation. A null/empty pattern or a malformed
            // regex (e.g., "[unterminated") will flip patternIsInvalid.
            _ = AutoDepsResolver.GlobMatch(string.Empty, pattern ?? string.Empty, out var patternIsInvalid);
            if (!patternIsInvalid) continue;

            var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.AutoDepsApplyGlobInvalid,
                location,
                pattern ?? "<null>");
            context.ReportDiagnostic(diagnostic);
        }
    }
}
