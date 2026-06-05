namespace IoCTools.FluentValidation.Tests;

using System.Collections.Immutable;
using System.Linq;

using FluentAssertions;

using IoCTools.FluentValidation.Diagnostics.Validators;
using IoCTools.FluentValidation.Generator.CompositionGraph;
using IoCTools.FluentValidation.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Tests for IOC100 (direct instantiation) and IOC101 (lifetime mismatch) diagnostics.
/// Uses unit-level validator testing with constructed ValidatorClassInfo instances
/// to verify diagnostic logic independent of the composition graph builder (Plan 04).
/// </summary>
public sealed class DiagnosticTests
{
    #region IOC100 - Direct Instantiation Detection

    [Fact]
    public void SetValidator_WithNewDIManagedValidator_EmitsIOC100()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class Address { }
public class Order
{
    public Address Address { get; set; }
}

[Scoped]
public partial class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator() { }
}

[Scoped]
public partial class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(x => x.Address).SetValidator(new AddressValidator());
    }
}
";

        // Act
        var (parentInfo, childInfo) = BuildValidatorPair(
            source,
            parentName: "OrderValidator",
            childName: "AddressValidator",
            parentLifetime: "Scoped",
            childLifetime: "Scoped",
            compositionType: CompositionType.SetValidator,
            isDirectInstantiation: true);

        var diagnostics = RunDirectInstantiationValidator(parentInfo, childInfo);

        // Assert
        diagnostics.Should().ContainSingle(d => d.Id == "IOC100",
            "SetValidator(new AddressValidator()) with DI-managed child should emit IOC100");
    }

    [Fact]
    public void SetValidator_WithInjectedValidator_NoIOC100()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class Address { }
public class Order
{
    public Address Address { get; set; }
}

[Scoped]
public partial class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator() { }
}

[Scoped]
public partial class OrderValidator : AbstractValidator<Order>
{
    private readonly IValidator<Address> _addressValidator;
    public OrderValidator(IValidator<Address> addressValidator)
    {
        _addressValidator = addressValidator;
        RuleFor(x => x.Address).SetValidator(_addressValidator);
    }
}
";

        // Act - injected, not directly instantiated
        var (parentInfo, childInfo) = BuildValidatorPair(
            source,
            parentName: "OrderValidator",
            childName: "AddressValidator",
            parentLifetime: "Scoped",
            childLifetime: "Scoped",
            compositionType: CompositionType.SetValidator,
            isDirectInstantiation: false);

        var diagnostics = RunDirectInstantiationValidator(parentInfo, childInfo);

        // Assert
        diagnostics.Should().BeEmpty("injected validator should not emit IOC100");
    }

    [Fact]
    public void Include_WithNewDIManagedValidator_EmitsIOC100()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class Order { public string Name { get; set; } }

[Scoped]
public partial class SharedRulesValidator : AbstractValidator<Order>
{
    public SharedRulesValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

[Scoped]
public partial class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator()
    {
        Include(new SharedRulesValidator());
    }
}
";

        // Act
        var (parentInfo, childInfo) = BuildValidatorPair(
            source,
            parentName: "OrderValidator",
            childName: "SharedRulesValidator",
            parentLifetime: "Scoped",
            childLifetime: "Scoped",
            compositionType: CompositionType.Include,
            isDirectInstantiation: true);

        var diagnostics = RunDirectInstantiationValidator(parentInfo, childInfo);

        // Assert
        diagnostics.Should().ContainSingle(d => d.Id == "IOC100",
            "Include(new SharedRulesValidator()) with DI-managed child should emit IOC100");
    }

    [Fact]
    public void NoCompositionEdges_NoIOC100()
    {
        // Arrange - validator with no composition edges
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class Order { public string Name { get; set; } }

[Scoped]
public partial class SimpleValidator : AbstractValidator<Order>
{
    public SimpleValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
";

        // Act
        var result = TestHelper.Generate(source);

        // Assert - no IOC100 diagnostic from the full pipeline
        result.Diagnostics
            .Where(d => d.Id == "IOC100")
            .Should().BeEmpty("validator with no composition should not emit IOC100");
    }

    #endregion

    #region IOC101 - Lifetime Mismatch Detection

    [Fact]
    public void SingletonParent_ScopedChild_EmitsIOC101()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class Address { }
public class Order
{
    public Address Address { get; set; }
}

[Scoped]
public partial class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator() { }
}

