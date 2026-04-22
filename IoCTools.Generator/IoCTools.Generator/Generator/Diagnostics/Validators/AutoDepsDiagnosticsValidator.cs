namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System.Collections.Immutable;
using System.Linq;

using IoCTools.Generator.Shared;

/// <summary>
/// Per-service validator that materializes resolver-internal diagnostic signals
/// (IOC096, IOC098, IOC102, IOC105) into <see cref="Diagnostic"/> instances with proper
/// Locations. The resolver emits string-typed signals; this validator re-walks the service's
/// attributes to locate the right syntax nodes.
/// </summary>
internal static class AutoDepsDiagnosticsValidator
{
    internal static void Validate(SourceProductionContext context,
        Compilation compilation,
        INamedTypeSymbol serviceSymbol,
        TypeDeclarationSyntax classDeclaration,
        ImmutableDictionary<string, string> autoDepsOptions,
        DiagnosticConfiguration config)
    {
        if (!config.DiagnosticsEnabled) return;
        if (autoDepsOptions is null) return;

        // The kill switch short-circuits in the resolver too, but check here to avoid the
        // attribute walk and signal translation entirely.
        if (autoDepsOptions.TryGetValue("build_property.IoCToolsAutoDepsDisable", out var disabled)
            && string.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase))
            return;

        ImmutableArray<AutoDepDiagnosticSignal> signals;
        try
        {
            var resolverResult = AutoDepsResolver.ResolveForServiceWithDiagnostics(
                compilation, serviceSymbol, autoDepsOptions);
            signals = resolverResult.Signals;
        }
        catch
        {
            // Resolver exceptions are surfaced through the debug-report path; do not block the
            // rest of the diagnostics pipeline if the resolver trips over an ill-formed
            // attribute here.
            return;
        }

        if (signals.IsDefaultOrEmpty) return;

