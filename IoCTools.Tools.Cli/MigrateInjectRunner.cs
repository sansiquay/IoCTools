namespace IoCTools.Tools.Cli;

using CommandLine;

using IoCTools.Generator.Shared;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

/// <summary>
///     Headless runner for the <c>migrate-inject</c> subcommand. Loads a project (or
///     solution), finds every <c>[Inject]</c> field on each service, and applies the
///     shared <see cref="InjectMigrationRewriter" /> to produce updated source. With
///     <c>--dry-run</c> the computed diff is printed without touching disk; otherwise
///     files are rewritten in-place.
/// </summary>
/// <remarks>
///     Processing is strictly sequential per project, per document -- the rewriter reads
///     and writes source files, and a parallel sweep over the same document set would
///     race. MSBuildWorkspace is likewise single-threaded by contract.
/// </remarks>
internal static class MigrateInjectRunner
{
    private const string InjectAttributeMetadataName = "IoCTools.Abstractions.Annotations.InjectAttribute";
    private const string ExternalServiceAttributeMetadataName = "IoCTools.Abstractions.Annotations.ExternalServiceAttribute";

    public static async Task<int> RunAsync(MigrateInjectCommandOptions opts, CancellationToken token)
    {
        var path = string.IsNullOrWhiteSpace(opts.Path) ? Environment.CurrentDirectory : opts.Path!;
        var projectPaths = ResolveProjectPaths(path);
        if (projectPaths.Count == 0)
        {
            Console.Error.WriteLine(
                $"No .csproj files found under '{path}'. Pass --path pointing to a .sln, .csproj, or directory containing one.");
            return 1;
        }

        var summary = new MigrationSummary();

        // Sequential per project. Each project gets its own ProjectContext (which in turn owns
        // an MSBuildWorkspace) so project-level NuGet/reference state does not leak between
        // projects in a solution. ProjectContext handles MSBuild/SDK registration exactly the
        // way every other CLI subcommand does -- reusing it avoids divergent package-resolution
        // behavior across commands.
        foreach (var projectPath in projectPaths)
        {
            var common = new CommandLine.CommonOptions(
                projectPath,
                opts.Configuration,
                opts.Framework,
                Json: false,
                Verbose: opts.Verbose);

            ProjectContext? context = null;
            try
            {
                context = await ProjectContext.CreateAsync(common, token);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load project '{projectPath}': {ex.Message}");
                continue;
            }

            try
            {
                await MigrateProjectAsync(context.Project, opts, summary, token);
            }
            finally
            {
                await context.DisposeAsync();
            }
        }

        PrintSummary(summary, opts.DryRun);
        return 0;
    }

