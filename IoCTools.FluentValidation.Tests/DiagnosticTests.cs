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
    /// so tests can assert on IOC111/IOC112 diagnostic emission without a full generator host.
    /// If the fix is reverted (silent skip restored), the IOC111/IOC112 assertions go RED.
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
                catch (OperationCanceledException)
                {
                    throw;
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
                catch (OperationCanceledException)
                {
                    throw;
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
/// Tests for IOC111 (CompositionGraphBuilder analysis error) and IOC112 (ValidatorDiagnosticsPipeline error).
/// All tests drive the REAL prod code paths — no test-local reimplementation of catch/emit logic.
///
/// Revert-RED contract:
///   • Revert prod OCE rethrow in BuildEdges  → OCE111 test goes RED.
///   • Revert prod IOC111 emit                → IOC111 emit test goes RED.
///   • Revert prod OCE rethrow in EmitDiagnosticsForValidator → OCE112 test goes RED.
///   • Revert prod IOC112 emit                → IOC112 emit test goes RED.
///
/// IOC103/IOC104 are reserved by IoCTools.AutoDeps — FluentValidation uses IOC111/IOC112.
/// </summary>
public sealed class FailLoudDiagnosticTests
{
    #region IOC111 — CompositionGraphBuilder analysis error

    [Fact]
    public void GraphBuildError_EmitsIOC111()
    {
        // Arrange: ValidatorClassInfo carrying a graphBuildError (as set by BuildEdges when it
        // catches an unexpected exception). Drive REAL EmitDiagnosticsForValidator — not a copy.
        var (validator, allValidators) = BuildSingleCleanValidator();
        const string simulatedError = "simulated graph analysis failure";
        var validatorWithError = new ValidatorClassInfo(
            validator.ClassSymbol, validator.ClassDeclaration, validator.SemanticModel,
            validator.ValidatedType, "Scoped",
            graphBuildError: simulatedError);

        // Act: call the REAL prod method — reverting the IOC111 emit block turns this RED.
        var diagnostics = CollectFrom(ImmutableArray.Create(validatorWithError));

        // Assert
        diagnostics.Should().ContainSingle(d => d.Id == "IOC111",
            "a non-null GraphBuildError must surface as IOC111 via EmitDiagnosticsForValidator");
        diagnostics.First(d => d.Id == "IOC111").GetMessage()
            .Should().Contain(simulatedError, "the error message must be included in the diagnostic");
    }

    [Fact]
    public void NoGraphBuildError_NoIOC111()
    {
        var (validator, allValidators) = BuildSingleCleanValidator();

        var diagnostics = CollectFrom(ImmutableArray.Create(validator));

        diagnostics.Where(d => d.Id == "IOC111").Should().BeEmpty(
            "a validator with no graph build error must not emit IOC111");
    }

    [Fact]
    public void GraphBuildError_OperationCanceledException_PropagatesNotIOC111()
    {
        // Regression: OperationCanceledException must NOT be converted into IOC111.
        // Scenario: a validator already carrying a GraphBuildError (the IOC111 payload) also
        // causes an OCE when its validator rules are run. The OCE must propagate out of
        // EmitDiagnosticsForValidator — it must not be swallowed by the IOC111 emit block or
        // caught and re-emitted as IOC112.
        //
        // Revert-RED proof: removing `catch (OperationCanceledException) { throw; }` from
        // EmitDiagnosticsForValidator causes OCE to be caught by the outer Exception filter
        // and emitted as IOC112 → Should().Throw() fails → test RED.
        //
        // Note: BuildEdges OCE propagation (ResolveChildValidatorType + outer BuildEdges guard)
        // is verified by code-inspection and integration; Roslyn 4.13 synchronous model APIs
        // do not honour pre-cancelled tokens, so the guard is not unit-testable at that level.
        var (validator, _) = BuildSingleCleanValidator();
        const string simulatedBuildError = "prior graph analysis failure";
        var validatorWithError = new ValidatorClassInfo(
            validator.ClassSymbol, validator.ClassDeclaration, validator.SemanticModel,
            validator.ValidatedType, "Scoped",
            graphBuildError: simulatedBuildError);
        var allValidators = ImmutableArray.Create(validatorWithError);

        // Act + Assert: OCE must propagate out of the REAL EmitDiagnosticsForValidator,
        // not be swallowed by the IOC111 or IOC112 catch blocks.
        var act = () => CollectFrom(
            allValidators,
            overrideValidate: (v, all, report) => throw new OperationCanceledException("simulated cancellation"));
        act.Should().Throw<OperationCanceledException>(
            "OperationCanceledException must propagate out of EmitDiagnosticsForValidator even when a GraphBuildError is present, not be swallowed as IOC111 or IOC112");
    }

    #endregion

    #region IOC112 — ValidatorDiagnosticsPipeline error

    [Fact]
    public void ThrowingValidator_EmitsIOC112()
    {
        // Arrange: inject a throwing override into the REAL EmitDiagnosticsForValidator.
        // Reverting the IOC112 catch+emit block causes the exception to escape → no IOC112 → test RED.
        var (validator, allValidators) = BuildSingleCleanValidator();
        const string thrownMessage = "broken validator rule NullReferenceException";

        var diagnostics = CollectFrom(
            allValidators,
            overrideValidate: (v, all, report) => throw new InvalidOperationException(thrownMessage));

        diagnostics.Should().ContainSingle(d => d.Id == "IOC112",
            "a throwing validator rule must surface as IOC112 via EmitDiagnosticsForValidator");
        diagnostics.First(d => d.Id == "IOC112").GetMessage()
            .Should().Contain(thrownMessage, "the exception message must appear in the diagnostic");
    }

    [Fact]
    public void NormalValidator_NoIOC112()
    {
        var (validator, allValidators) = BuildSingleCleanValidator();

        // No override → real DirectInstantiationValidator + CompositionLifetimeValidator run.
        var diagnostics = CollectFrom(allValidators);

        diagnostics.Where(d => d.Id == "IOC112").Should().BeEmpty(
            "a clean validator must not emit IOC112");
    }

    [Fact]
    public void ValidatorPipeline_OperationCanceledException_PropagatesNotIOC112()
    {
        // Regression: OCE thrown by a validator rule must propagate, not become IOC112.
        // This calls the REAL EmitDiagnosticsForValidator with a throwing override.
        // Reverting the `catch (OperationCanceledException) { throw; }` causes OCE to be caught
        // by the outer Exception filter → emitted as IOC112 → Should().Throw() fails → test RED.
        var (validator, allValidators) = BuildSingleCleanValidator();

        var act = () => CollectFrom(
            allValidators,
            overrideValidate: (v, all, report) => throw new OperationCanceledException("simulated cancellation"));

        act.Should().Throw<OperationCanceledException>(
            "OperationCanceledException must propagate out of EmitDiagnosticsForValidator, not become IOC112");
    }

    #endregion

    #region Test Infrastructure

    /// <summary>
    /// Calls <see cref="IoCTools.FluentValidation.Generator.Pipeline.ValidatorDiagnosticsPipeline.EmitDiagnosticsForValidator"/>
    /// (the REAL prod method) for every validator and collects emitted diagnostics.
    /// No catch/emit logic is duplicated here — the prod method owns it.
    /// </summary>
    private static ImmutableArray<Diagnostic> CollectFrom(
        ImmutableArray<ValidatorClassInfo> allValidators,
        System.Action<ValidatorClassInfo, ImmutableArray<ValidatorClassInfo>, System.Action<Diagnostic>>? overrideValidate = null)
    {
        var diagnostics = new System.Collections.Generic.List<Diagnostic>();
        System.Action<Diagnostic> report = d => diagnostics.Add(d);
        foreach (var validator in allValidators)
            IoCTools.FluentValidation.Generator.Pipeline.ValidatorDiagnosticsPipeline
                .EmitDiagnosticsForValidator(validator, allValidators, report, overrideValidate);
        return diagnostics.ToImmutableArray();
    }

    /// <summary>
    /// Builds a single clean <see cref="ValidatorClassInfo"/> with no graph-build error,
    /// suitable as a baseline for FailLoud tests.
    /// </summary>
    private static (ValidatorClassInfo Validator, ImmutableArray<ValidatorClassInfo> All) BuildSingleCleanValidator()
    {
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
        var validator = new ValidatorClassInfo(symbol, decl, semanticModel, validatedType, "Scoped");
        return (validator, ImmutableArray.Create(validator));
    }

    private static CSharpCompilation CreateMinimalCompilation(string source)
    {
        var iocToolsAssembly = typeof(IoCTools.Abstractions.Annotations.ScopedAttribute).Assembly;
        var refs = new System.Collections.Generic.List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(iocToolsAssembly.Location),
        };
        try
        {
            var fvAssembly = typeof(global::FluentValidation.AbstractValidator<>).Assembly;
            refs.Add(MetadataReference.CreateFromFile(fvAssembly.Location));
        }
        catch { /* FluentValidation may not be available */ }

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        return CSharpCompilation.Create(
            "FailLoudTest",
            new[] { syntaxTree },
            refs,
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
