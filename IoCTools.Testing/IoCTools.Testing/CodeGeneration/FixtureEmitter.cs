namespace IoCTools.Testing.CodeGeneration;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using IoCTools.Testing.Analysis;
using IoCTools.Testing.Models;
using IoCTools.Testing.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

internal static class FixtureEmitter
{
    public static void Emit(SourceProductionContext context, TestClassInfo testClass)
    {
        var parameters = ConstructorReader.GetConstructorParameters(testClass.ServiceSymbol);

        if (parameters.IsEmpty)
        {
            // No constructor found - emit diagnostic
            return;
        }

        var hasFluentValidation = testClass.SemanticModel?.Compilation != null &&
            FluentValidationFixtureHelper.HasFluentValidationReference(testClass.SemanticModel.Compilation);

        var source = GenerateFixtureSource(testClass, parameters, hasFluentValidation);
        var fileName = $"{testClass.TestClassName}.Fixture.g.cs";
        context.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateFixtureSource(TestClassInfo testClass, ImmutableArray<IParameterSymbol> parameters, bool hasFluentValidation)
    {
        var sb = new StringBuilder();

        // Namespace
        if (!string.IsNullOrEmpty(testClass.TestClassNamespace))
        {
            sb.AppendLine($"namespace {testClass.TestClassNamespace}");
            sb.AppendLine("{");
            sb.AppendLine();
        }

        // Usings
        var namespaces = CollectNamespaces(parameters);
        namespaces.Add("Moq");
        namespaces.Add("System");

        if (hasFluentValidation && parameters.Any(p => FluentValidationFixtureHelper.IsFluentValidatorType(p.Type)))
        {
            namespaces.Add("System.Linq");
            namespaces.Add("System.Threading");
        }

        foreach (var ns in namespaces.OrderBy(n => n))
        {
            sb.AppendLine($"    using {ns};");
        }
        sb.AppendLine();

        // Class declaration (partial augmentation)
        sb.AppendLine($"    public partial class {testClass.TestClassName}");
        sb.AppendLine("    {");

        // Mock<T> fields
        foreach (var param in parameters)
        {
            var fieldName = TypeNameUtilities.GetMockFieldName(param.Type);
            var paramType = GetTypeNameWithoutGlobal(param.Type);
            sb.AppendLine($"        protected readonly Mock<{paramType}> {fieldName} = new();");
        }
        sb.AppendLine();

        // CreateSut() factory method
        var serviceName = GetTypeNameWithoutGlobal(testClass.ServiceSymbol);
        sb.AppendLine($"        public {serviceName} CreateSut() => new(");
        var paramList = string.Join(",\n            ", parameters.Select(p =>
        {
            var fieldName = TypeNameUtilities.GetMockFieldName(p.Type);
            return $"{fieldName}.Object";
        }));
        sb.AppendLine($"            {paramList}");
        sb.AppendLine("        );");
        sb.AppendLine();

        // Setup helper methods
        foreach (var param in parameters)
        {
            var methodName = TypeNameUtilities.GetSetupMethodName(param.Type);
            var fieldName = TypeNameUtilities.GetMockFieldName(param.Type);
            var paramType = GetTypeNameWithoutGlobal(param.Type);

            if (ConstructorReader.IsConfigurationParameter(param))
            {
                // Configuration-specific helpers
                if (param.Type.Name == "IConfiguration")
                {
                    sb.AppendLine($"        protected void ConfigureIConfiguration(Func<string, object?> valueProvider)");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            {fieldName}.Setup(x => x.GetValue<string>(It.IsAny<string>()))");
                    sb.AppendLine("                .Returns((string key) => valueProvider(key)?.ToString());");
                    sb.AppendLine($"            {fieldName}.Setup(x => x[It.IsAny<string>()])");
                    sb.AppendLine("                .Returns((string key) => valueProvider(key)?.ToString());");
                    sb.AppendLine("        }");
                    sb.AppendLine();
                }
                else if (ConstructorReader.GetOptionsType(param) is { } optionsType)
                {
                    var optionsTypeName = GetTypeNameWithoutGlobal(optionsType);
                    sb.AppendLine($"        protected void Configure{optionsType.Name}(Action<{optionsTypeName}> configureOptions)");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            var options = Microsoft.Extensions.Options.Options.Create(new {optionsTypeName}());");
                    sb.AppendLine("            configureOptions(options.Value);");
                    sb.AppendLine($"            {fieldName}.Setup(x => x.Value).Returns(options.Value);");
                    sb.AppendLine("        }");
                    sb.AppendLine();
                }
            }
            else
            {
                // Standard typed helper
                sb.AppendLine($"        protected void {methodName}(Action<Mock<{paramType}>> configure) => configure({fieldName});");
            }
        }

        // FluentValidation setup helpers
        if (hasFluentValidation)
        {
            foreach (var param in parameters)
            {
                if (FluentValidationFixtureHelper.IsFluentValidatorType(param.Type))
                {
                    var fieldName = TypeNameUtilities.GetMockFieldName(param.Type);
                    var validatedTypeName = ((INamedTypeSymbol)param.Type).TypeArguments[0].ToDisplayString();

                    sb.AppendLine();
                    sb.Append(FluentValidationFixtureHelper.GenerateSetupHelpers(fieldName, validatedTypeName, param.Name));
                }
            }
        }

        sb.AppendLine("    }");

        if (!string.IsNullOrEmpty(testClass.TestClassNamespace))
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static HashSet<string> CollectNamespaces(ImmutableArray<IParameterSymbol> parameters)
    {
        var namespaces = new HashSet<string>();
        foreach (var param in parameters)
        {
            CollectNamespacesForType(param.Type, namespaces);
        }
        return namespaces;
    }

    private static void CollectNamespacesForType(ITypeSymbol type, HashSet<string> namespaces)
    {
        var current = type;

        while (current != null)
        {
            var ns = current.ContainingNamespace?.ToString();
            if (!string.IsNullOrEmpty(ns) && !current.ContainingNamespace.IsGlobalNamespace)
            {
                namespaces.Add(ns);
            }

            if (current is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                foreach (var typeArg in namedType.TypeArguments)
                {
                    CollectNamespacesForType(typeArg, namespaces);
                }
            }

            current = current.ContainingType;
        }
    }

    private static string GetTypeNameWithoutGlobal(ITypeSymbol type)
    {
        var format = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        var name = type.ToDisplayString(format);
        return name.Replace("global::", "");
    }
}
