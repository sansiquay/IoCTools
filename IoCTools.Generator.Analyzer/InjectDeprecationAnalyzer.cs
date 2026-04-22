namespace IoCTools.Generator.Analyzer;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
///     Emits <c>IOC095</c> at every <c>[IoCTools.Abstractions.Annotations.Inject]</c>
///     field. The IDE lightbulb quick-fix ( <see cref="InjectDeprecationCodeFixProvider" /> )
///     binds to this analyzer's diagnostic ID — source-generator diagnostics cannot
///     anchor code fixes, so the IOC095 emission is intentionally duplicated here
///     (Roslyn deduplicates identical diagnostic locations by ID).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InjectDeprecationAnalyzer : DiagnosticAnalyzer
{
    private const string InjectAttributeMetadataName = "IoCTools.Abstractions.Annotations.InjectAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(AnalyzerDiagnosticDescriptors.InjectDeprecated);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
    }

    private static void AnalyzeField(SymbolAnalysisContext ctx)
    {
        var field = (IFieldSymbol)ctx.Symbol;

        var injectAttr = field.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == InjectAttributeMetadataName);
        if (injectAttr is null) return;

        var location = field.Locations.FirstOrDefault() ?? Location.None;
        var typeName = field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        ctx.ReportDiagnostic(Diagnostic.Create(
            AnalyzerDiagnosticDescriptors.InjectDeprecated,
            location,
            field.Name,
            typeName));
    }
}
