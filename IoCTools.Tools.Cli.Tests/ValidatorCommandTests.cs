namespace IoCTools.Tools.Cli.Tests;

using System.Text;

using FluentAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

/// <summary>
/// Tests for the validator CLI commands: ValidatorInspector discovery,
/// ValidatorPrinter formatting, composition graph building, and lifetime tracing.
/// </summary>
[Collection("CLI Execution")]
public sealed class ValidatorCommandTests
{
    #region Helper Methods

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Add references for basic compilation
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location)
        };

        // Also add System.Runtime for core types
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var runtimeRef = MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll"));

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references.Append(runtimeRef),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static OutputContext CreateTextOutput() => OutputContext.Create(false, false);

    private static OutputContext CreateJsonOutput() => OutputContext.Create(true, false);

    private static string CaptureConsoleOutput(Action action)
    {
        var originalOut = Console.Out;
        var sb = new StringBuilder();
        Console.SetOut(new StringWriter(sb));
        try
        {
            action();
            return sb.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion

    #region ValidatorInspector - Discovery

    [Fact]
    public void DiscoverValidators_FindsValidatorWithModelType()
    {
        // Arrange
        var source = @"
namespace FluentValidation
{
    public abstract class AbstractValidator<T> { }
}

namespace TestApp
{
    public class Order { }

    public class OrderValidator : FluentValidation.AbstractValidator<Order> { }
}";
        var compilation = CreateCompilation(source);

        // Act
        var validators = ValidatorInspector.DiscoverValidators(compilation);

        // Assert
        validators.Should().HaveCount(1);
        validators[0].FullName.Should().Contain("OrderValidator");
        validators[0].ModelType.Should().Be("Order");
    }

    [Fact]
    public void DiscoverValidators_DetectsLifetimeFromAttribute()
    {
        // Arrange
        var source = @"
namespace FluentValidation
{
    public abstract class AbstractValidator<T> { }
}

namespace IoCTools.Abstractions.Annotations
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class ScopedAttribute : System.Attribute { }
}

namespace TestApp
{
    public class Order { }

    [IoCTools.Abstractions.Annotations.Scoped]
    public class OrderValidator : FluentValidation.AbstractValidator<Order> { }
}";
        var compilation = CreateCompilation(source);

        // Act
        var validators = ValidatorInspector.DiscoverValidators(compilation);

        // Assert
        validators.Should().HaveCount(1);
        validators[0].Lifetime.Should().Be("Scoped");
    }

    [Fact]
    public void DiscoverValidators_ReturnsEmptyForNoValidators()
    {
        // Arrange
        var source = @"
namespace TestApp
{
    public class NotAValidator { }
}";
        var compilation = CreateCompilation(source);

        // Act
        var validators = ValidatorInspector.DiscoverValidators(compilation);

        // Assert
        validators.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverValidators_FindsMultipleValidators()
    {
        // Arrange
        var source = @"
namespace FluentValidation
{
    public abstract class AbstractValidator<T> { }
}

namespace TestApp
{
    public class Order { }
    public class Customer { }

    public class OrderValidator : FluentValidation.AbstractValidator<Order> { }
    public class CustomerValidator : FluentValidation.AbstractValidator<Customer> { }
}";
        var compilation = CreateCompilation(source);

        // Act
        var validators = ValidatorInspector.DiscoverValidators(compilation);

        // Assert
        validators.Should().HaveCount(2);
        validators.Select(v => v.ModelType).Should().Contain("Order");
        validators.Select(v => v.ModelType).Should().Contain("Customer");
    }

    [Fact]
    public void DiscoverValidators_DetectsNullLifetimeWhenNoAttribute()
    {
        // Arrange
        var source = @"
namespace FluentValidation
{
    public abstract class AbstractValidator<T> { }
}

namespace TestApp
{
    public class Order { }
    public class OrderValidator : FluentValidation.AbstractValidator<Order> { }
}";
        var compilation = CreateCompilation(source);

        // Act
        var validators = ValidatorInspector.DiscoverValidators(compilation);

        // Assert
        validators.Should().HaveCount(1);
        validators[0].Lifetime.Should().BeNull();
    }

    #endregion

    #region ValidatorInspector - Composition Graph

    [Fact]
    public void BuildCompositionTree_CreatesTreeFromEdges()
    {
        // Arrange - manually build validators with composition edges
        var parent = new ValidatorInfo(
            "TestApp.OrderValidator",
            "Order",
            "Scoped",
            new[] { new CompositionEdgeInfo("AddressValidator", "SetValidator", false) });

        var child = new ValidatorInfo(
            "TestApp.AddressValidator",
            "Address",
            "Scoped",
            Array.Empty<CompositionEdgeInfo>());

        var validators = new[] { parent, child };

        // Act
        var tree = ValidatorInspector.BuildCompositionTree(validators);

        // Assert
        tree.Should().HaveCount(1); // Only root (OrderValidator)
        tree[0].Validator.FullName.Should().Be("TestApp.OrderValidator");
        tree[0].Children.Should().HaveCount(1);
        tree[0].Children[0].Edge.ChildValidatorType.Should().Be("AddressValidator");
    }

    [Fact]
    public void BuildCompositionTree_HandlesIsolatedValidators()
    {
        // Arrange - validators with no composition edges
        var v1 = new ValidatorInfo("TestApp.OrderValidator", "Order", "Scoped", Array.Empty<CompositionEdgeInfo>());
        var v2 = new ValidatorInfo("TestApp.CustomerValidator", "Customer", "Singleton", Array.Empty<CompositionEdgeInfo>());

        // Act
        var tree = ValidatorInspector.BuildCompositionTree(new[] { v1, v2 });

        // Assert
        tree.Should().HaveCount(2);
    }

    #endregion

    #region ValidatorPrinter - JSON Output

    [Fact]
    public void WriteList_JsonMode_ProducesValidJson()
    {
        // Arrange
        var validators = new[]
        {
            new ValidatorInfo("TestApp.OrderValidator", "Order", "Scoped",
                new[] { new CompositionEdgeInfo("AddressValidator", "SetValidator", false) })
        };

        // Act
        var output = CaptureConsoleOutput(() =>
        {
            var ctx = CreateJsonOutput();
            ValidatorPrinter.WriteList(validators, null, ctx);
        });

        // Assert
        output.Should().Contain("\"validator\"");
        output.Should().Contain("\"modelType\"");
        output.Should().Contain("OrderValidator");
        output.Should().Contain("\"lifetime\"");
    }

    [Fact]
    public void WriteGraph_JsonMode_ProducesValidJson()
    {
        // Arrange
        var child = new ValidatorInfo("TestApp.AddressValidator", "Address", "Scoped",
            Array.Empty<CompositionEdgeInfo>());
        var parent = new ValidatorInfo("TestApp.OrderValidator", "Order", "Scoped",
            new[] { new CompositionEdgeInfo("AddressValidator", "SetValidator", false) });
        var validators = new[] { parent, child };

        // Act
        var output = CaptureConsoleOutput(() =>
        {
            var ctx = CreateJsonOutput();
            ValidatorPrinter.WriteGraph(validators, ctx);
        });

        // Assert
        output.Should().Contain("\"validator\"");
        output.Should().Contain("\"children\"");
        output.Should().Contain("OrderValidator");
        output.Should().Contain("\"resolved\"");
        output.Should().Contain("\"method\"");
        output.Should().Contain("\"isDirect\"");
        output.Should().Contain("\"lifetime\"");
    }

    [Fact]
    public void WriteWhy_JsonMode_EmitsStructuredExplanation()
    {
        // Arrange
        var child = new ValidatorInfo("TestApp.AddressValidator", "Address", "Scoped",
            Array.Empty<CompositionEdgeInfo>());
        var parent = new ValidatorInfo("TestApp.OrderValidator", "Order", "Singleton",
            new[] { new CompositionEdgeInfo("AddressValidator", "SetValidator", false) });
        var validators = new[] { parent, child };

        // Act
        var output = CaptureConsoleOutput(() =>
        {
            var ctx = CreateJsonOutput();
            ValidatorPrinter.WriteWhy("OrderValidator", validators, ctx);
        });

        // Assert
        output.Should().Contain("\"validator\"");
        output.Should().Contain("\"lifetime\"");
        output.Should().Contain("\"reason\"");
        output.Should().Contain("\"steps\"");
        output.Should().Contain("\"kind\"");
        output.Should().Contain("\"target\"");
        output.Should().Contain("\"method\"");
    }

    #endregion

    #region ValidatorInspector - Lifetime Tracing

    [Fact]
    public void TraceLifetime_ExplainsDirectAttribute()
    {
        // Arrange
        var validators = new[]
        {
            new ValidatorInfo("TestApp.OrderValidator", "Order", "Scoped",
                Array.Empty<CompositionEdgeInfo>())
        };

        // Act
        var explanation = ValidatorInspector.TraceLifetime("OrderValidator", validators);

        // Assert
        explanation.Should().Contain("Scoped");
        explanation.Should().Contain("[Scoped] attribute");
    }

    [Fact]
    public void TraceLifetime_ReportsNotFoundForUnknownValidator()
    {
        // Arrange
        var validators = Array.Empty<ValidatorInfo>();

        // Act
        var explanation = ValidatorInspector.TraceLifetime("NonExistent", validators);

        // Assert
        explanation.Should().Contain("not found");
    }

    [Fact]
    public void TraceLifetime_TracesCompositionChain()
    {
        // Arrange
        var child = new ValidatorInfo("TestApp.AddressValidator", "Address", "Scoped",
            Array.Empty<CompositionEdgeInfo>());
        var parent = new ValidatorInfo("TestApp.OrderValidator", "Order", "Singleton",
            new[] { new CompositionEdgeInfo("AddressValidator", "SetValidator", false) });
        var validators = new[] { parent, child };

        // Act
        var explanation = ValidatorInspector.TraceLifetime("OrderValidator", validators);

        // Assert
        explanation.Should().Contain("Singleton");
        explanation.Should().Contain("AddressValidator");
        explanation.Should().Contain("Scoped");
    }

    [Fact]
    public void TraceLifetime_ReportsNoLifetimeAttribute()
    {
        // Arrange
        var validators = new[]
        {
            new ValidatorInfo("TestApp.OrderValidator", "Order", null,
                Array.Empty<CompositionEdgeInfo>())
        };

        // Act
        var explanation = ValidatorInspector.TraceLifetime("OrderValidator", validators);

        // Assert
        explanation.Should().Contain("no lifetime attribute");
    }

    #endregion

    #region ValidatorPrinter - Text Output

    [Fact]
    public void WriteList_TextMode_ShowsValidatorsWithLifetimes()
    {
        // Arrange
        var validators = new[]
        {
            new ValidatorInfo("TestApp.OrderValidator", "Order", "Scoped",
                Array.Empty<CompositionEdgeInfo>()),
            new ValidatorInfo("TestApp.CustomerValidator", "Customer", "Singleton",
                new[] { new CompositionEdgeInfo("AddressValidator", "SetValidator", true) })
        };

        // Act
        var output = CaptureConsoleOutput(() =>
        {
            var ctx = CreateTextOutput();
            ValidatorPrinter.WriteList(validators, null, ctx);
        });

        // Assert
        output.Should().Contain("Validators: 2");
        output.Should().Contain("OrderValidator");
        output.Should().Contain("CustomerValidator");
        output.Should().Contain("Order");
        output.Should().Contain("Customer");
    }

    [Fact]
    public void WriteList_WithFilter_FiltersResults()
    {
        // Arrange
        var validators = new[]
        {
            new ValidatorInfo("TestApp.OrderValidator", "Order", "Scoped",
                Array.Empty<CompositionEdgeInfo>()),
            new ValidatorInfo("TestApp.CustomerValidator", "Customer", "Singleton",
                Array.Empty<CompositionEdgeInfo>())
        };

        // Act
        var output = CaptureConsoleOutput(() =>
        {
            var ctx = CreateTextOutput();
            ValidatorPrinter.WriteList(validators, "Order", ctx);
        });

        // Assert
        output.Should().Contain("OrderValidator");
        output.Should().NotContain("CustomerValidator");
    }

    [Fact]
    public void WriteList_NoValidators_ShowsEmptyMessage()
    {
        // Act
        var output = CaptureConsoleOutput(() =>
        {
            var ctx = CreateTextOutput();
            ValidatorPrinter.WriteList(Array.Empty<ValidatorInfo>(), null, ctx);
        });

        // Assert
        output.Should().Contain("No validators found");
    }

    #endregion
}
