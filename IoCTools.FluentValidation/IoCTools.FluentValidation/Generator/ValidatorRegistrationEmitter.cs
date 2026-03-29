namespace IoCTools.FluentValidation.Generator;

using System;
using System.Collections.Immutable;
using System.Text;

using CodeGeneration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using Models;

/// <summary>
/// Emits the partial method implementation that registers FluentValidation validators.
/// </summary>
internal static class ValidatorRegistrationEmitter
{
    /// <summary>
    /// Emits validator registration code as a partial method implementation.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="validators">Discovered validators.</param>
    /// <param name="compilation">The current compilation.</param>
    public static void Emit(
        SourceProductionContext context,
        ImmutableArray<ValidatorClassInfo> validators,
        Compilation compilation)
    {
        try
        {
            if (validators.Length == 0)
                return;

            // Compute namespace using EXACT same logic as RegistrationEmitter.cs lines 54-57
            var assemblyName = compilation.AssemblyName ?? "UnknownAssembly";
            var safeRootNamespace = assemblyName.Replace("-", "_").Replace(" ", "_");
            var extensionNamespace = safeRootNamespace + ".Extensions.Generated";
            var safeAssemblyName = safeRootNamespace.Replace(".", "");

            var code = ValidatorRegistrationGenerator.GeneratePartialMethodBody(
                validators, safeAssemblyName, extensionNamespace);

            context.AddSource(
                $"FluentValidation_{safeAssemblyName}.g.cs",
                SourceText.From(code, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Generator never throws per project constraint — emit diagnostic on failure
            var descriptor = new DiagnosticDescriptor(
                "IOC999",
                "FluentValidation generator failed",
                "FluentValidation registration generation failed: {0}",
                "IoCTools.FluentValidation",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, ex.Message));
        }
    }
}
