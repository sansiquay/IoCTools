namespace IoCTools.Tools.Cli;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using CommandLine;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Phase 7 Task 7.8 — implements the <c>profiles</c> (plural) subcommand which inspects
/// auto-deps profiles declared in the project. Distinct from the existing <c>profile</c>
/// (singular) command which benchmarks project load.
/// </summary>
/// <remarks>
/// A "profile" is a class implementing
/// <c>IoCTools.Abstractions.Annotations.IAutoDepsProfile</c>. For each profile we enumerate:
/// <list type="bullet">
/// <item>contributions via <c>[assembly: AutoDepIn&lt;TProfile, T&gt;]</c>,</item>
/// <item>service matches via <c>[assembly: AutoDepsApply&lt;TProfile, TBase&gt;]</c>,
///       <c>[assembly: AutoDepsApplyGlob&lt;TProfile&gt;(...)]</c>, and
///       <c>[AutoDeps&lt;TProfile&gt;]</c> on individual services.</item>
/// </list>
/// The compilation passed in has already had generated syntax trees stripped by the
/// runner, so attribute scans see only user-authored source.
/// </remarks>
internal static class ProfilesPrinter
{
    public static int Print(Compilation compilation, ProfilesCommandOptions opts, OutputContext output)
    {
        var profiles = FindProfileTypes(compilation);

        if (opts.ProfileName is not null)
        {
            var (candidates, selected) = ResolveProfileName(profiles, opts.ProfileName);
            if (selected is null)
            {
                if (candidates.Count == 0)
                {
                    if (string.Equals(opts.Format, "json", System.StringComparison.OrdinalIgnoreCase))
                    {
                        output.WriteJson(new
                        {
                            error = $"No profile matching '{opts.ProfileName}'.",
                            candidates = System.Array.Empty<string>()
                        });
                    }
                    output.WriteLine($"No profile matching '{opts.ProfileName}'.");
                    return 1;
                }

                if (string.Equals(opts.Format, "json", System.StringComparison.OrdinalIgnoreCase))
                {
                    output.WriteJson(new
                    {
                        error = $"Ambiguous profile name '{opts.ProfileName}'.",
                        candidates = candidates.Select(FullyQualifiedName).ToArray()
                    });
                }
                output.WriteLine($"Ambiguous profile name '{opts.ProfileName}'. Candidates:");
                foreach (var c in candidates)
                    output.WriteLine($"  {FullyQualifiedName(c)}");
                return 1;
            }

            PrintProfileDetail(compilation, selected, opts, output);
            return 0;
        }

        PrintProfileList(compilation, profiles, opts, output);
        return 0;
    }

    private static void PrintProfileList(Compilation compilation,
        IReadOnlyList<INamedTypeSymbol> profiles,
        ProfilesCommandOptions opts,
        OutputContext output)
    {
        if (profiles.Count == 0)
        {
            if (string.Equals(opts.Format, "json", System.StringComparison.OrdinalIgnoreCase))
                output.WriteJson(new { profiles = System.Array.Empty<object>() });
            output.WriteLine("No auto-deps profiles found in this project.");
            return;
        }

        var rows = profiles.Select(p => BuildRow(compilation, p, opts.ShowMatches)).ToList();

        if (string.Equals(opts.Format, "json", System.StringComparison.OrdinalIgnoreCase))
        {
            output.WriteJson(new
            {
                profiles = rows.Select(r => r.ShowMatches
                    ? (object)new { name = r.Name, fullyQualifiedName = r.Fqn, deps = r.Deps, matches = r.Matches }
                    : new { name = r.Name, fullyQualifiedName = r.Fqn, deps = r.Deps }).ToArray()
            });
            return;
        }

        output.WriteLine($"Profiles ({profiles.Count}):");
        foreach (var row in rows)
        {
            output.WriteLine(string.Empty);
            output.WriteLine($"  {row.Name}  ({row.Fqn})");
            if (row.Deps.Count == 0)
                output.WriteLine("    deps: (none)");
            else
            {
                output.WriteLine("    deps:");
                foreach (var dep in row.Deps)
                    output.WriteLine($"      - {dep}");
            }

            if (opts.ShowMatches)
            {
                if (row.Matches.Count == 0)
                    output.WriteLine("    matches: (none)");
                else
                {
                    output.WriteLine("    matches:");
                    foreach (var match in row.Matches)
                        output.WriteLine($"      - {match}");
                }
            }
        }
    }

