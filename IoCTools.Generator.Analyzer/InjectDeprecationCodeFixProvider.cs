namespace IoCTools.Generator.Analyzer;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IoCTools.Generator.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

/// <summary>
///     IDE code-fix for <c>IOC095</c>. Re-uses the shared
///     <see cref="InjectMigrationRewriter" /> so the lightbulb and the
///     <c>migrate-inject</c> CLI subcommand emit byte-identical output.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InjectDeprecationCodeFixProvider))]
[Shared]
public sealed class InjectDeprecationCodeFixProvider : CodeFixProvider
{
    private const string InjectAttributeMetadataName = "IoCTools.Abstractions.Annotations.InjectAttribute";
    private const string ExternalServiceAttributeMetadataName = "IoCTools.Abstractions.Annotations.ExternalServiceAttribute";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(AnalyzerDiagnosticDescriptors.InjectDeprecated.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            var fieldDecl = node.FirstAncestorOrSelf<FieldDeclarationSyntax>();
            if (fieldDecl is null) continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Migrate [Inject] to [DependsOn<T>]",
                    ct => ApplyFixAsync(context.Document, fieldDecl, ct),
                    equivalenceKey: "MigrateInjectToDependsOn"),
                diagnostic);
        }
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        FieldDeclarationSyntax triggerField,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null) return document;

        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is null) return document;

        var classDecl = triggerField.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl is null) return document;

        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct);
        if (classSymbol is null) return document;

        var fields = CollectInjectFields(classDecl, semanticModel, ct);
        if (fields.Count == 0) return document;

        var msbuildProperties = BuildMsBuildPropertyMap(document, root.SyntaxTree, ct);

        AutoDepsResolverOutput resolved;
        try
        {
            resolved = AutoDepsResolver.ResolveForService(
                semanticModel.Compilation,
                classSymbol,
                msbuildProperties);
        }
        catch
        {
            // The rewriter tolerates an empty resolved set (everything converts, nothing deletes).
            resolved = AutoDepsResolverOutput.Empty;
        }

        var migration = InjectMigrationRewriter.Rewrite(fields, resolved);

        var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);

        // Delete fields fully covered by an auto-dep.
        foreach (var fieldToDelete in migration.FieldsToDelete)
            editor.RemoveNode(fieldToDelete, SyntaxRemoveOptions.KeepNoTrivia);

        // Attach each new [DependsOn<...>] attribute as its own attribute list on the class.
        // Using separate AttributeLists keeps trailing trivia / formatting simple and matches
        // the CLI output. ElasticMarker lets the formatter pick the ambient line-ending
        // (LF in .editorconfig, CRLF on Windows defaults) rather than hard-coding CR-LF.
        foreach (var attr in migration.AttributesToAdd)
        {
            var attrList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attr))
                .WithTrailingTrivia(SyntaxFactory.ElasticMarker);
            editor.AddAttribute(classDecl, attrList);
        }

        return editor.GetChangedDocument();
    }

    private static IReadOnlyList<InjectMigrationRewriter.InjectFieldInfo> CollectInjectFields(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        var result = new List<InjectMigrationRewriter.InjectFieldInfo>();

        foreach (var fieldDecl in classDecl.Members.OfType<FieldDeclarationSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                if (semanticModel.GetDeclaredSymbol(variable, ct) is not IFieldSymbol fieldSymbol)
                    continue;

                var attrs = fieldSymbol.GetAttributes();
                var hasInject = attrs.Any(a => a.AttributeClass?.ToDisplayString() == InjectAttributeMetadataName);
                if (!hasInject) continue;

                var hasExternal = attrs.Any(a =>
                    a.AttributeClass?.ToDisplayString() == ExternalServiceAttributeMetadataName);

                result.Add(new InjectMigrationRewriter.InjectFieldInfo(
                    fieldDecl,
                    fieldSymbol.Type,
                    fieldSymbol.Name,
                    hasExternal));

                // One InjectFieldInfo per declaration; the rewriter deletes/keeps the whole declaration.
                break;
            }
        }

        return result;
    }

    /// <summary>
    ///     Builds the MSBuild-property dictionary in the shape
    ///     <see cref="AutoDepsResolver.ResolveForService" /> expects:
    ///     keys prefixed with <c>"build_property."</c>, values as-written.
    /// </summary>
    /// <remarks>
    ///     Only forwards the AutoDeps-related properties the resolver actually reads.
    ///     Missing keys are treated as "property not set" by the resolver's null-coalesce.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> BuildMsBuildPropertyMap(
        Document document,
        SyntaxTree tree,
        CancellationToken ct)
    {
        var result = new Dictionary<string, string>();

        // MSBuild `build_property.*` values are surfaced through GlobalOptions, not the
        // tree-scoped GetOptions(tree) API — those are .editorconfig-style per-file options.
        // Using the wrong one silently produces an empty map and the resolver falls back to
        // defaults, losing the user's kill-switch/exclude-glob/auto-detect configuration.
        var options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions;

        AddIfPresent(options, "IoCToolsAutoDepsDisable", result);
        AddIfPresent(options, "IoCToolsAutoDepsExcludeGlob", result);
        AddIfPresent(options, "IoCToolsAutoDetectLogger", result);

        return result;
    }

    private static void AddIfPresent(
        Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions options,
        string propertyName,
        IDictionary<string, string> target)
    {
        var buildKey = "build_property." + propertyName;
        if (options.TryGetValue(buildKey, out var value) && !string.IsNullOrEmpty(value))
            target[buildKey] = value;
    }
}
