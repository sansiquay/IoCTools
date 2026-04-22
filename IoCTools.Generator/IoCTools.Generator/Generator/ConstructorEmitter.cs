namespace IoCTools.Generator.Generator;

using System.Collections.Immutable;
using System.Text;

using CodeGeneration;

using IoCTools.Generator.Utilities;

using Microsoft.CodeAnalysis.Text;

internal static class ConstructorEmitter
{
    public static void EmitSingleConstructor(SourceProductionContext context,
        ServiceClassInfo serviceInfo,
        ImmutableDictionary<string, string> autoDepsOptions)
    {
        try
        {
            if (serviceInfo.SemanticModel == null || serviceInfo.ClassDeclaration == null)
                return;

            var hasRegisterAsOnly = serviceInfo.ClassSymbol.GetAttributes()
                .Any(attr => AttributeTypeChecker.IsRegisterAsAttribute(attr));

            var hasInjectFields = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(serviceInfo.ClassSymbol);
            var hasInjectConfigurationFields =
                ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(serviceInfo.ClassSymbol);
            var hasDependsOnAttribute = serviceInfo.ClassSymbol.GetAttributes()
                .Any(AttributeTypeChecker.IsDependsOnAttribute);
            var hasConditionalServiceAttribute = serviceInfo.ClassSymbol.GetAttributes()
                .Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
            var hasRegisterAsAllAttribute = serviceInfo.ClassSymbol.GetAttributes()
                .Any(attr => AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute));
            var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(serviceInfo.ClassSymbol);
            var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(serviceInfo.ClassSymbol);

            if (hasRegisterAsOnly && !hasInjectFields && !hasInjectConfigurationFields && !hasDependsOnAttribute &&
                !hasConditionalServiceAttribute && !hasRegisterAsAllAttribute && !hasLifetimeAttribute &&
                !isHostedService)
                return;

            var hierarchyDependencies = DependencyAnalyzer.GetConstructorDependencies(
                serviceInfo.ClassSymbol, serviceInfo.SemanticModel);

            // Merge any resolved auto-deps (built-in ILogger, assembly-level AutoDep<T>,
            // profiles, etc.) into the hierarchy as Level-0 DependsOn entries before rendering.
            // The resolver has already done reconciliation against explicit DependsOn and all
            // opt-outs, so the merger just surfaces what's left.
            AutoDepsMerger.MergeAutoDepsIntoHierarchy(
                hierarchyDependencies,
                serviceInfo.SemanticModel.Compilation,
                serviceInfo.ClassSymbol,
                autoDepsOptions);

            var constructorCode = GenerateInheritanceAwareConstructorCodeWithContext(
                serviceInfo.ClassDeclaration, hierarchyDependencies, serviceInfo.SemanticModel, context,
                autoDepsOptions);

            if (!string.IsNullOrEmpty(constructorCode))
            {
                // IoCToolsAutoDepsReport=true prepends a human-readable auto-deps summary
                // comment to the top of the generated constructor file. The resolver is
                // re-invoked (cheap; same compilation + options) so the reporter can see
                // attribution metadata that isn't stored on the hierarchy model.
                if (IsReportEnabled(autoDepsOptions))
                {
                    var resolved = IoCTools.Generator.Shared.AutoDepsResolver.ResolveForServiceWithSymbols(
                        serviceInfo.SemanticModel.Compilation, serviceInfo.ClassSymbol, autoDepsOptions);

                    var suppressed = AutoDepsReporter.CollectSuppressedDescriptions(serviceInfo.ClassSymbol);
                    var report = AutoDepsReporter.BuildReport(serviceInfo.ClassSymbol, resolved, suppressed);
                    constructorCode = report + constructorCode;
                }

                var canonicalKey = serviceInfo.ClassSymbol.ToDisplayString();
                var sanitizedTypeName = FileNameUtilities.Sanitize(canonicalKey);
                var fileName = $"{sanitizedTypeName}_Constructor.g.cs";

                context.AddSource(fileName, SourceText.From(constructorCode, Encoding.UTF8));
            }
        }
        catch (Exception ex)
        {
            var typeName = serviceInfo.ClassSymbol.ToDisplayString();
            GeneratorDiagnostics.Report(context, "IOC995", "Constructor generation error",
                $"Failed to generate constructor for {typeName}: {ex}");
        }
    }

    private static bool IsReportEnabled(ImmutableDictionary<string, string> options)
    {
        if (options is null) return false;
        if (!options.TryGetValue("build_property.IoCToolsAutoDepsReport", out var raw))
            return false;
        return bool.TryParse(raw, out var parsed) && parsed;
    }

    private static string GenerateInheritanceAwareConstructorCodeWithContext(
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        SemanticModel semanticModel,
        SourceProductionContext context,
        ImmutableDictionary<string, string> autoDepsOptions)
    {
        try
        {
            return ConstructorGenerator.GenerateInheritanceAwareConstructorCodeWithContext(
                classDeclaration, hierarchyDependencies, semanticModel, context, autoDepsOptions);
        }
        catch (Exception ex)
        {
            GeneratorDiagnostics.Report(context, "IOC992", "Constructor generation adapter error", ex.Message);
            return string.Empty;
        }
    }
}