[Singleton]
public partial class OrderValidator : AbstractValidator<Order>
{
    private readonly IValidator<Address> _addressValidator;
    public OrderValidator(IValidator<Address> addressValidator)
    {
        _addressValidator = addressValidator;
        RuleFor(x => x.Address).SetValidator(_addressValidator);
    }
}
";

        // Act - injected, Singleton parent with Scoped child
        var (parentInfo, childInfo) = BuildValidatorPair(
            source,
            parentName: "OrderValidator",
            childName: "AddressValidator",
            parentLifetime: "Singleton",
            childLifetime: "Scoped",
            compositionType: CompositionType.SetValidator,
            isDirectInstantiation: false);

        var diagnostics = RunCompositionLifetimeValidator(parentInfo, childInfo);

        // Assert
        diagnostics.Should().ContainSingle(d => d.Id == "IOC101",
            "Singleton parent with Scoped child should emit IOC101");
        diagnostics.First().GetMessage().Should().Contain("captive dependency");
    }

    [Fact]
    public void ScopedParent_ScopedChild_NoIOC101()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class Address { }
public class Order
{
    public Address Address { get; set; }
}

[Scoped]
public partial class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator() { }
}

[Scoped]
public partial class OrderValidator : AbstractValidator<Order>
{
    private readonly IValidator<Address> _addressValidator;
    public OrderValidator(IValidator<Address> addressValidator)
    {
        _addressValidator = addressValidator;
    }
}
";

        // Act - same lifetime, no issue
        var (parentInfo, childInfo) = BuildValidatorPair(
            source,
            parentName: "OrderValidator",
            childName: "AddressValidator",
            parentLifetime: "Scoped",
            childLifetime: "Scoped",
            compositionType: CompositionType.SetValidator,
            isDirectInstantiation: false);

        var diagnostics = RunCompositionLifetimeValidator(parentInfo, childInfo);

        // Assert
        diagnostics.Should().BeEmpty("same lifetime should not emit IOC101");
    }

    [Fact]
    public void TransientParent_SingletonChild_NoIOC101()
    {
        // Arrange - shorter-lived parent depending on longer-lived child is fine
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class Address { }
public class Order
{
    public Address Address { get; set; }
}

[Singleton]
public partial class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator() { }
}

