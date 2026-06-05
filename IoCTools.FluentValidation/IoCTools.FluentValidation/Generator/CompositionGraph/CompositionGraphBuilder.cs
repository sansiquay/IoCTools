namespace IoCTools.FluentValidation.Generator.CompositionGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Walks a validator class body to discover SetValidator, Include, and SetInheritanceValidator
/// invocations and builds composition graph edges.
/// </summary>
internal static class CompositionGraphBuilder
{
    /// <summary>
    /// Builds composition edges by walking the validator's syntax tree.
    /// Returns the edge list and, if an unexpected exception was caught, its message
    /// so the caller can emit an IOC111 diagnostic via a <c>ReportDiagnostic</c> sink.
    /// </summary>
    /// <param name="validatorDecl">The syntax node for the validator class.</param>
    /// <param name="model">The semantic model for type resolution.</param>
    /// <param name="parentValidatorFqn">Fully-qualified name of the parent validator.</param>
    /// <param name="buildError">Non-null when an unexpected exception was caught; carries the message for IOC111 emission.</param>
    /// <param name="cancellationToken">Token forwarded to semantic model queries; propagates analyzer cancellation.</param>
    internal static List<CompositionEdge> BuildEdges(
        TypeDeclarationSyntax validatorDecl,
        SemanticModel model,
        string parentValidatorFqn,
        out string? buildError,
        CancellationToken cancellationToken = default)
    {
        var edges = new List<CompositionEdge>();
        buildError = null;

        try
        {
            foreach (var invocation in validatorDecl.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var methodName = GetMethodName(invocation);
                if (methodName == null)
                    continue;

                switch (methodName)
                {
                    case "SetValidator":
                        HandleSetValidatorOrInclude(invocation, model, parentValidatorFqn, CompositionType.SetValidator, edges, cancellationToken);
                        break;
                    case "Include":
                        HandleSetValidatorOrInclude(invocation, model, parentValidatorFqn, CompositionType.Include, edges, cancellationToken);
                        break;
                    case "SetInheritanceValidator":
                        HandleSetInheritanceValidator(invocation, model, parentValidatorFqn, edges, cancellationToken);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Analyzer cancellation must propagate — do not convert to IOC111.
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            // Capture the error message so the caller can emit IOC111; do not rethrow.
            buildError = ex.Message;
        }

        return edges;
    }

    /// <summary>
    /// Extracts the method name from an invocation expression.
    /// Handles both member access (e.g., <c>RuleFor(...).SetValidator(...)</c>) and simple identifier forms.
    /// </summary>
    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax memberAccess:
                return memberAccess.Name.Identifier.Text;
            case IdentifierNameSyntax identifier:
                return identifier.Identifier.Text;
            default:
                return null;
        }
    }

    /// <summary>
    /// Handles SetValidator and Include invocations, which share the same argument pattern:
    /// a single argument that is either a <c>new ChildValidator()</c> or an injected reference.
    /// </summary>
    private static void HandleSetValidatorOrInclude(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        string parentValidatorFqn,
        CompositionType compositionType,
        List<CompositionEdge> edges,
        CancellationToken cancellationToken)
    {
        var args = invocation.ArgumentList?.Arguments;
        if (args == null || args.Value.Count == 0)
            return;

        var firstArg = args.Value[0].Expression;
        var resolved = ResolveChildValidatorType(firstArg, model, cancellationToken);
        if (resolved == null)
            return;

        edges.Add(new CompositionEdge(
            parentValidatorFqn,
            resolved.Value.fullyQualifiedName,
            resolved.Value.shortName,
            compositionType,
            resolved.Value.isDirectInstantiation,
            invocation.GetLocation()));
    }

    /// <summary>
    /// Handles SetInheritanceValidator invocations. The argument is typically a lambda
    /// containing <c>.Add&lt;T&gt;(new DogValidator())</c> calls.
    /// </summary>
    private static void HandleSetInheritanceValidator(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        string parentValidatorFqn,
        List<CompositionEdge> edges,
        CancellationToken cancellationToken)
    {
        var args = invocation.ArgumentList?.Arguments;
        if (args == null || args.Value.Count == 0)
            return;

        var lambdaArg = args.Value[0].Expression;

        // Walk the lambda body for .Add<T>(...) invocations
        foreach (var innerInvocation in lambdaArg.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var innerMethodName = GetMethodName(innerInvocation);
            if (innerMethodName != "Add")
                continue;

            // The Add<T>() method takes a validator argument
            var innerArgs = innerInvocation.ArgumentList?.Arguments;
            if (innerArgs == null || innerArgs.Value.Count == 0)
                continue;

            var validatorArg = innerArgs.Value[0].Expression;
            var resolved = ResolveChildValidatorType(validatorArg, model, cancellationToken);
            if (resolved == null)
                continue;

            edges.Add(new CompositionEdge(
                parentValidatorFqn,
                resolved.Value.fullyQualifiedName,
                resolved.Value.shortName,
                CompositionType.SetInheritanceValidator,
                resolved.Value.isDirectInstantiation,
                innerInvocation.GetLocation()));
        }
    }

    /// <summary>
    /// Resolves the child validator type from an argument expression.
    /// Handles direct instantiation (<c>new X()</c>) and injected references (fields, parameters, properties).
    /// OperationCanceledException propagates so analyzer cancellation is not swallowed.
    /// </summary>
    /// <returns>A tuple with the fully-qualified name, short name, and whether it is direct instantiation;
    /// or null if the type cannot be resolved.</returns>
    private static (string fullyQualifiedName, string shortName, bool isDirectInstantiation)? ResolveChildValidatorType(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        try
        {
            // Direct instantiation: new SomeValidator() or new SomeValidator(args)
            if (expression is ObjectCreationExpressionSyntax creation)
            {
                var typeInfo = model.GetTypeInfo(creation, cancellationToken);
                var typeSymbol = typeInfo.Type as INamedTypeSymbol;
                if (typeSymbol == null)
                    return null;

                return (
                    typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    typeSymbol.Name,
                    true);
            }

            // Injected reference: field, parameter, property, or local variable
            var symbolInfo = model.GetSymbolInfo(expression, cancellationToken);
            var symbol = symbolInfo.Symbol;
            if (symbol == null)
                return null;

            ITypeSymbol? resolvedType = null;
            switch (symbol)
            {
                case IFieldSymbol field:
                    resolvedType = field.Type;
                    break;
                case IParameterSymbol parameter:
                    resolvedType = parameter.Type;
                    break;
                case IPropertySymbol property:
                    resolvedType = property.Type;
                    break;
                case ILocalSymbol local:
                    resolvedType = local.Type;
                    break;
            }

            if (resolvedType is INamedTypeSymbol namedType)
            {
                return (
                    namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    namedType.Name,
                    false);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            // Analyzer cancellation must propagate — do not swallow into a null return.
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            // Guard — skip unresolvable expressions
            return null;
        }
    }
}
