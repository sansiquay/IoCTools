using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace IoCTools.Testing.Tests;

using System.Collections.Immutable;

/// <summary>
/// Tests for FluentValidation-aware fixture generation.
/// Verifies that IValidator&lt;T&gt; parameters get SetupValidationSuccess/Failure helpers
/// when FluentValidation is in compilation references, and standard mocks otherwise.
/// Services use explicit constructors so the fixture generator can find parameters
/// without depending on the main generator running first.
/// </summary>
public sealed class FluentValidationFixtureTests
{
    #region Helper

    /// <summary>
    /// Runs the testing generator with FluentValidation assembly added to references.
    /// Services have explicit constructors so the fixture generator can discover parameters.
    /// </summary>
    private static TestHelper.GenerationResult GenerateWithFluentValidation(string source)
    {
        var iocToolsAssembly = typeof(Abstractions.Annotations.ScopedAttribute).Assembly;
        var iocTestingAssembly = typeof(IoCTools.Testing.Annotations.CoverAttribute<>).Assembly;

        // Use trusted platform assemblies for proper type resolution
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => MetadataReference.CreateFromFile(p))
            .Cast<MetadataReference>()
            .ToList();

        var metadataRefs = new List<MetadataReference>(trustedAssemblies)
        {
            MetadataReference.CreateFromFile(iocToolsAssembly.Location),
            MetadataReference.CreateFromFile(iocTestingAssembly.Location),
        };

        // Add FluentValidation assembly reference
        var fluentValidationAssembly = typeof(FluentValidation.IValidator<>).Assembly;
        metadataRefs.Add(MetadataReference.CreateFromFile(fluentValidationAssembly.Location));

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