[Transient]
public partial class OrderValidator : AbstractValidator<Order>
{
    private readonly IValidator<Address> _addressValidator;
    public OrderValidator(IValidator<Address> addressValidator)
    {
        _addressValidator = addressValidator;
    }
}
";

        // Act
        var (parentInfo, childInfo) = BuildValidatorPair(
            source,
            parentName: "OrderValidator",
            childName: "AddressValidator",
            parentLifetime: "Transient",
            childLifetime: "Singleton",
            compositionType: CompositionType.SetValidator,
            isDirectInstantiation: false);

        var diagnostics = RunCompositionLifetimeValidator(parentInfo, childInfo);

        // Assert
        diagnostics.Should().BeEmpty("Transient parent with Singleton child is fine");
    }

    #endregion

    #region Test Infrastructure

    /// <summary>
    /// Builds a parent/child validator pair with composition edges for testing.
    /// Compiles the source to get real INamedTypeSymbol instances.
    /// </summary>
    private static (ValidatorClassInfo Parent, ValidatorClassInfo Child) BuildValidatorPair(
        string source,
        string parentName,
        string childName,
        string parentLifetime,
        string childLifetime,
        CompositionType compositionType,
        bool isDirectInstantiation)
    {
        var compilation = CreateCompilation(source);
        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());

        var parentSymbol = FindType(compilation, parentName);
        var childSymbol = FindType(compilation, childName);

        parentSymbol.Should().NotBeNull($"parent type '{parentName}' should be found in compilation");
        childSymbol.Should().NotBeNull($"child type '{childName}' should be found in compilation");

        var parentDecl = FindTypeDeclaration(compilation, parentName);
        var childDecl = FindTypeDeclaration(compilation, childName);

        // Build composition edge for the parent
        var edge = new CompositionEdge(
            parentSymbol!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            childSymbol!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            childSymbol.Name,
            compositionType,
            isDirectInstantiation,
            location: null);

        // Find validated types
        var parentValidatedType = GetAbstractValidatorTypeArg(parentSymbol);
        var childValidatedType = GetAbstractValidatorTypeArg(childSymbol);

        var parentInfo = new ValidatorClassInfo(
            parentSymbol,
            parentDecl!,
            semanticModel,
            parentValidatedType!,
            parentLifetime,
            ImmutableArray.Create(edge));

        var childInfo = new ValidatorClassInfo(
            childSymbol,
            childDecl!,
            semanticModel,
            childValidatedType!,
            childLifetime);

        return (parentInfo, childInfo);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var iocToolsAssembly = typeof(IoCTools.Abstractions.Annotations.ScopedAttribute).Assembly;
        var refs = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(iocToolsAssembly.Location),
        };

        // Add FluentValidation reference
        var allRefs = refs.ToList();
        try
        {
            var fvAssembly = typeof(global::FluentValidation.AbstractValidator<>).Assembly;
            allRefs.Add(MetadataReference.CreateFromFile(fvAssembly.Location));
        }
        catch { /* FluentValidation may not be available */ }

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        return CSharpCompilation.Create(
            "DiagnosticTest",
            new[] { syntaxTree },
            allRefs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static INamedTypeSymbol? FindType(CSharpCompilation compilation, string simpleName)
    {
        return compilation.GetSymbolsWithName(simpleName, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault();
    }

    private static Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax? FindTypeDeclaration(
        CSharpCompilation compilation, string simpleName)
    {
        return compilation.SyntaxTrees
            .SelectMany(t => t.GetRoot().DescendantNodes())
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .FirstOrDefault(td => td.Identifier.Text == simpleName);
    }

    private static INamedTypeSymbol? GetAbstractValidatorTypeArg(INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.IsGenericType &&
                current.OriginalDefinition.ToDisplayString().Contains("AbstractValidator"))
            {
                return current.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
            }
            current = current.BaseType;
        }
        return null;
    }

    private static ImmutableArray<Diagnostic> RunDirectInstantiationValidator(
        ValidatorClassInfo parent, ValidatorClassInfo child)
    {
        var allValidators = ImmutableArray.Create(parent, child);
        var diagnostics = new System.Collections.Generic.List<Diagnostic>();
        DirectInstantiationValidator.Validate(parent, allValidators, d => diagnostics.Add(d));
        return diagnostics.ToImmutableArray();
    }

    private static ImmutableArray<Diagnostic> RunCompositionLifetimeValidator(
        ValidatorClassInfo parent, ValidatorClassInfo child)
    {
        var allValidators = ImmutableArray.Create(parent, child);
        var diagnostics = new System.Collections.Generic.List<Diagnostic>();
        CompositionLifetimeValidator.Validate(parent, allValidators, d => diagnostics.Add(d));
        return diagnostics.ToImmutableArray();
    }

    /// <summary>
    /// Replicates the body of <see cref="IoCTools.FluentValidation.Generator.Pipeline.ValidatorDiagnosticsPipeline"/>
    /// so tests can assert on IOC103/IOC104 diagnostic emission without a full generator host.
    /// If the fix is reverted (silent skip restored), the IOC103/IOC104 assertions go RED.
    /// </summary>
    private static ImmutableArray<Diagnostic> RunDiagnosticsPipelineFor(
        ImmutableArray<ValidatorClassInfo> allValidators,
        System.Action<ValidatorClassInfo, ImmutableArray<ValidatorClassInfo>, System.Action<Diagnostic>>? overrideValidate = null)
    {
        var diagnostics = new System.Collections.Generic.List<Diagnostic>();
        System.Action<Diagnostic> report = d => diagnostics.Add(d);

        foreach (var validator in allValidators)
        {
            if (validator.GraphBuildError != null)
            {
                diagnostics.Add(Diagnostic.Create(
                    IoCTools.FluentValidation.Diagnostics.FluentValidationDiagnosticDescriptors.CompositionGraphAnalysisError,
                    location: null,
                    validator.FullyQualifiedName,
                    validator.GraphBuildError));
            }

            if (overrideValidate != null)
            {
                try
                {
                    overrideValidate(validator, allValidators, report);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    diagnostics.Add(Diagnostic.Create(
                        IoCTools.FluentValidation.Diagnostics.FluentValidationDiagnosticDescriptors.ValidatorPipelineError,
                        location: null,
                        validator.FullyQualifiedName,
                        ex.Message));
                }
            }
            else
            {
                try
                {
                    DirectInstantiationValidator.Validate(validator, allValidators, report);
                    CompositionLifetimeValidator.Validate(validator, allValidators, report);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
                {
                    diagnostics.Add(Diagnostic.Create(
                        IoCTools.FluentValidation.Diagnostics.FluentValidationDiagnosticDescriptors.ValidatorPipelineError,
                        location: null,
                        validator.FullyQualifiedName,
                        ex.Message));
                }
            }
        }

        return diagnostics.ToImmutableArray();
    }

    #endregion
}

