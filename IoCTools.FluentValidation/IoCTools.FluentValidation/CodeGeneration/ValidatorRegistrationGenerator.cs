namespace IoCTools.FluentValidation.CodeGeneration;

using System.Collections.Immutable;
using System.Text;

using Models;

/// <summary>
/// Generates registration code for FluentValidation validators.
/// Per D-08: registers IValidator&lt;T&gt; + concrete type only (not non-generic IValidator).
/// </summary>
internal static class ValidatorRegistrationGenerator
{
    /// <summary>
    /// Generates registration lines for a single validator.
    /// Produces two registrations: IValidator&lt;T&gt; and the concrete validator type.
    /// </summary>
    /// <param name="validator">The validator info.</param>
    /// <returns>Two registration lines as a string.</returns>
    public static string GenerateRegistrationLine(ValidatorClassInfo validator)
    {
        var lifetime = validator.Lifetime ?? "Scoped";
        var validatorFqn = validator.FullyQualifiedName;
        var validatedTypeFqn = validator.ValidatedTypeFullyQualifiedName;

        var sb = new StringBuilder();
        // IValidator<T> interface registration
        sb.AppendLine($"            services.Add{lifetime}<global::FluentValidation.IValidator<{validatedTypeFqn}>, {validatorFqn}>();");
        // Concrete type registration
        sb.AppendLine($"            services.Add{lifetime}<{validatorFqn}>();");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the full partial method body containing all validator registrations.
    /// </summary>
    /// <param name="validators">All discovered validators.</param>
    /// <param name="methodNamePrefix">The safe assembly name prefix for the partial method.</param>
    /// <param name="extNameSpace">The extension namespace.</param>
    /// <returns>Complete source file content for the partial method implementation.</returns>
    public static string GeneratePartialMethodBody(
        ImmutableArray<ValidatorClassInfo> validators,
        string methodNamePrefix,
        string extNameSpace)
    {
        var registrations = new StringBuilder();
        foreach (var validator in validators)
        {
            registrations.Append(GenerateRegistrationLine(validator));
        }

        return $$"""
                 #nullable enable
                 namespace {{extNameSpace}};

                 using Microsoft.Extensions.DependencyInjection;

                 public static partial class GeneratedServiceCollectionExtensions
                 {
                     static partial void Add{{methodNamePrefix}}FluentValidationServices(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                     {
                 {{registrations.ToString().TrimEnd()}}
                     }
                 }
                 """;
    }
}