        var compilation = CSharpCompilation.Create(
            "Test",
            new[] { syntaxTree },
            metadataRefs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable));

        // Run only the testing generator (services have explicit constructors)
        var testingGenerator = new IoCTools.Testing.IoCToolsTestingGenerator();
        var driver = CSharpGeneratorDriver.Create(new[]
            {
                testingGenerator.AsSourceGenerator()
            },
            Array.Empty<AdditionalText>(),
            new CSharpParseOptions(LanguageVersion.Preview));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees
            .Skip(1) // Skip the original source tree
            .ToImmutableArray();

        return new TestHelper.GenerationResult(generatedTrees, diagnostics.ToImmutableArray());
    }

    /// <summary>
    /// Runs the testing generator WITHOUT FluentValidation in references.
    /// Uses trusted platform assemblies for proper compilation.
    /// </summary>
    private static TestHelper.GenerationResult GenerateWithoutFluentValidation(string source)
    {
        var iocToolsAssembly = typeof(Abstractions.Annotations.ScopedAttribute).Assembly;
        var iocTestingAssembly = typeof(IoCTools.Testing.Annotations.CoverAttribute<>).Assembly;

        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => !string.IsNullOrEmpty(p))
            // Exclude FluentValidation from trusted assemblies
            .Where(p => !p.Contains("FluentValidation", StringComparison.OrdinalIgnoreCase))
            .Select(p => MetadataReference.CreateFromFile(p))
            .Cast<MetadataReference>()
            .ToList();

        var metadataRefs = new List<MetadataReference>(trustedAssemblies)
        {
            MetadataReference.CreateFromFile(iocToolsAssembly.Location),
            MetadataReference.CreateFromFile(iocTestingAssembly.Location),
        };

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

        var compilation = CSharpCompilation.Create(
            "Test",
            new[] { syntaxTree },
            metadataRefs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable));

        var testingGenerator = new IoCTools.Testing.IoCToolsTestingGenerator();
        var driver = CSharpGeneratorDriver.Create(new[]
            {
                testingGenerator.AsSourceGenerator()
            },
            Array.Empty<AdditionalText>(),
            new CSharpParseOptions(LanguageVersion.Preview));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees
            .Skip(1)
            .ToImmutableArray();

        return new TestHelper.GenerationResult(generatedTrees, diagnostics.ToImmutableArray());
    }

    #endregion

    #region With FluentValidation Reference

    [Fact]
    public void Service_With_IValidator_And_FluentValidation_Reference_Generates_SetupValidationSuccess()
    {
        // Arrange - Service has explicit constructor so fixture generator can find parameters
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;
            using FluentValidation;

            namespace TestProject;

            public class OrderCommand { public string Name { get; set; } }

            public partial class OrderService
            {
                private readonly IValidator<OrderCommand> _orderValidator;

                public OrderService(IValidator<OrderCommand> orderValidator)
                {
                    _orderValidator = orderValidator;
                }
            }

            [Cover<OrderService>]
            public partial class OrderServiceTests { }
            """;

        // Act
        var result = GenerateWithFluentValidation(source);

        // Assert
        var fixtureSource = result.GeneratedTrees
            .Select(t => t.ToString())
            .FirstOrDefault(s => s.Contains("OrderServiceTests"));

        fixtureSource.Should().NotBeNull("fixture should be generated for OrderServiceTests");
        fixtureSource.Should().Contain("SetupOrderValidatorValidationSuccess", "should generate validation success helper named after parameter");
        fixtureSource.Should().Contain("SetupOrderValidatorValidationFailure", "should generate validation failure helper named after parameter");
    }

    [Fact]
    public void Generated_Helpers_Setup_Both_Validate_And_ValidateAsync()
    {
        // Arrange
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;
            using FluentValidation;

            namespace TestProject;

            public class OrderCommand { public string Name { get; set; } }

            public partial class OrderService
            {
                private readonly IValidator<OrderCommand> _orderValidator;

                public OrderService(IValidator<OrderCommand> orderValidator)
                {
                    _orderValidator = orderValidator;
                }
            }

            [Cover<OrderService>]
            public partial class OrderServiceTests { }
            """;

        // Act
        var result = GenerateWithFluentValidation(source);

        // Assert
        var fixtureSource = result.GeneratedTrees
            .Select(t => t.ToString())
            .FirstOrDefault(s => s.Contains("OrderServiceTests"));

        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("ValidationResult", "should use FluentValidation.Results.ValidationResult");
        fixtureSource.Should().Contain("Validate(It.IsAny<", "should set up sync Validate");
        fixtureSource.Should().Contain("ValidateAsync(It.IsAny<", "should set up async ValidateAsync");
    }

    #endregion

    #region Without FluentValidation Reference

    [Fact]
    public void Service_With_IValidator_Without_FluentValidation_Reference_Omits_Helpers()
    {
        // Arrange - Define our own IValidator<T> to simulate the parameter without the real assembly
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;

            namespace FluentValidation
            {
                public interface IValidator<T> { }
            }

            namespace TestProject
            {
                public class OrderCommand { public string Name { get; set; } }

                public partial class OrderService
                {
                    private readonly FluentValidation.IValidator<OrderCommand> _orderValidator;

                    public OrderService(FluentValidation.IValidator<OrderCommand> orderValidator)
                    {
                        _orderValidator = orderValidator;
                    }
                }

                [Cover<OrderService>]
                public partial class OrderServiceTests { }
            }
            """;

        // Act - Generate WITHOUT FluentValidation assembly reference
        var result = GenerateWithoutFluentValidation(source);

        // Assert
        var fixtureSource = result.GeneratedTrees
            .Select(t => t.ToString())
            .FirstOrDefault(s => s.Contains("OrderServiceTests"));

        fixtureSource.Should().NotBeNull("fixture should still be generated");
        fixtureSource.Should().Contain("Mock<IValidator<OrderCommand>>", "should have standard mock field");
        fixtureSource.Should().NotContain("SetupValidationSuccess", "should NOT generate validation helpers without FluentValidation reference");
        fixtureSource.Should().NotContain("SetupValidationFailure", "should NOT generate validation helpers without FluentValidation reference");
    }

    #endregion

    #region Multiple Validators

    [Fact]
    public void Service_With_Multiple_IValidator_Params_Gets_Named_Helpers()
    {
        // Arrange
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;
            using FluentValidation;

            namespace TestProject;

            public class OrderCommand { public string Name { get; set; } }
            public class AddressCommand { public string Street { get; set; } }

            public partial class OrderService
            {
                private readonly IValidator<OrderCommand> _orderValidator;
                private readonly IValidator<AddressCommand> _addressValidator;

                public OrderService(
                    IValidator<OrderCommand> orderValidator,
                    IValidator<AddressCommand> addressValidator)
                {
                    _orderValidator = orderValidator;
                    _addressValidator = addressValidator;
                }
            }

            [Cover<OrderService>]
            public partial class OrderServiceTests { }
            """;

        // Act
        var result = GenerateWithFluentValidation(source);

        // Assert
        var fixtureSource = result.GeneratedTrees
            .Select(t => t.ToString())
            .FirstOrDefault(s => s.Contains("OrderServiceTests"));

        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("SetupOrderValidatorValidationSuccess", "should name helper after order validator parameter");
        fixtureSource.Should().Contain("SetupAddressValidatorValidationSuccess", "should name helper after address validator parameter");
        fixtureSource.Should().Contain("SetupOrderValidatorValidationFailure", "should name failure helper after order validator parameter");
        fixtureSource.Should().Contain("SetupAddressValidatorValidationFailure", "should name failure helper after address validator parameter");
    }

    #endregion
}
