namespace IoCTools.Testing.CodeGeneration;

using System;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;

/// <summary>
/// Helper for generating FluentValidation-aware setup methods in test fixtures.
/// Detects IValidator&lt;T&gt; parameters and generates SetupValidationSuccess/Failure helpers
/// only when FluentValidation is in the compilation references.
/// </summary>
internal static class FluentValidationFixtureHelper
{
    /// <summary>
    /// Determines if a type symbol represents FluentValidation.IValidator&lt;T&gt;.
    /// Detection is name-based to avoid requiring a FluentValidation package reference.
    /// </summary>
    public static bool IsFluentValidatorType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            namedType.TypeArguments.Length == 1 &&
            namedType.Name == "IValidator" &&
            namedType.ContainingNamespace?.ToDisplayString() == "FluentValidation")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether FluentValidation is referenced by the compilation.
    /// Per D-17, helpers are only generated when FluentValidation is in references.
    /// </summary>
    public static bool HasFluentValidationReference(Compilation compilation)
    {
        return compilation.ReferencedAssemblyNames
            .Any(a => a.Name == "FluentValidation");
    }

    /// <summary>
    /// Generates SetupValidationSuccess and SetupValidationFailure helper methods
    /// for a FluentValidation IValidator&lt;T&gt; mock field.
    /// </summary>
    /// <param name="mockFieldName">The mock field name (e.g., _mockValidatorOrderCommand).</param>
    /// <param name="validatedTypeName">The fully-qualified validated type name (e.g., TestProject.OrderCommand).</param>
    /// <param name="parameterName">The constructor parameter name, PascalCased for method naming.</param>
    /// <returns>Generated C# code for the setup helper methods.</returns>
    public static string GenerateSetupHelpers(string mockFieldName, string validatedTypeName, string parameterName)
    {
        var pascalName = ToPascalCase(parameterName);
        var sb = new StringBuilder();

        sb.AppendLine($"        protected void Setup{pascalName}ValidationSuccess()");
        sb.AppendLine("        {");
        sb.AppendLine($"            {mockFieldName}.Setup(v => v.Validate(It.IsAny<{validatedTypeName}>()))");
        sb.AppendLine("                .Returns(new FluentValidation.Results.ValidationResult());");
        sb.AppendLine($"            {mockFieldName}.Setup(v => v.ValidateAsync(It.IsAny<{validatedTypeName}>(), It.IsAny<CancellationToken>()))");
        sb.AppendLine("                .ReturnsAsync(new FluentValidation.Results.ValidationResult());");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        protected void Setup{pascalName}ValidationFailure(params string[] errorMessages)");
        sb.AppendLine("        {");
        sb.AppendLine("            var failures = errorMessages.Select(m => new FluentValidation.Results.ValidationFailure(\"\", m)).ToList();");
        sb.AppendLine("            var result = new FluentValidation.Results.ValidationResult(failures);");
        sb.AppendLine($"            {mockFieldName}.Setup(v => v.Validate(It.IsAny<{validatedTypeName}>()))");
        sb.AppendLine("                .Returns(result);");
        sb.AppendLine($"            {mockFieldName}.Setup(v => v.ValidateAsync(It.IsAny<{validatedTypeName}>(), It.IsAny<CancellationToken>()))");
        sb.AppendLine("                .ReturnsAsync(result);");
        sb.AppendLine("        }");

        return sb.ToString();
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }
}
