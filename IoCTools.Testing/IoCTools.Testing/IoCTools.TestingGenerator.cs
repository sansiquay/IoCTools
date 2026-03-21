namespace IoCTools.Testing;

using Generator.Pipeline;
using CodeGeneration;

using Microsoft.CodeAnalysis;

/// <summary>
/// Source generator for IoCTools test fixture generation.
/// Generates Mock&lt;T&gt; fields, CreateSut() factories, and typed setup helpers
/// for test classes marked with [Cover&lt;TService&gt;].
/// </summary>
[Generator]
public sealed class IoCToolsTestingGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the generator pipeline.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var testClasses = TestFixturePipeline.Build(context);
        context.RegisterSourceOutput(testClasses, FixtureEmitter.Emit);
    }
}