    private static void PrintProfileDetail(Compilation compilation,
        INamedTypeSymbol profile,
        ProfilesCommandOptions opts,
        OutputContext output)
    {
        var deps = CollectContributions(compilation, profile);
        var (matches, attachmentSources) = CollectMatchesAndSources(compilation, profile);

        if (string.Equals(opts.Format, "json", System.StringComparison.OrdinalIgnoreCase))
        {
            output.WriteJson(new
            {
                profile = profile.Name,
                fullyQualifiedName = FullyQualifiedName(profile),
                deps,
                matches,
                attachmentSources
            });
            return;
        }

        output.WriteLine($"Profile: {profile.Name}");
        output.WriteLine($"  FQN: {FullyQualifiedName(profile)}");

        if (deps.Count == 0)
            output.WriteLine("  deps: (none)");
        else
        {
            output.WriteLine("  deps:");
            foreach (var dep in deps)
                output.WriteLine($"    - {dep}");
        }

        if (matches.Count == 0)
            output.WriteLine("  matches: (none)");
        else
        {
            output.WriteLine("  matches:");
            foreach (var match in matches)
                output.WriteLine($"    - {match}");
        }

        if (attachmentSources.Count == 0)
            output.WriteLine("  attachmentSources: (none)");
        else
        {
            output.WriteLine("  attachmentSources:");
            foreach (var src in attachmentSources)
                output.WriteLine($"    - {src}");
        }
    }

    private readonly struct ProfileRow
    {
        public ProfileRow(string name, string fqn, List<string> deps, List<string> matches, bool showMatches)
        {
            Name = name;
            Fqn = fqn;
            Deps = deps;
            Matches = matches;
            ShowMatches = showMatches;
        }

        public string Name { get; }
        public string Fqn { get; }
        public List<string> Deps { get; }
        public List<string> Matches { get; }
        public bool ShowMatches { get; }
    }

    private static ProfileRow BuildRow(Compilation compilation, INamedTypeSymbol profile, bool showMatches)
    {
        var deps = CollectContributions(compilation, profile);
        var matches = showMatches ? CollectMatchesAndSources(compilation, profile).matches : new List<string>();
        return new ProfileRow(profile.Name, FullyQualifiedName(profile), deps, matches, showMatches);
    }

