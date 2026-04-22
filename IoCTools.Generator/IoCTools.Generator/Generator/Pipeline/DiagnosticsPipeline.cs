namespace IoCTools.Generator.Generator.Pipeline;

using System.Collections.Immutable;

using IoCTools.Generator.Diagnostics;
using IoCTools.Generator.Utilities;

internal static class DiagnosticsPipeline
{
    internal static void Attach(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<ServiceClassInfo> serviceClasses)
    {
        var styleOptionsProvider = context.AnalyzerConfigOptionsProvider
            .Combine(context.CompilationProvider)
            .Select(static (input,
                _) => GeneratorStyleOptions.From(input.Left, input.Right));

        var autoDepsOptionsProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (opts, _) => AutoDepsOptionsReader.Read(opts.GlobalOptions));

        var excludedPrefixesProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
            {
                var config = DiagnosticUtilities.GetDiagnosticConfiguration(options);
                return config.ExcludedNamespacePrefixes;
            });

        var referencedAssemblyTypes = context.CompilationProvider
            .Combine(excludedPrefixesProvider)
            .Select(static (input,
                _) =>
            {
                var (compilation, excludedPrefixes) = input;
                var referencedTypes = new List<INamedTypeSymbol>();
                foreach (var reference in compilation.References)
                {
                    if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol asm) continue;
                    var name = asm.Name;

                    // Check against excluded namespace prefixes
                    var isExcluded = false;
                    foreach (var prefix in excludedPrefixes)
                    {
                        if (name.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            isExcluded = true;
                            break;
                        }
                    }

                    if (isExcluded)
                        continue;

                    var referencesAbstractions = asm.Modules.Any(m =>
                        m.ReferencedAssemblies.Any(ra => ra.Name == "IoCTools.Abstractions"));
                    if (!referencesAbstractions) continue;

                    DiagnosticScan.ScanNamespaceForTypes(asm.GlobalNamespace, referencedTypes);
                }

                return referencedTypes.ToImmutableArray();
            });

        var diagnosticsInput = serviceClasses
            .Collect()
            .Combine(referencedAssemblyTypes)
            .Combine(context.CompilationProvider)
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Combine(styleOptionsProvider)
            .Combine(autoDepsOptionsProvider)
            .Select(static (input,
                _) =>
            {
                var (((((services, referencedTypes), compilation), config), styleOptions), autoDepsOptions) = input;
                return ((services, referencedTypes, compilation), config, styleOptions, autoDepsOptions);
            });

        context.RegisterSourceOutput(diagnosticsInput, DiagnosticsRunner.EmitWithReferencedTypes);
    }
}
