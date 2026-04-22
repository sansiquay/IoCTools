namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using IoCTools.Generator.Shared;

/// <summary>
/// IOC099: compilation-wide validator that fires when an
/// <see cref="IoCTools.Abstractions.Annotations.AutoDepsApplyAttribute{TProfile, TBase}"/> or
/// <see cref="IoCTools.Abstractions.Annotations.AutoDepsApplyGlobAttribute{TProfile}"/> rule
/// matches zero services in the local compilation. These rules are inert when nothing matches,
/// so the diagnostic flags them as stale for removal or adjustment.
/// </summary>
internal static class AutoDepsApplyMatchCountValidator
{
    internal static void Validate(SourceProductionContext context,
        Compilation compilation,
        DiagnosticConfiguration config)
    {
        if (!config.DiagnosticsEnabled) return;

        // Collect candidate services once. We use the compilation-level scan (same entry point
        // used by MissedOpportunityValidator and AutoDepsReporter) so referenced-assembly types
        // aren't included — IOC099 is about local rules matching local services.
        var localTypes = new List<INamedTypeSymbol>();
        DiagnosticScan.ScanNamespaceForTypes(compilation.Assembly.GlobalNamespace, localTypes);

        // Precompute service namespaces for glob matching.
        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            var cls = attribute.AttributeClass;
            if (cls is null) continue;
            var attrName = cls.Name;

            if (string.Equals(attrName, "AutoDepsApplyAttribute", StringComparison.Ordinal))
            {
                if (cls.TypeArguments.Length != 2) continue;
                if (cls.TypeArguments[1] is not ITypeSymbol tbase) continue;

                var matched = false;
                foreach (var type in localTypes)
                {
                    if (ServiceMatchesBase(type, tbase))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched) continue;

                var prof = cls.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                var baseDisp = tbase.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                var description = $"[assembly: AutoDepsApply<{prof}, {baseDisp}>]";

                var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                               ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AutoDepsApplyStale,
                    location,
                    description));
            }
            else if (string.Equals(attrName, "AutoDepsApplyGlobAttribute", StringComparison.Ordinal))
            {
                if (cls.TypeArguments.Length != 1) continue;
                if (attribute.ConstructorArguments.Length < 1) continue;
                if (attribute.ConstructorArguments[0].Value is not string pattern) continue;
                if (string.IsNullOrEmpty(pattern)) continue;

                // Invalid patterns are IOC103's domain; don't double-report as IOC099.
                _ = AutoDepsResolver.GlobMatch(string.Empty, pattern, out var invalid);
                if (invalid) continue;

                var matched = false;
                foreach (var type in localTypes)
                {
                    var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                    if (AutoDepsResolver.GlobMatch(ns, pattern, out _))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched) continue;

                var prof = cls.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                var description = $"[assembly: AutoDepsApplyGlob<{prof}>(\"{pattern}\")]";

                var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                               ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AutoDepsApplyStale,
                    location,
                    description));
            }
        }
    }

    private static bool ServiceMatchesBase(INamedTypeSymbol service, ITypeSymbol tbase)
    {
        // Walk base-class chain.
        INamedTypeSymbol? cur = service.BaseType;
        while (cur is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(cur.OriginalDefinition, tbase.OriginalDefinition))
                return true;
            cur = cur.BaseType;
        }

        // Walk implemented interfaces.
        foreach (var iface in service.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, tbase.OriginalDefinition))
                return true;
        }

        return false;
    }
}
