namespace IoCTools.Testing.Generator.Pipeline;

using System.Collections.Immutable;
using System.Linq;
using IoCTools.Testing.CodeGeneration;
using IoCTools.Testing.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class TestFixturePipeline
{
    // Exact fully-qualified namespace the [Cover<T>] attribute must come from. A substring
    // match (the previous behavior) false-positives on consumer namespaces that contain the
    // substring "IoCTools.Testing", e.g. MyCorp.IoCTools.Testing.Extensions.
    private const string CoverAttributeNamespace = "IoCTools.Testing.Annotations";

    internal static IncrementalValuesProvider<TestClassInfo> Build(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                // Cheap syntactic pre-filter: must be a type decl that has at least one
                // attribute whose name starts with "Cover" (covers `Cover<T>`, `[Cover<T>]`,
                // fully-qualified `[IoCTools.Testing.Annotations.Cover<T>]`, and the legal
                // but discouraged `[CoverAttribute<T>]` form). Avoids running the semantic
                // model for every type declaration in the entire compilation.
                static (node, _) =>
                {
                    if (node is not TypeDeclarationSyntax tds) return false;
                    foreach (var attrList in tds.AttributeLists)
                    {
                        foreach (var attr in attrList.Attributes)
                        {
                            var name = attr.Name;
                            // unwrap qualified / generic names down to the rightmost identifier
                            while (true)
                            {
                                switch (name)
                                {
                                    case QualifiedNameSyntax qn:
                                        name = qn.Right;
                                        continue;
                                    case AliasQualifiedNameSyntax aqn:
                                        name = aqn.Name;
                                        continue;
                                    case GenericNameSyntax gn:
                                        var ident = gn.Identifier.ValueText;
                                        if (ident == "Cover" || ident == "CoverAttribute") return true;
                                        break;
                                    case SimpleNameSyntax sn:
                                        var simpleIdent = sn.Identifier.ValueText;
                                        if (simpleIdent == "Cover" || simpleIdent == "CoverAttribute") return true;
                                        break;
                                }
                                break;
                            }
                        }
                    }
                    return false;
                },
                static (ctx, _) =>
                {
                    var typeDecl = (TypeDeclarationSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl);
                    if (symbol == null || symbol.IsStatic || symbol.TypeKind != TypeKind.Class)
                        return (TestClassInfo?)null;

                    // Check for partial keyword
                    if (!typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                        return (TestClassInfo?)null;

                    // Check for [Cover<T>] attribute — exact namespace match. A consumer
                    // namespace like `MyApp.IoCTools.Testing.Extensions.CoverAttribute<T>`
                    // must NOT be picked up here.
                    var coverAttr = symbol.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.Name == "CoverAttribute" &&
                                            a.AttributeClass?.ContainingNamespace?.ToDisplayString() == CoverAttributeNamespace);

                    if (coverAttr == null)
                        return (TestClassInfo?)null;

                    // Extract TService generic type argument
                    if (coverAttr.AttributeClass is INamedTypeSymbol namedAttr &&
                        namedAttr.TypeArguments.Length > 0 &&
                        namedAttr.TypeArguments[0] is INamedTypeSymbol serviceSymbol)
                    {
                        return new TestClassInfo(
                            symbol,
                            typeDecl,
                            ctx.SemanticModel,
                            serviceSymbol,
                            GetLoggerProfile(coverAttr),
                            GetConcreteHandling(coverAttr));
                    }

                    return (TestClassInfo?)null;
                })
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x!.Value)
            .Collect()
            .SelectMany(static (tests, _) =>
                tests
                    .GroupBy(t => t.TestClassSymbol, SymbolEqualityComparer.Default)
                    .Select(g => g.First())
                    .ToImmutableArray());
    }

    private static LoggerProfile GetLoggerProfile(AttributeData coverAttr)
    {
        foreach (var arg in coverAttr.NamedArguments)
        {
            if (arg.Key != "Logger" || arg.Value.Value == null)
                continue;

            // Prefer symbolic resolution against the enum's field list so member-order
            // reordering in `FixtureLoggerProfile` cannot silently misclassify.
            var name = ResolveEnumMemberName(arg.Value);
            if (name == "NullLogger") return LoggerProfile.NullLogger;
            if (name == "Mock") return LoggerProfile.Mock;

            // Fallback to int comparison preserving prior behavior if the symbol resolution
            // path comes back empty (defensive — should not normally happen).
            return arg.Value.Value is int profileValue && profileValue == 1
                ? LoggerProfile.NullLogger
                : LoggerProfile.Mock;
        }

        return LoggerProfile.Mock;
    }

    private static ConcreteHandlingMode GetConcreteHandling(AttributeData coverAttr)
    {
        foreach (var arg in coverAttr.NamedArguments)
        {
            if (arg.Key != "ConcreteHandling" || arg.Value.Value == null)
                continue;

            var name = ResolveEnumMemberName(arg.Value);
            if (name == "ForceMock") return ConcreteHandlingMode.ForceMock;
            if (name == "Auto") return ConcreteHandlingMode.Auto;

            // Fallback to numeric: 1 == ForceMock based on declaration order
            return arg.Value.Value is int v && v == 1
                ? ConcreteHandlingMode.ForceMock
                : ConcreteHandlingMode.Auto;
        }

        return ConcreteHandlingMode.Auto;
    }

    private static string? ResolveEnumMemberName(TypedConstant constant)
    {
        if (constant.Type is not INamedTypeSymbol enumType || enumType.TypeKind != TypeKind.Enum)
            return null;
        if (constant.Value is null) return null;

        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (member is { HasConstantValue: true, ConstantValue: not null } &&
                member.ConstantValue.Equals(constant.Value))
            {
                return member.Name;
            }
        }
        return null;
    }
}