    private static async Task MigrateProjectAsync(
        Project project,
        MigrateInjectCommandOptions opts,
        MigrationSummary summary,
        CancellationToken token)
    {
        if (await project.GetCompilationAsync(token) is not CSharpCompilation rawCompilation)
        {
            Console.Error.WriteLine($"Project '{project.Name}': unable to build C# compilation; skipping.");
            return;
        }

        // MSBuildWorkspace runs source generators when producing the compilation, which means
        // IoCTools has already emitted its partial constructor into the class. That constructor
        // trips AutoDepsResolver.HasManualConstructor -> Empty, so every field ends up in the
        // "convert" branch (never deleted). Strip generator-authored trees before handing the
        // compilation to the resolver. The `profiles` subcommand uses the identical trick --
        // see Program.StripGeneratedTreesForProfiles.
        var compilation = StripGeneratedTrees(rawCompilation);

        // Cross-version check: if the referenced IoCTools.Abstractions is < 1.6, emit the
        // notice and force convert-only (no deletions) by passing Empty to the rewriter.
        var hasIoCTools16 = HasIoCToolsAbstractions16(compilation);
        if (!hasIoCTools16)
        {
            Console.WriteLine(
                $"Project {project.Name}: IoCTools.Abstractions < 1.6 detected (or absent). " +
                "'Delete entirely' migration branch disabled -- all [Inject] fields will convert to [DependsOn<T>].");
        }

        var msbuildProps = ReadMsBuildPropsForProject(project);
        summary.ProjectsProcessed++;

        // Sequentially visit each document in the project. Solution-level fan-out is also
        // sequential -- each project owns its own workspace + compilation snapshot.
        foreach (var document in project.Documents)
        {
            token.ThrowIfCancellationRequested();
            if (document.FilePath is null || !File.Exists(document.FilePath)) continue;
            // Skip generator-authored trees; we only touch user source.
            if (IsGeneratedPath(document.FilePath)) continue;

            if (await document.GetSyntaxRootAsync(token) is not SyntaxNode root) continue;
            var originalTree = root.SyntaxTree;
            // Pull the semantic model from the stripped compilation so the class symbol we hand
            // to AutoDepsResolver does NOT advertise the generator-emitted constructor (which
            // would trigger HasManualConstructor -> Empty and disable the delete branch). Match
            // the stripped tree by file path; the text content is identical by construction.
            var strippedTree = compilation.SyntaxTrees.FirstOrDefault(t =>
                string.Equals(t.FilePath, document.FilePath, StringComparison.Ordinal));
            if (strippedTree is null) continue;
            var semantic = compilation.GetSemanticModel(strippedTree);
            // For DocumentEditor.RemoveNode to resolve the node inside the *original* document's
            // tree, rediscover classes in the stripped tree but pair them with same-position
            // nodes from the original tree. Tree reference identity matters for editor edits.
            if (await strippedTree.GetRootAsync(token) is not SyntaxNode strippedRoot) continue;

            var classDecls = strippedRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
            if (classDecls.Length == 0) continue;

            var editor = await DocumentEditor.CreateAsync(document, token);
            var documentModified = false;

            foreach (var classDeclStripped in classDecls)
            {
                if (semantic.GetDeclaredSymbol(classDeclStripped, token) is not INamedTypeSymbol classSymbol)
                    continue;

                var injectFields = CollectInjectFields(classDeclStripped, semantic, token);
                if (injectFields.Count == 0) continue;

                // Translate the class declaration and each field-to-delete back into the
                // original document's syntax tree -- DocumentEditor keys edits off node identity
                // in the document it was created from.
                var classDecl = FindMatchingNode<ClassDeclarationSyntax>(root, classDeclStripped);
                if (classDecl is null) continue;

                AutoDepsResolverOutput resolved;
                if (hasIoCTools16)
                {
                    try
                    {
                        resolved = AutoDepsResolver.ResolveForService(compilation, classSymbol, msbuildProps);
                        if (opts.Verbose)
                            Console.WriteLine(
                                $"  {classSymbol.Name}: {resolved.Entries.Length} auto-deps; " +
                                $"{injectFields.Count} [Inject] field(s)");
                    }
                    catch
                    {
                        resolved = AutoDepsResolverOutput.Empty;
                    }
                }
                else
                {
                    // Force convert-only: every [Inject] becomes a [DependsOn<T>], nothing deletes.
                    resolved = AutoDepsResolverOutput.Empty;
                }

                var migration = InjectMigrationRewriter.Rewrite(injectFields, resolved);
                if (migration.FieldsToDelete.Count == 0 && migration.AttributesToAdd.Count == 0)
                    continue;

                // Conflict guard: if the class already declares [DependsOn<T>] for a type we
                // would add, prefer the existing attribute and skip re-adding it (warn once).
                var alreadyDeclared = CollectExistingDependsOnTypes(classDeclStripped, semantic, token);
                var attributesToAdd = new List<AttributeSyntax>();
                foreach (var attr in migration.AttributesToAdd)
                {
                    if (AttributeConflictsWithExisting(attr, alreadyDeclared, out var conflictType))
                    {
                        Console.Error.WriteLine(
                            $"Warning: {document.FilePath} -- class '{classSymbol.Name}' already declares " +
                            $"[DependsOn<{conflictType}>]; skipping duplicate.");
                        continue;
                    }
                    attributesToAdd.Add(attr);
                }

                foreach (var fieldStripped in migration.FieldsToDelete.Distinct())
                {
                    var field = FindMatchingNode<FieldDeclarationSyntax>(root, fieldStripped);
                    if (field is null) continue;
                    editor.RemoveNode(field, SyntaxRemoveOptions.KeepNoTrivia);
                }

                foreach (var attr in attributesToAdd)
                {
                    var attrList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attr))
                        .WithTrailingTrivia(SyntaxFactory.ElasticMarker);
                    editor.AddAttribute(classDecl, attrList);
                }

                // Branch A contributions are deletes; Branches B/C show up as converts. The
                // rewriter lumps both into FieldsToDelete (Branch B/C fields are removed so the
                // class ends up with either the old [Inject] field or the new [DependsOn<T>]
                // class attr, never both). We split them back out for the summary using the
                // same predicate the rewriter uses internally.
                foreach (var f in injectFields)
                {
                    if (IsFullyCoveredDelete(f, resolved)) summary.FieldsDeleted++;
                    else summary.FieldsConverted++;
                }
                documentModified = true;
            }