    private static List<string> CollectContributions(Compilation compilation, INamedTypeSymbol profile)
    {
        // Walk [assembly: AutoDepIn<TProfile, T>] attributes; collect T names when TProfile matches.
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        var deps = new List<string>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var attr in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                var name = attr.Name.ToString();
                // Generic attribute names come through as `AutoDepIn<Profile, Dep>`.
                if (!name.Contains("AutoDepIn", System.StringComparison.Ordinal)) continue;
                if (attr.Name is not GenericNameSyntax generic || generic.TypeArgumentList.Arguments.Count < 2) continue;

                var profileArg = generic.TypeArgumentList.Arguments[0];
                var depArg = generic.TypeArgumentList.Arguments[1];
                var profileType = model.GetTypeInfo(profileArg).Type as INamedTypeSymbol;
                if (profileType is null || !SymbolEqualityComparer.Default.Equals(profileType, profile)) continue;

                var depType = model.GetTypeInfo(depArg).Type as INamedTypeSymbol;
                var depName = depType?.Name ?? depArg.ToString();
                if (seen.Add(depName)) deps.Add(depName);
            }
        }

        deps.Sort(System.StringComparer.Ordinal);
        return deps;
    }

    private static (List<string> matches, List<string> attachmentSources) CollectMatchesAndSources(
        Compilation compilation,
        INamedTypeSymbol profile)
    {
        // Strategy:
        //  - AutoDepsApply<TProfile, TBase>  -> every class in the compilation assignable to TBase.
        //  - AutoDepsApplyGlob<TProfile>("…") -> every class whose FQN matches the glob.
        //  - [AutoDeps<TProfile>] on a class  -> that class directly.
        var matches = new List<string>();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        var sources = new List<string>();

        var allClasses = CollectCandidateServices(compilation);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var attr in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                var name = attr.Name.ToString();
                if (attr.Name is not GenericNameSyntax generic) continue;

                if (name.Contains("AutoDepsApplyGlob", System.StringComparison.Ordinal))
                {
                    if (generic.TypeArgumentList.Arguments.Count < 1) continue;
                    var profileType = model.GetTypeInfo(generic.TypeArgumentList.Arguments[0]).Type as INamedTypeSymbol;
                    if (profileType is null || !SymbolEqualityComparer.Default.Equals(profileType, profile)) continue;

                    var pattern = ExtractStringArg(attr);
                    if (pattern is null) continue;
                    sources.Add($"[assembly: AutoDepsApplyGlob<{profile.Name}>(\"{pattern}\")]");
                    foreach (var cls in allClasses)
                    {
                        var fqn = FullyQualifiedName(cls);
                        if (GlobMatch(fqn, pattern) && seen.Add(fqn))
                            matches.Add(cls.Name);
                    }
                }
                else if (name.Contains("AutoDepsApply", System.StringComparison.Ordinal))
                {
                    if (generic.TypeArgumentList.Arguments.Count < 2) continue;
                    var profileType = model.GetTypeInfo(generic.TypeArgumentList.Arguments[0]).Type as INamedTypeSymbol;
                    if (profileType is null || !SymbolEqualityComparer.Default.Equals(profileType, profile)) continue;

                    var baseType = model.GetTypeInfo(generic.TypeArgumentList.Arguments[1]).Type as INamedTypeSymbol;
                    if (baseType is null) continue;
                    sources.Add($"[assembly: AutoDepsApply<{profile.Name}, {baseType.Name}>]");
                    foreach (var cls in allClasses)
                    {
                        if (!IsAssignableTo(cls, baseType)) continue;
                        var fqn = FullyQualifiedName(cls);
                        if (seen.Add(fqn)) matches.Add(cls.Name);
                    }
                }
                else if (name.Contains("AutoDeps", System.StringComparison.Ordinal) &&
                         !name.Contains("AutoDepIn", System.StringComparison.Ordinal))
                {
                    // [AutoDeps<TProfile>] applied directly to a class.
                    if (generic.TypeArgumentList.Arguments.Count < 1) continue;
                    var profileType = model.GetTypeInfo(generic.TypeArgumentList.Arguments[0]).Type as INamedTypeSymbol;
                    if (profileType is null || !SymbolEqualityComparer.Default.Equals(profileType, profile)) continue;

                    // Find the enclosing class declaration.
                    var classDecl = attr.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                    if (classDecl is null) continue;
                    var classSymbol = model.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                    if (classSymbol is null) continue;

                    var fqn = FullyQualifiedName(classSymbol);
                    sources.Add($"[AutoDeps<{profile.Name}>] on {classSymbol.Name}");
                    if (seen.Add(fqn)) matches.Add(classSymbol.Name);
                }
            }
        }

        matches.Sort(System.StringComparer.Ordinal);
        return (matches, sources);
    }

    private static string? ExtractStringArg(AttributeSyntax attr)
    {
        if (attr.ArgumentList is null) return null;
        foreach (var arg in attr.ArgumentList.Arguments)
        {
            if (arg.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
                return literal.Token.ValueText;
        }
        return null;
    }

    private static bool GlobMatch(string text, string pattern)
    {
        // Translate a simple glob ('*' = any run of chars) into a regex anchored end-to-end.
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(text, regex);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAssignableTo(INamedTypeSymbol candidate, INamedTypeSymbol target)
    {
        // Accept when the candidate derives from, or implements, the target.
        if (SymbolEqualityComparer.Default.Equals(candidate, target)) return false;
        for (var current = candidate.BaseType; current != null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, target)) return true;
        foreach (var iface in candidate.AllInterfaces)
            if (SymbolEqualityComparer.Default.Equals(iface, target)) return true;
        return false;
    }

    private static List<INamedTypeSymbol> CollectCandidateServices(Compilation compilation)
    {
        // Every non-abstract class declared in the compilation is a candidate attach target.
        var classes = new List<INamedTypeSymbol>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol) continue;
                if (symbol.IsAbstract) continue;
                classes.Add(symbol);
            }
        }

        return classes;
    }

    private static List<INamedTypeSymbol> FindProfileTypes(Compilation compilation)
    {
        var profiles = new List<INamedTypeSymbol>();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol) continue;
                foreach (var iface in symbol.AllInterfaces)
                {
                    if (iface.Name == "IAutoDepsProfile" &&
                        iface.ContainingNamespace?.ToDisplayString() == "IoCTools.Abstractions.Annotations")
                    {
                        if (seen.Add(FullyQualifiedName(symbol)))
                            profiles.Add(symbol);
                        break;
                    }
                }
            }
        }

        profiles.Sort((a, b) => System.StringComparer.Ordinal.Compare(FullyQualifiedName(a), FullyQualifiedName(b)));
        return profiles;
    }

    private static (List<INamedTypeSymbol> candidates, INamedTypeSymbol? selected) ResolveProfileName(
        IReadOnlyList<INamedTypeSymbol> profiles,
        string query)
    {
        // First, exact match on the fully-qualified name always wins.
        var fqnMatch = profiles.FirstOrDefault(p =>
            string.Equals(FullyQualifiedName(p), query, System.StringComparison.Ordinal));
        if (fqnMatch != null) return (new List<INamedTypeSymbol> { fqnMatch }, fqnMatch);

        // Otherwise, match on the simple name. Multiple hits => ambiguous.
        var simpleMatches = profiles
            .Where(p => string.Equals(p.Name, query, System.StringComparison.Ordinal))
            .ToList();
        if (simpleMatches.Count == 1) return (simpleMatches, simpleMatches[0]);
        return (simpleMatches, null);
    }

    private static string FullyQualifiedName(INamedTypeSymbol symbol) =>
        symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty, System.StringComparison.Ordinal);
}
