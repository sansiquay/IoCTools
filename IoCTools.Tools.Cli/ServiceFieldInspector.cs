namespace IoCTools.Tools.Cli;

using Generator.Analysis;
using Generator.Generator;
using Generator.Generator.Intent;

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

        foreach (var (declaration, semanticModel) in semanticContext)
        {
            var symbol = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
            if (symbol == null) continue;
            var key = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!seen.Add(key)) continue;
            if (!IsServiceCandidate(symbol, declaration)) continue;
            if (!MatchesFilter(symbol, typeFilters)) continue;

            var dependencyFields = DependsOnFieldAnalyzer.GetRawDependsOnFieldsForTypeWithExternalFlag(symbol)
                .Select(dep => new GeneratedFieldInfo(dep.FieldName,
                    FormatTypeName(dep.ServiceType),
                    GeneratedFieldKind.Dependency,
                    "DependsOn",
                    null,
                    null,
                    null,
                    null,
                    dep.IsExternal))
                .ToList();

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
                    false))
                .ToList();

            var report = new ServiceFieldReport(symbol.ToDisplayString(TypeFormat),
                declaration.SyntaxTree.FilePath ?? filePath ?? "<unknown>",
                dependencyFields,
                configFields);
            matches.Add(report);
        }

        return matches;
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
        if (string.IsNullOrWhiteSpace(filter)) return false;
        var comparison = StringComparison.Ordinal;
        return string.Equals(symbol.Name, filter, comparison) ||
               string.Equals(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), filter, comparison) ||
               string.Equals(symbol.ToDisplayString(TypeFormat), filter, comparison);
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
    bool IsExternal);

internal enum GeneratedFieldKind
{
    Dependency,
    Configuration
}
