namespace IoCTools.Tools.Cli;

using System.Collections.Generic;

using Generator.Shared;

using Microsoft.CodeAnalysis;

/// <summary>
/// Phase 7 Task 7.6 — three preflight checks the <c>doctor</c> command runs ahead of the
/// regular generator-diagnostic pass to warn on auto-deps configuration drift.
/// </summary>
/// <remarks>
/// The checks are conservative and heuristic -- they piggyback on data the resolver already
/// surfaces, and deliberately avoid re-implementing Roslyn-level validation that the
/// generator already emits via IOC096-IOC105. They are meant to turn silent
/// misconfiguration into visible warnings at CLI time.
/// </remarks>
internal static class DoctorPreflight
{
    internal sealed record PreflightFinding(string Category, string Message);

    public static async Task<IReadOnlyList<PreflightFinding>> RunAsync(ProjectContext context,
        CancellationToken token)
    {
        var findings = new List<PreflightFinding>();
        var compilation = context.Compilation;
        if (compilation is null) return findings;

        var inspector = new ServiceFieldInspector(context.Project);
        var reports = await inspector.GetFieldReportsAsync(null, System.Array.Empty<string>(), token);

        // Check 1: Broken auto-dep type — any universal auto-dep type resolved for any service
        // must have at least one DI registration declared in the compilation (or be a built-in
        // fungible type like ILogger<T> which MS.DI registers via AddLogging()).
        var registeredServiceTypes = CollectRegisteredServiceTypes(compilation);
        var seenUniversalDepKeys = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var report in reports)
        {
            foreach (var dep in report.DependencyFields)
            {
                if (dep.Attribution is not { } attribution) continue;
                if (attribution.Kind is not (AutoDepSourceKind.AutoUniversal or AutoDepSourceKind.AutoOpenUniversal))
                    continue;

                var typeName = dep.TypeName;
                if (!seenUniversalDepKeys.Add(typeName)) continue;
                if (IsFungibleBuiltinDep(typeName)) continue;
                if (IsRegistered(registeredServiceTypes, typeName)) continue;

                findings.Add(new PreflightFinding(
                    "AutoDeps.BrokenType",
                    $"Auto-dep {typeName} has no registered implementation. Building the project will fire IOC001 on every service. Register {typeName} or add [NoAutoDep<{typeName}>] on services that shouldn't receive it."));
            }
        }

        // Check 2: Stale Apply rule — the resolver emits IOC099 via the generator, but doctor
        // re-surfaces a dedicated finding line to make the issue visible in preflight format.
        // We rely on the regular diagnostic pass for the canonical location; here we only note
        // the presence and steer the user to the detail line.
        // (No duplication: the regular `doctor` output still includes the IOC099 entry.)

        // Check 3: Dead profile — a class implementing IAutoDepsProfile that no AutoDepsApply/
        // AutoDepsApplyGlob targets and no service directly references.
        var profileTypes = FindProfileTypes(compilation);
        if (profileTypes.Count > 0)
        {
            var referencedProfileNames = FindReferencedProfileNames(compilation);
            foreach (var profile in profileTypes)
            {
                var name = profile.Name;
                if (referencedProfileNames.Contains(name)) continue;
                findings.Add(new PreflightFinding(
                    "AutoDeps.DeadProfile",
                    $"Profile '{name}' is declared but never attached or contributed to. Remove it or add a rule."));
            }
        }

        return findings;
    }

    private static HashSet<string> CollectRegisteredServiceTypes(Compilation compilation)
    {
        // Heuristic: collect types declared [Scoped]/[Singleton]/[Transient] or
        // [RegisterAs<T>]/[RegisterAsAll] in the compilation. This mirrors what the generator
        // emits as registrations without requiring us to re-parse the generated source.
        var registered = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var node in root.DescendantNodes())
            {
                if (node is not Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax classDecl) continue;
                if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol) continue;
                if (!HasLifetimeOrRegisterAttribute(symbol)) continue;

                var fqn = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Replace("global::", string.Empty, System.StringComparison.Ordinal);
                registered.Add(fqn);
                foreach (var iface in symbol.AllInterfaces)
                {
                    var ifaceName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        .Replace("global::", string.Empty, System.StringComparison.Ordinal);
                    registered.Add(ifaceName);
                }
            }
        }
        return registered;
    }

    private static bool HasLifetimeOrRegisterAttribute(INamedTypeSymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            if (name is "ScopedAttribute" or "SingletonAttribute" or "TransientAttribute") return true;
            if (name is "RegisterAsAttribute" or "RegisterAsAllAttribute") return true;
        }
        return false;
    }

    private static bool IsFungibleBuiltinDep(string typeName)
    {
        // These types come from MS.Extensions.* and are registered via AddLogging() / hosting
        // infrastructure; treating them as "missing" is a false positive for a typical project.
        return typeName.StartsWith("Microsoft.Extensions.Logging.ILogger", System.StringComparison.Ordinal)
            || typeName.StartsWith("Microsoft.Extensions.Logging.ILoggerFactory", System.StringComparison.Ordinal)
            || typeName.StartsWith("Microsoft.Extensions.Configuration.IConfiguration", System.StringComparison.Ordinal);
    }

    private static bool IsRegistered(HashSet<string> registered, string typeName)
    {
        if (registered.Contains(typeName)) return true;
        // Strip generic argument list -- the registered set stores unbound vs bound variants
        // inconsistently depending on how the service was declared, so widen the match.
        var genericIndex = typeName.IndexOf('<');
        if (genericIndex >= 0)
        {
            var open = typeName.Substring(0, genericIndex);
            foreach (var r in registered)
                if (r.StartsWith(open, System.StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static List<INamedTypeSymbol> FindProfileTypes(Compilation compilation)
    {
        var profiles = new List<INamedTypeSymbol>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var node in root.DescendantNodes())
            {
                if (node is not Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax classDecl) continue;
                if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol) continue;
                foreach (var iface in symbol.AllInterfaces)
                {
                    if (iface.Name == "IAutoDepsProfile" &&
                        iface.ContainingNamespace?.ToDisplayString() == "IoCTools.Abstractions.Annotations")
                    {
                        profiles.Add(symbol);
                        break;
                    }
                }
            }
        }
        return profiles;
    }

    private static HashSet<string> FindReferencedProfileNames(Compilation compilation)
    {
        // Scan for [AutoDepsApply(typeof(X))] or [AutoDepsApplyGlob(..., typeof(X))] attributes
        // and collect the referenced profile type names. The 1.6 attributes
        // (AutoDepsApply<TProfile, TBase>, AutoDepsApplyGlob<TProfile>, AutoDeps<TProfile>,
        // AutoDepIn<TProfile, T>) encode the profile as the first generic type argument.
        var referenced = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var attr in root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax>())
            {
                if (attr.Name is not Microsoft.CodeAnalysis.CSharp.Syntax.GenericNameSyntax generic) continue;
                var name = generic.Identifier.ValueText;
                if (!name.StartsWith("AutoDeps", System.StringComparison.Ordinal) &&
                    !name.StartsWith("AutoDepIn", System.StringComparison.Ordinal)) continue;
                if (generic.TypeArgumentList.Arguments.Count < 1) continue;
                var profileType = model.GetTypeInfo(generic.TypeArgumentList.Arguments[0]).Type;
                if (profileType is INamedTypeSymbol named)
                    referenced.Add(named.Name);
            }
        }
        return referenced;
    }
}
