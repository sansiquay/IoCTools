namespace IoCTools.Generator.Shared;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
///     Pure syntax transform that migrates <c>[Inject]</c> fields on a single service
///     class to <c>[DependsOn&lt;T&gt;]</c> class-level attributes. Consumed by both the
///     IDE code-fix provider and the headless <c>migrate-inject</c> CLI subcommand — the
///     output must be byte-identical in both environments.
/// </summary>
public static class InjectMigrationRewriter
{
    /// <summary>
    ///     Describes a single <c>[Inject]</c> field candidate for migration.
    /// </summary>
    public readonly struct InjectFieldInfo
    {
        public InjectFieldInfo(
            FieldDeclarationSyntax field,
            ITypeSymbol type,
            string fieldName,
            bool hasExternalService)
        {
            Field = field;
            Type = type;
            FieldName = fieldName;
            HasExternalService = hasExternalService;
        }

        public FieldDeclarationSyntax Field { get; }
        public ITypeSymbol Type { get; }
        public string FieldName { get; }
        public bool HasExternalService { get; }
    }

    /// <summary>
    ///     Result of a migration pass over a single class's <c>[Inject]</c> fields.
    /// </summary>
    public readonly struct MigrationResult
    {
        public MigrationResult(
            IReadOnlyList<FieldDeclarationSyntax> fieldsToDelete,
            IReadOnlyList<AttributeSyntax> attributesToAdd)
        {
            FieldsToDelete = fieldsToDelete;
            AttributesToAdd = attributesToAdd;
        }

        /// <summary>Fields fully covered by an auto-dep — safe to delete entirely.</summary>
        public IReadOnlyList<FieldDeclarationSyntax> FieldsToDelete { get; }

        /// <summary>New <c>[DependsOn&lt;...&gt;]</c> attributes to attach to the class.</summary>
        public IReadOnlyList<AttributeSyntax> AttributesToAdd { get; }
    }

    /// <summary>
    ///     Given every <c>[Inject]</c> field on one service class plus the resolved
    ///     auto-dep set for that service, returns the migration plan:
    ///     (fields to delete, DependsOn attributes to add to the class).
    /// </summary>
    public static MigrationResult Rewrite(
        IReadOnlyList<InjectFieldInfo> fields,
        AutoDepsResolverOutput resolvedAutoDeps)
    {
        if (fields == null) throw new ArgumentNullException(nameof(fields));

        var fieldsToDelete = new List<FieldDeclarationSyntax>();
        var toConvert = new List<InjectFieldInfo>();

        var coveredTypes = new HashSet<SymbolIdentity>(
            resolvedAutoDeps.Entries.Select(e => e.DepType));

        // Branch A: delete if covered by auto-dep, the field uses the default name,
        // and no ExternalService flag is present.
        foreach (var f in fields)
        {
            var covered = coveredTypes.Contains(SymbolIdentity.From(f.Type));
            var bareName = IsDefaultFieldName(f.FieldName, f.Type);
            if (covered && bareName && !f.HasExternalService)
                fieldsToDelete.Add(f.Field);
            else
                toConvert.Add(f);
        }

        // Branch B+C: coalesce remaining fields into DependsOn attributes.
        // Split by external flag since external is an attribute-wide modifier.
        var attrs = new List<AttributeSyntax>();
        foreach (var group in toConvert.GroupBy(f => f.HasExternalService))
        {
            var external = group.Key;
            var fieldsInGroup = group.ToList();
            attrs.Add(BuildDependsOnAttribute(fieldsInGroup, external));
        }

        return new MigrationResult(fieldsToDelete, attrs);
    }

    private static AttributeSyntax BuildDependsOnAttribute(
        IReadOnlyList<InjectFieldInfo> fields,
        bool external)
    {
        // DependsOn<T1, T2, ..., Tn>(memberName1: "_custom", external: true)
        // Use MinimallyQualifiedFormat so ParseTypeName accepts the result (no global:: prefix).
        var typeArgs = SyntaxFactory.TypeArgumentList(
            SyntaxFactory.SeparatedList(fields.Select(f =>
                SyntaxFactory.ParseTypeName(f.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))));

        var name = SyntaxFactory.GenericName("DependsOn").WithTypeArgumentList(typeArgs);

        var args = new List<AttributeArgumentSyntax>();
        for (var i = 0; i < fields.Count; i++)
        {
            // Only emit memberName{N} for positions whose field name differs from the default.
            if (IsDefaultFieldName(fields[i].FieldName, fields[i].Type)) continue;
            args.Add(SyntaxFactory.AttributeArgument(
                    SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(fields[i].FieldName)))
                .WithNameColon(SyntaxFactory.NameColon($"memberName{i + 1}")));
        }

        if (external)
        {
            args.Add(SyntaxFactory.AttributeArgument(
                    SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))
                .WithNameColon(SyntaxFactory.NameColon("external")));
        }

        var attr = SyntaxFactory.Attribute(name);
        if (args.Count > 0)
            attr = attr.WithArgumentList(
                SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(args)));
        return attr;
    }

    /// <summary>
    ///     Checks whether a field uses the default name IoCTools' generator would emit
    ///     for its type. Routes through <see cref="DefaultFieldName" /> so the rewriter
    ///     agrees with the generator on edge cases (generic collections, arrays,
    ///     reserved keywords, non-default naming conventions).
    /// </summary>
    private static bool IsDefaultFieldName(string fieldName, ITypeSymbol type)
    {
        var expected = DefaultFieldName.Compute(type);
        return string.Equals(fieldName, expected, StringComparison.Ordinal);
    }
}
