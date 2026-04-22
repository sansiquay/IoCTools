namespace IoCTools.Generator;

using Generator.Pipeline;

using Utilities;

[Generator]
public sealed class DependencyInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var serviceClasses = ServiceClassPipeline.Build(context);

        RegistrationPipeline.Attach(context, serviceClasses);

        // Extract only auto-deps-relevant MSBuild properties into an equatable
        // ImmutableDictionary so the incremental pipeline can cache on it without re-running
        // constructor emit on unrelated property churn.
        var autoDepsOptionsProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (opts, _) => AutoDepsOptionsReader.Read(opts.GlobalOptions));

        var serviceClassesWithOptions = serviceClasses.Combine(autoDepsOptionsProvider);
        context.RegisterSourceOutput(serviceClassesWithOptions, static (ctx, tuple) =>
            ConstructorEmitter.EmitSingleConstructor(ctx, tuple.Left, tuple.Right));

        DiagnosticsPipeline.Attach(context, serviceClasses);
    }
}