            if (!documentModified) continue;

            var newDoc = editor.GetChangedDocument();
            if (await newDoc.GetSyntaxRootAsync(token) is not SyntaxNode newRoot) continue;
            var newText = newRoot.ToFullString();

            // Parse-back validation: a malformed rewrite would be worse than no rewrite at all.
            var reparsed = CSharpSyntaxTree.ParseText(newText);
            if (reparsed.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                Console.Error.WriteLine(
                    $"Warning: {document.FilePath} -- rewrite produced parse errors; leaving file unchanged.");
                continue;
            }

            summary.FilesTouched++;
            var originalText = root.ToFullString();

            if (opts.DryRun)
            {
                PrintDiff(document.FilePath!, originalText, newText);
            }
            else
            {
                File.WriteAllText(document.FilePath!, newText);
                if (opts.Verbose) Console.WriteLine($"Migrated: {document.FilePath}");
            }
        }
    }

    /// <summary>
    ///     Given a node from the stripped compilation's tree, finds the equivalent node in
    ///     the original document's tree by span. The two trees share source text, so span
    ///     equality is a reliable match.
    /// </summary>
    private static T? FindMatchingNode<T>(SyntaxNode originalRoot, SyntaxNode strippedNode) where T : SyntaxNode
    {
        foreach (var candidate in originalRoot.DescendantNodesAndSelf().OfType<T>())
        {
            if (candidate.Span == strippedNode.Span) return candidate;
        }
        return null;
    }

    private static CSharpCompilation StripGeneratedTrees(CSharpCompilation compilation)
    {
        // Generator trees arrive from MSBuildWorkspace with paths like
        // /.../obj/Debug/net8.0/generated/IoCTools.Generator/.../Foo.Constructor.g.cs -- any of
        // (.g.cs suffix, "generated" segment, "/obj/" segment) reliably tags them. Use both
        // forward- and backslash variants so macOS/Linux and Windows both hit the filter.
        bool IsGenerated(string p) =>
            p.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("/generated/", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("\\generated\\", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase);

        var generated = compilation.SyntaxTrees
            .Where(t => !string.IsNullOrEmpty(t.FilePath) && IsGenerated(t.FilePath))
            .ToArray();
        if (generated.Length == 0) return compilation;
        return (CSharpCompilation)compilation.RemoveSyntaxTrees(generated);
    }

    private static bool IsFullyCoveredDelete(
        InjectMigrationRewriter.InjectFieldInfo f,
        AutoDepsResolverOutput resolved)
    {
        // Mirrors Branch A in InjectMigrationRewriter.Rewrite: fully-covered, default-named,
        // non-external fields get deleted (no DependsOn<T> added). We recompute it here so
        // the runner's summary counters can distinguish "delete" from "convert" without
        // threading state back out of the rewriter.
        if (f.HasExternalService) return false;
        var id = SymbolIdentity.From(f.Type);
        var covered = resolved.Entries.Any(e => e.DepType.Equals(id));
        if (!covered) return false;
        var expected = DefaultFieldName.Compute(f.Type);
        return string.Equals(f.FieldName, expected, StringComparison.Ordinal);
    }

    private static IReadOnlyList<InjectMigrationRewriter.InjectFieldInfo> CollectInjectFields(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        // Mirrors InjectDeprecationCodeFixProvider.CollectInjectFields; kept in sync so
        // the CLI and IDE paths emit byte-identical output from the shared rewriter.
        var result = new List<InjectMigrationRewriter.InjectFieldInfo>();
        foreach (var fieldDecl in classDecl.Members.OfType<FieldDeclarationSyntax>())
        {
            ct.ThrowIfCancellationRequested();
            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                if (semanticModel.GetDeclaredSymbol(variable, ct) is not IFieldSymbol fieldSymbol) continue;
                var attrs = fieldSymbol.GetAttributes();
                var hasInject = attrs.Any(a => a.AttributeClass?.ToDisplayString() == InjectAttributeMetadataName);
                if (!hasInject) continue;
                var hasExternal = attrs.Any(a =>
                    a.AttributeClass?.ToDisplayString() == ExternalServiceAttributeMetadataName);
                result.Add(new InjectMigrationRewriter.InjectFieldInfo(
                    fieldDecl, fieldSymbol.Type, fieldSymbol.Name, hasExternal));
                break;
            }
        }

        return result;
    }

    private static HashSet<string> CollectExistingDependsOnTypes(
        ClassDeclarationSyntax classDecl,
        SemanticModel semantic,
        CancellationToken ct)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attrList in classDecl.AttributeLists)
        foreach (var attr in attrList.Attributes)
        {
            if (attr.Name is not GenericNameSyntax gn) continue;
            if (!gn.Identifier.ValueText.StartsWith("DependsOn", StringComparison.Ordinal)) continue;
            foreach (var typeArg in gn.TypeArgumentList.Arguments)
            {
                if (semantic.GetTypeInfo(typeArg, ct).Type is ITypeSymbol ts)
                    set.Add(ts.ToDisplayString());
            }
        }

        return set;
    }

    private static bool AttributeConflictsWithExisting(
        AttributeSyntax attr,
        HashSet<string> existingTypes,
        out string conflictType)
    {
        conflictType = string.Empty;
        if (attr.Name is not GenericNameSyntax gn) return false;
        foreach (var typeArg in gn.TypeArgumentList.Arguments)
        {
            var display = typeArg.ToFullString().Trim();
            if (existingTypes.Contains(display))
            {
                conflictType = display;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Decides whether the consuming project is on IoCTools 1.6+ -- the version of
    ///     <c>IoCTools.Abstractions</c> that introduced auto-deps. We consider it 1.6+ only
    ///     if the Abstractions assembly appears as a metadata reference (NuGet package or
    ///     ProjectReference pointing at this repo's Abstractions) with version >= 1.6, OR
    ///     the ProjectReference wires the Abstractions via an in-tree
    ///     <c>&lt;Compile Include&gt;</c> link (which appears as a class library in the
    ///     compilation's assembly name). Source-defined stub types in the consumer itself
    ///     are treated as pre-1.6: they signal a user on the old Abstractions who rolled
    ///     their own minimal shim.
    /// </summary>
    private static bool HasIoCToolsAbstractions16(Compilation compilation)
    {
        // Explicit metadata reference to IoCTools.Abstractions.
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) continue;
            var name = assembly.Identity.Name;
            if (!string.Equals(name, "IoCTools.Abstractions", StringComparison.OrdinalIgnoreCase)) continue;
            var version = assembly.Identity.Version;
            return version.Major > 1 || (version.Major == 1 && version.Minor >= 6);
        }

        // In-tree consumers (our own fixture projects) often do not reference the Abstractions
        // as a package; they link the source via <Compile Include>. The resulting DependsOnAttribute
        // lives in the CONSUMER's own assembly, so when we locate it there treat that as 1.6+.
        var dependsOnTypeMetadataName = "IoCTools.Abstractions.Annotations.DependsOnAttribute`1";
        var dependsOnType = compilation.GetTypeByMetadataName(dependsOnTypeMetadataName);
        if (dependsOnType is null) return false;

        // Distinguish "linked from this repo's current Abstractions tree" from "pre-1.6 stub":
        // the linked file paths will point into IoCTools.Abstractions/Annotations/; hand-rolled
        // stubs in the consumer's own source tree will not.
        foreach (var loc in dependsOnType.Locations)
        {
            var path = loc.SourceTree?.FilePath;
            if (string.IsNullOrEmpty(path)) continue;
            if (path!.Contains("IoCTools.Abstractions", StringComparison.OrdinalIgnoreCase) &&
                path.Contains("Annotations", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyDictionary<string, string> ReadMsBuildPropsForProject(Project project)
    {
        // Only forward the AutoDeps-related switches the resolver actually consults. Absent
        // keys are treated as "not set" by the resolver's null-coalesce, which gives us the
        // defaults (auto-detect logger on, no kill switch, no exclude glob).
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var options = project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions;
        foreach (var key in new[]
                 {
                     "build_property.IoCToolsAutoDepsDisable",
                     "build_property.IoCToolsAutoDepsExcludeGlob",
                     "build_property.IoCToolsAutoDetectLogger"
                 })
        {
            if (options.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                result[key] = v;
        }

        return result;
    }

    private static IReadOnlyList<string> ResolveProjectPaths(string input)
    {
        var result = new List<string>();
        if (File.Exists(input))
        {
            var ext = Path.GetExtension(input).ToLowerInvariant();
            if (ext == ".csproj")
            {
                result.Add(Path.GetFullPath(input));
                return result;
            }

            if (ext == ".sln")
            {
                // Very light .sln parser -- read the `Project(...) = "Name", "Path\To.csproj"` lines.
                foreach (var line in File.ReadAllLines(input))
                {
                    if (!line.StartsWith("Project(", StringComparison.OrdinalIgnoreCase)) continue;
                    var parts = line.Split('"');
                    if (parts.Length < 6) continue;
                    var relPath = parts[5];
                    if (!relPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) continue;
                    var abs = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(input)!, relPath));
                    if (File.Exists(abs)) result.Add(abs);
                }
                return result;
            }
        }

        if (Directory.Exists(input))
        {
            foreach (var f in Directory.EnumerateFiles(input, "*.csproj", SearchOption.AllDirectories))
            {
                if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
                if (f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
                result.Add(Path.GetFullPath(f));
            }
        }

        return result;
    }

    private static bool IsGeneratedPath(string path) =>
        path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}generated{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Prints a minimalist unified diff so the user can eyeball the proposed rewrite.</summary>
    private static void PrintDiff(string filePath, string original, string updated)
    {
        Console.WriteLine($"--- {filePath} (before)");
        Console.WriteLine($"+++ {filePath} (after)");
        var origLines = original.Split('\n');
        var newLines = updated.Split('\n');
        var max = Math.Max(origLines.Length, newLines.Length);

        // Greedy line-level diff. Sufficient for a human review pass; we are not producing
        // a patch that will be piped into `patch(1)`.
        for (var i = 0; i < max; i++)
        {
            var a = i < origLines.Length ? origLines[i].TrimEnd('\r') : null;
            var b = i < newLines.Length ? newLines[i].TrimEnd('\r') : null;
            if (a == b) continue;
            if (a != null) Console.WriteLine($"- {a}");
            if (b != null) Console.WriteLine($"+ {b}");
        }

        var anyDelete = origLines.Length > newLines.Length;
        var anyAdd = newLines.Length > origLines.Length;
        if (anyDelete) Console.WriteLine($"(would delete lines from {filePath})");
        if (anyAdd) Console.WriteLine($"(would add lines to {filePath})");
    }

    private static void PrintSummary(MigrationSummary summary, bool dryRun)
    {
        Console.WriteLine();
        Console.WriteLine("migrate-inject summary");
        Console.WriteLine($"  Projects processed: {summary.ProjectsProcessed}");
        Console.WriteLine($"  Files touched:      {summary.FilesTouched}");
        Console.WriteLine($"  Fields deleted:     {summary.FieldsDeleted}");
        Console.WriteLine($"  Fields converted:   {summary.FieldsConverted}");
        if (dryRun) Console.WriteLine("  Mode: dry-run (no files written)");
    }

    private sealed class MigrationSummary
    {
        public int ProjectsProcessed { get; set; }
        public int FilesTouched { get; set; }
        public int FieldsDeleted { get; set; }
        public int FieldsConverted { get; set; }
    }
}