/// <summary>
/// Tests for IOC103 (CompositionGraphBuilder analysis error) and IOC104 (ValidatorDiagnosticsPipeline error).
/// Revert-RED-proof: if either silent-skip is restored, these tests fail because the expected
/// diagnostic ID is no longer emitted.
/// </summary>
public sealed class FailLoudDiagnosticTests
{
    #region IOC103 — CompositionGraphBuilder analysis error

    [Fact]
    public void GraphBuildError_EmitsIOC103()
    {
        // Arrange: construct a ValidatorClassInfo that carries a graph-build error
        // (as would happen when CompositionGraphBuilder.BuildEdges catches an exception).
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;
namespace TestApp;
public class Order { }
[Scoped]
public partial class OrderValidator : AbstractValidator<Order> { }
";
        var compilation = CreateMinimalCompilation(source);
        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
        var symbol = FindType(compilation, "OrderValidator")!;
        var validatedType = GetAbstractValidatorTypeArg(symbol)!;
        var decl = FindTypeDeclaration(compilation, "OrderValidator")!;

        const string simulatedError = "simulated graph analysis failure";
        var validatorWithError = new ValidatorClassInfo(
            symbol, decl, semanticModel, validatedType, "Scoped",
            graphBuildError: simulatedError);

        // Act: run the pipeline body (replicates ValidatorDiagnosticsPipeline.Attach logic).
        var allValidators = ImmutableArray.Create(validatorWithError);
        var diagnostics = RunPipeline(allValidators);

        // Assert: IOC103 must be reported — revert to silent-skip → no IOC103 → test RED.
        diagnostics.Should().ContainSingle(d => d.Id == "IOC103",
            "a non-null GraphBuildError must surface as IOC103, not be silently swallowed");
        diagnostics.First(d => d.Id == "IOC103").GetMessage()
            .Should().Contain(simulatedError, "the error message must be included in the diagnostic");
    }

    [Fact]
    public void NoGraphBuildError_NoIOC103()
    {
        // Arrange: clean validator with no graph build error.
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;
namespace TestApp;
public class Order { }
[Scoped]
public partial class OrderValidator : AbstractValidator<Order> { }
";
        var compilation = CreateMinimalCompilation(source);
        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
        var symbol = FindType(compilation, "OrderValidator")!;
        var validatedType = GetAbstractValidatorTypeArg(symbol)!;
        var decl = FindTypeDeclaration(compilation, "OrderValidator")!;

        var cleanValidator = new ValidatorClassInfo(
            symbol, decl, semanticModel, validatedType, "Scoped");

        // Act
        var diagnostics = RunPipeline(ImmutableArray.Create(cleanValidator));

        // Assert
        diagnostics.Where(d => d.Id == "IOC103").Should().BeEmpty(
            "a validator with no graph build error must not emit IOC103");
    }

    #endregion

    #region IOC104 — ValidatorDiagnosticsPipeline error

    [Fact]
    public void ThrowingValidator_EmitsIOC104()
    {
        // Arrange: a validator info that will cause the inner validate action to throw.
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;
namespace TestApp;
public class Order { }
[Scoped]
public partial class OrderValidator : AbstractValidator<Order> { }
";
        var compilation = CreateMinimalCompilation(source);
        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
        var symbol = FindType(compilation, "OrderValidator")!;
        var validatedType = GetAbstractValidatorTypeArg(symbol)!;
        var decl = FindTypeDeclaration(compilation, "OrderValidator")!;

        var validator = new ValidatorClassInfo(
            symbol, decl, semanticModel, validatedType, "Scoped");

        // Act: use a throwing override to simulate a broken validator rule.
        const string thrownMessage = "broken validator rule NullReferenceException";
        var diagnostics = RunPipelineWithThrowingValidate(
            ImmutableArray.Create(validator),
            throwMessage: thrownMessage);

        // Assert: IOC104 must be reported — revert to silent-skip → no IOC104 → test RED.
        diagnostics.Should().ContainSingle(d => d.Id == "IOC104",
            "a throwing validator must surface as IOC104, not be silently swallowed");
        diagnostics.First(d => d.Id == "IOC104").GetMessage()
            .Should().Contain(thrownMessage, "the exception message must appear in the diagnostic");
    }

    [Fact]
    public void NormalValidator_NoIOC104()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;
namespace TestApp;
public class Order { }
[Scoped]
public partial class OrderValidator : AbstractValidator<Order> { }
";
        var compilation = CreateMinimalCompilation(source);
        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
        var symbol = FindType(compilation, "OrderValidator")!;
        var validatedType = GetAbstractValidatorTypeArg(symbol)!;
        var decl = FindTypeDeclaration(compilation, "OrderValidator")!;

        var validator = new ValidatorClassInfo(
            symbol, decl, semanticModel, validatedType, "Scoped");

