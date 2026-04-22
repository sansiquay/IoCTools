namespace IoCTools.Tools.Cli;

using System.Collections.Immutable;

using Generator.Analysis;
using Generator.Generator;
using Generator.Generator.Intent;
using Generator.Shared;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed class ServiceFieldInspector
{
    // Use fully qualified type names (without the leading global::) so CLI output
    // is stable across compiler versions. Dotnet 10 started eliding namespaces for
    // some metadata types (e.g. ILogger<>) when using a custom SymbolDisplayFormat.
    private static readonly SymbolDisplayFormat TypeFormat =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private readonly Project _project;

    public ServiceFieldInspector(Project project)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
    }

    private static string FormatTypeName(ITypeSymbol symbol)
    {
        // FullyQualifiedFormat always includes namespaces for the symbol and its generic arguments.
        // We strip the Roslyn "global::" prefix to keep the CLI output clean.
        var formatted = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return formatted.Replace("global::", string.Empty, StringComparison.Ordinal);
    }

    public async Task<IReadOnlyList<ServiceFieldReport>> GetFieldReportsAsync(string? filePath,
        IReadOnlyList<string> typeFilters,
        CancellationToken cancellationToken)
    {
        var semanticContext = await BuildSemanticContextAsync(filePath, cancellationToken);
        var matches = new List<ServiceFieldReport>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var compilation = await _project.GetCompilationAsync(cancellationToken);
        var msbuildProps = AutoDepsAttributionResolver.BuildMsBuildProperties(_project);

        foreach (var (declaration, semanticModel) in semanticContext)
        {
            var symbol = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
            if (symbol == null) continue;
            var key = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!seen.Add(key)) continue;
            if (!IsServiceCandidate(symbol, declaration)) continue;
            if (!MatchesFilter(symbol, typeFilters)) continue;

            var attributions = compilation != null
                ? AutoDepsAttributionResolver.ResolveAttributions(compilation, symbol, msbuildProps)
                : ImmutableDictionary<string, AutoDepAttribution>.Empty;

            var dependencyFields = DependsOnFieldAnalyzer.GetRawDependsOnFieldsForTypeWithExternalFlag(symbol)
                .Select(dep =>
                {
                    var typeName = FormatTypeName(dep.ServiceType);
                    AutoDepAttribution? attribution = attributions.TryGetValue(typeName, out var resolved)
                        ? resolved
                        : null;
                    return new GeneratedFieldInfo(dep.FieldName,
                        typeName,
                        GeneratedFieldKind.Dependency,
                        "DependsOn",
                        null,
                        null,
                        null,
                        null,
                        dep.IsExternal,
                        attribution);
                })
                .ToList();

            // Merge auto-deps that are NOT covered by explicit DependsOn fields (e.g., auto-builtin
            // ILogger detected via MEL). These are surfaced as additional dependency rows so the
            // CLI can narrate the full resolved set, not just the DependsOn subset.
            var coveredTypeNames = new HashSet<string>(dependencyFields.Select(f => f.TypeName), StringComparer.Ordinal);
            foreach (var kvp in attributions)
            {
                if (coveredTypeNames.Contains(kvp.Key)) continue;
                if (kvp.Value.Kind == AutoDepSourceKind.Explicit) continue;
                dependencyFields.Add(new GeneratedFieldInfo(
                    DeriveFieldName(kvp.Key),
                    kvp.Key,
                    GeneratedFieldKind.Dependency,
                    MapSourceLabel(kvp.Value),
                    null, null, null, null,
                    false,
                    kvp.Value));
            }

            var configFields = ConfigurationFieldAnalyzer.GetConfigurationInjectedFieldsForType(symbol, semanticModel)
                .Where(info => info.GeneratedField)
                .Select(info => new GeneratedFieldInfo(info.FieldName,
                    FormatTypeName(info.FieldType),
                    GeneratedFieldKind.Configuration,
                    "DependsOnConfiguration",
                    info.ConfigurationKey,
                    info.DefaultValue,
                    info.Required,
                    info.SupportsReloading,
                    false,
                    null))
                .ToList();

            var report = new ServiceFieldReport(symbol.ToDisplayString(TypeFormat),
                declaration.SyntaxTree.FilePath ?? filePath ?? "<unknown>",
                dependencyFields,
                configFields);
            matches.Add(report);
        }

        return matches;
    }

    private static string MapSourceLabel(AutoDepAttribution attribution) => attribution.Kind switch
    {
        AutoDepSourceKind.AutoBuiltinILogger => "AutoDep (built-in ILogger)",
        AutoDepSourceKind.AutoUniversal => "AutoDep",
        AutoDepSourceKind.AutoOpenUniversal => "AutoDepOpen",
        AutoDepSourceKind.AutoProfile => $"AutoDepsProfile:{attribution.SourceName}",
        AutoDepSourceKind.AutoTransitive => $"AutoDep (transitive:{attribution.AssemblyName})",
        _ => "AutoDep"
    };

    private static string DeriveFieldName(string typeName)
    {
        // Produce a stable, lowercase, underscore-prefixed field name for display in CLI tables.
        // Only used when the resolver surfaced an auto-dep not covered by an existing DependsOn
        // field -- this matches the generator's `_camelCase` convention for consistency.
        //
        // Takes only the outer type name (strip namespace and any generic argument suffix),
        // drops a leading 'I' on interfaces, and camel-cases the result.
        var simple = typeName;
        var firstGeneric = simple.IndexOf('<');
        var pruned = firstGeneric >= 0 ? simple.Substring(0, firstGeneric) : simple;
        var lastDot = pruned.LastIndexOf('.');
        if (lastDot >= 0) pruned = pruned.Substring(lastDot + 1);
        if (pruned.Length > 1 && pruned[0] == 'I' && char.IsUpper(pruned[1])) pruned = pruned.Substring(1);
        if (pruned.Length == 0) return "_dep";
        return "_" + char.ToLowerInvariant(pruned[0]) + pruned.Substring(1);
    }

    public async Task<INamedTypeSymbol?> FindServiceSymbolAsync(string filePath,
        string typeName,
        CancellationToken cancellationToken)
    {
        var semanticContext = await BuildSemanticContextAsync(filePath, cancellationToken);
        foreach (var (declaration, semanticModel) in semanticContext)
        {
            var symbol = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
            if (symbol == null) continue;
            if (!IsServiceCandidate(symbol, declaration)) continue;
            if (MatchesTypeName(symbol, typeName)) return symbol;
        }

        return null;
    }

    private async Task<IReadOnlyList<(ClassDeclarationSyntax Declaration, SemanticModel SemanticModel)>>
        BuildSemanticContextAsync(
            string? filePath,
            CancellationToken cancellationToken)
    {
        var documents = filePath == null
            ? _project.Documents
            : new[] { FindDocument(filePath) };

        var results = new List<(ClassDeclarationSyntax, SemanticModel)>();

        foreach (var document in documents)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken)
                                ?? throw new InvalidOperationException("Unable to create semantic model for file.");
            var root = await document.GetSyntaxRootAsync(cancellationToken) ??
                       throw new InvalidOperationException("Unable to read syntax tree for file.");

            var declarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            results.AddRange(declarations.Select(d => (d, semanticModel)));
        }

        return results;
    }

    private Document FindDocument(string filePath)
    {
        var normalized = Path.GetFullPath(filePath);
        var document = _project.Documents.FirstOrDefault(d =>
            !string.IsNullOrWhiteSpace(d.FilePath) &&
            PathsEqual(d.FilePath!, normalized));
        if (document == null)
            throw new InvalidOperationException($"File '{filePath}' is not part of the loaded project.");
        return document;
    }

    private static bool MatchesFilter(INamedTypeSymbol symbol,
        IReadOnlyList<string> filters)
    {
        if (filters == null || filters.Count == 0) return true;
        return filters.Any(filter => MatchesTypeName(symbol, filter));
    }

    private static bool MatchesTypeName(INamedTypeSymbol symbol,
        string filter)
    {
        return TypeFilterUtility.Matches(symbol.Name, filter)
            || TypeFilterUtility.Matches(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), filter)
            || TypeFilterUtility.Matches(symbol.ToDisplayString(TypeFormat), filter);
    }

    private static bool IsServiceCandidate(INamedTypeSymbol symbol,
        ClassDeclarationSyntax declaration)
    {
        var hasInject = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(symbol);
        var hasInjectConfig = ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(symbol);
        var hasDependsOn = symbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);
        var hasConditional = symbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
        var hasRegisterAll = symbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "RegisterAsAllAttribute");
        var hasRegisterAs = symbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name?.StartsWith("RegisterAsAttribute") == true &&
                         attr.AttributeClass?.IsGenericType == true);
        var (hasLifetime, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(symbol);
        var isHosted = TypeAnalyzer.IsAssignableFromIHostedService(symbol);
        var isPartialWithInterfaces = declaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) &&
                                      symbol.Interfaces.Any();

        return ServiceIntentEvaluator.HasExplicitServiceIntent(symbol,
            hasInject,
            hasInjectConfig,
            hasDependsOn,
            hasConditional,
            hasRegisterAll,
            hasRegisterAs,
            hasLifetime,
            isHosted,
            isPartialWithInterfaces);
    }

    private static bool PathsEqual(string left,
        string right)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
    }
}

internal sealed record ServiceFieldReport(
    string TypeName,
    string FilePath,
    IReadOnlyList<GeneratedFieldInfo> DependencyFields,
    IReadOnlyList<GeneratedFieldInfo> ConfigurationFields);

internal sealed record GeneratedFieldInfo(
    string FieldName,
    string TypeName,
    GeneratedFieldKind Kind,
    string Source,
    string? ConfigurationKey,
    object? DefaultValue,
    bool? Required,
    bool? SupportsReloading,
    bool IsExternal,
    AutoDepAttribution? Attribution = null);

internal enum GeneratedFieldKind
{
    Dependency,
    Configuration
}