        foreach (var signal in signals)
        {
            switch (signal.Kind)
            {
                case AutoDepDiagnosticKind.RedundantProfile:
                    EmitRedundantProfile(context, serviceSymbol, classDeclaration, signal);
                    break;
                case AutoDepDiagnosticKind.DependsOnOverlap:
                    EmitDependsOnOverlap(context, serviceSymbol, classDeclaration, signal);
                    break;
                case AutoDepDiagnosticKind.StaleOptOut:
                    EmitStaleOptOut(context, serviceSymbol, classDeclaration, signal);
                    break;
                case AutoDepDiagnosticKind.OpenGenericConstraint:
                    EmitOpenGenericConstraint(context, compilation, classDeclaration, signal);
                    break;
            }
        }
    }

    private static void EmitRedundantProfile(SourceProductionContext context,
        INamedTypeSymbol serviceSymbol,
        TypeDeclarationSyntax classDeclaration,
        AutoDepDiagnosticSignal signal)
    {
        // IOC105 points at the class declaration — the redundancy is a declaration-level issue
        // (multiple rules attach the same profile to THIS service). Pointing at a specific
        // attribute would mislead users into thinking one of the rules is the bug.
        var location = classDeclaration.Identifier.GetLocation();

        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.RedundantProfileAttachment,
            location,
            signal.Arg0 ?? string.Empty,
            signal.Arg1 ?? string.Empty,
            signal.Arg2 ?? string.Empty);

        context.ReportDiagnostic(diagnostic);
    }

    private static void EmitDependsOnOverlap(SourceProductionContext context,
        INamedTypeSymbol serviceSymbol,
        TypeDeclarationSyntax classDeclaration,
        AutoDepDiagnosticSignal signal)
    {
        // Locate the matching [DependsOn<T>] attribute on the service. If present across multiple
        // partials, the first matching syntax node is acceptable (all partials' attributes see
        // the same overlap).
        var location = FindDependsOnLocation(serviceSymbol, signal.Arg0 ?? string.Empty)
                       ?? classDeclaration.Identifier.GetLocation();

        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.DependsOnAutoDepOverlap,
            location,
            signal.Arg0 ?? string.Empty,
            signal.Arg1 ?? string.Empty);

        context.ReportDiagnostic(diagnostic);
    }

    private static void EmitStaleOptOut(SourceProductionContext context,
        INamedTypeSymbol serviceSymbol,
        TypeDeclarationSyntax classDeclaration,
        AutoDepDiagnosticSignal signal)
    {
        // arg0 = type display, arg1 = attribute tag (e.g., "[NoAutoDep<Foo>]")
        // arg2 = service name
        var location = FindOptOutLocation(serviceSymbol, signal.Arg1 ?? string.Empty)
                       ?? classDeclaration.Identifier.GetLocation();

        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.NoAutoDepStale,
            location,
            signal.Arg0 ?? string.Empty,
            signal.Arg1 ?? string.Empty,
            signal.Arg2 ?? string.Empty);

        context.ReportDiagnostic(diagnostic);
    }

    private static void EmitOpenGenericConstraint(SourceProductionContext context,
        Compilation compilation,
        TypeDeclarationSyntax classDeclaration,
        AutoDepDiagnosticSignal signal)
    {
        // arg0 = open-generic display, arg1 = service name, arg2 = constraint, arg3 = open-generic name
        // The AutoDepOpen attribute is assembly-level; locate it by matching the unbound arg.
        var location = FindAutoDepOpenLocation(compilation, signal.Arg3 ?? string.Empty)
                       ?? classDeclaration.Identifier.GetLocation();

        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.AutoDepOpenConstraintViolation,
            location,
            signal.Arg0 ?? string.Empty,
            signal.Arg1 ?? string.Empty,
            signal.Arg2 ?? string.Empty,
            signal.Arg3 ?? string.Empty);

        context.ReportDiagnostic(diagnostic);
    }

    private static Location? FindDependsOnLocation(INamedTypeSymbol serviceSymbol, string typeDisplay)
    {
        foreach (var attr in serviceSymbol.GetAttributes())
        {
            var cls = attr.AttributeClass;
            if (cls is null) continue;
            if (cls.Name != "DependsOnAttribute") continue;
            foreach (var ta in cls.TypeArguments)
            {
                if (!string.Equals(ta.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), typeDisplay,
                        StringComparison.Ordinal))
                    continue;
                var loc = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation();
                if (loc is not null) return loc;
            }
        }
        return null;
    }

    private static Location? FindOptOutLocation(INamedTypeSymbol serviceSymbol, string attributeTag)
    {
        // attributeTag looks like "[NoAutoDep<Foo>]" or "[NoAutoDepOpen(typeof(IRepo<>))]".
        // Match on the core attribute-class name and the primary type arg spelling.
        // Strategy: strip surrounding brackets and match substrings.
        var tag = attributeTag.Trim('[', ']');
        foreach (var attr in serviceSymbol.GetAttributes())
        {
            var cls = attr.AttributeClass;
            if (cls is null) continue;
            var name = cls.Name;
            if (name != "NoAutoDepAttribute" && name != "NoAutoDepOpenAttribute") continue;

            // Prefer a match on the contained type-argument spelling (NoAutoDep<T>) or the ctor
            // argument type (NoAutoDepOpen(typeof(T<>))).
            bool matches = false;
            if (name == "NoAutoDepAttribute" && cls.TypeArguments.Length == 1)
            {
                var tspell = cls.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                matches = tag.IndexOf(tspell, StringComparison.Ordinal) >= 0;
            }
            else if (name == "NoAutoDepOpenAttribute" && attr.ConstructorArguments.Length == 1 &&
                     attr.ConstructorArguments[0].Value is ITypeSymbol open)
            {
                var tspell = open.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                matches = tag.IndexOf(tspell, StringComparison.Ordinal) >= 0;
            }

            if (!matches) continue;
            var loc = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation();
            if (loc is not null) return loc;
        }
        return null;
    }

    private static Location? FindAutoDepOpenLocation(Compilation compilation, string openGenericName)
    {
        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            var cls = attr.AttributeClass;
            if (cls is null) continue;
            if (cls.Name != "AutoDepOpenAttribute") continue;
            if (attr.ConstructorArguments.Length == 0) continue;

            if (attr.ConstructorArguments[0].Value is INamedTypeSymbol unbound)
            {
                var shortName = unbound.Name;
                if (!string.Equals(shortName, openGenericName, StringComparison.Ordinal)) continue;
                var loc = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation();
                if (loc is not null) return loc;
            }
        }
        return null;
    }
}