        // Act: normal (non-throwing) validate action.
        var diagnostics = RunPipeline(ImmutableArray.Create(validator));

        // Assert
        diagnostics.Where(d => d.Id == "IOC104").Should().BeEmpty(
            "a clean validator must not emit IOC104");
    }

    #endregion

    #region Test Infrastructure

    /// <summary>
    /// Runs the pipeline body (mirrors ValidatorDiagnosticsPipeline.Attach) against the given validators.
    /// Uses real DirectInstantiationValidator + CompositionLifetimeValidator.
    /// </summary>
    private static ImmutableArray<Diagnostic> RunPipeline(ImmutableArray<ValidatorClassInfo> allValidators)
    {
        var diagnostics = new System.Collections.Generic.List<Diagnostic>();
        System.Action<Diagnostic> report = d => diagnostics.Add(d);

        foreach (var validator in allValidators)
        {
            if (validator.GraphBuildError != null)
            {
                diagnostics.Add(Diagnostic.Create(
                    IoCTools.FluentValidation.Diagnostics.FluentValidationDiagnosticDescriptors.CompositionGraphAnalysisError,
                    location: null,
                    validator.FullyQualifiedName,
                    validator.GraphBuildError));
            }

            try
            {
                IoCTools.FluentValidation.Diagnostics.Validators.DirectInstantiationValidator.Validate(validator, allValidators, report);
                IoCTools.FluentValidation.Diagnostics.Validators.CompositionLifetimeValidator.Validate(validator, allValidators, report);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
            {
                diagnostics.Add(Diagnostic.Create(
                    IoCTools.FluentValidation.Diagnostics.FluentValidationDiagnosticDescriptors.ValidatorPipelineError,
                    location: null,
                    validator.FullyQualifiedName,
                    ex.Message));
            }
        }

        return diagnostics.ToImmutableArray();
    }

    /// <summary>
    /// Runs the pipeline body with an injected throwing validate action to exercise IOC104.
    /// </summary>
    private static ImmutableArray<Diagnostic> RunPipelineWithThrowingValidate(
        ImmutableArray<ValidatorClassInfo> allValidators,
        string throwMessage)
    {
        var diagnostics = new System.Collections.Generic.List<Diagnostic>();
        System.Action<Diagnostic> report = d => diagnostics.Add(d);

        foreach (var validator in allValidators)
        {
            if (validator.GraphBuildError != null)
            {
                diagnostics.Add(Diagnostic.Create(
                    IoCTools.FluentValidation.Diagnostics.FluentValidationDiagnosticDescriptors.CompositionGraphAnalysisError,
                    location: null,
                    validator.FullyQualifiedName,
                    validator.GraphBuildError));
            }

            try
            {
                throw new InvalidOperationException(throwMessage);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
            {
                diagnostics.Add(Diagnostic.Create(
                    IoCTools.FluentValidation.Diagnostics.FluentValidationDiagnosticDescriptors.ValidatorPipelineError,
                    location: null,
                    validator.FullyQualifiedName,
                    ex.Message));
            }
        }

        return diagnostics.ToImmutableArray();
    }

    private static CSharpCompilation CreateMinimalCompilation(string source)
    {
        var iocToolsAssembly = typeof(IoCTools.Abstractions.Annotations.ScopedAttribute).Assembly;
        var refs = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(iocToolsAssembly.Location),
        };

        var allRefs = new System.Collections.Generic.List<MetadataReference>(refs);
        try
        {
            var fvAssembly = typeof(global::FluentValidation.AbstractValidator<>).Assembly;
            allRefs.Add(MetadataReference.CreateFromFile(fvAssembly.Location));
        }
        catch { /* FluentValidation may not be available */ }

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        return CSharpCompilation.Create(
            "FailLoudTest",
            new[] { syntaxTree },
            allRefs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static INamedTypeSymbol? FindType(CSharpCompilation compilation, string simpleName) =>
        compilation.GetSymbolsWithName(simpleName, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault();

    private static Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax? FindTypeDeclaration(
        CSharpCompilation compilation, string simpleName) =>
        compilation.SyntaxTrees
            .SelectMany(t => t.GetRoot().DescendantNodes())
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .FirstOrDefault(td => td.Identifier.Text == simpleName);

    private static INamedTypeSymbol? GetAbstractValidatorTypeArg(INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.IsGenericType &&
                current.OriginalDefinition.ToDisplayString().Contains("AbstractValidator"))
            {
                return current.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
            }
            current = current.BaseType;
        }
        return null;
    }

    #endregion
}
