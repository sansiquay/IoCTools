namespace IoCTools.FluentValidation.Generator.CompositionGraph;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Walks a validator class body to discover SetValidator, Include, and SetInheritanceValidator
/// invocations and builds composition graph edges.
/// </summary>
internal static class CompositionGraphBuilder
{
    /// <summary>
    /// Builds composition edges by walking the validator's syntax tree for known
    /// FluentValidation composition invocations.
    /// </summary>
    /// <param name="validatorDecl">The syntax node for the validator class.</param>
    /// <param name="model">The semantic model for type resolution.</param>
    /// <param name="parentValidatorFqn">Fully-qualified name of the parent validator.</param>
    /// <returns>A list of composition edges found in the validator body.</returns>
    internal static List<CompositionEdge> BuildEdges(
        TypeDeclarationSyntax validatorDecl,
        SemanticModel model,
        string parentValidatorFqn)
    {
        var edges = new List<CompositionEdge>();

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
                        HandleSetValidatorOrInclude(invocation, model, parentValidatorFqn, CompositionType.SetValidator, edges);
                        break;
                    case "Include":
                        HandleSetValidatorOrInclude(invocation, model, parentValidatorFqn, CompositionType.Include, edges);
                        break;
                    case "SetInheritanceValidator":
                        HandleSetInheritanceValidator(invocation, model, parentValidatorFqn, edges);
                        break;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            // Generator never throws — skip edges if analysis fails
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
        List<CompositionEdge> edges)
    {
        var args = invocation.ArgumentList?.Arguments;
        if (args == null || args.Value.Count == 0)
            return;

        var firstArg = args.Value[0].Expression;
        var resolved = ResolveChildValidatorType(firstArg, model);
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
        List<CompositionEdge> edges)
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
            var resolved = ResolveChildValidatorType(validatorArg, model);
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
    /// </summary>
    /// <returns>A tuple with the fully-qualified name, short name, and whether it is direct instantiation;
    /// or null if the type cannot be resolved.</returns>
    private static (string fullyQualifiedName, string shortName, bool isDirectInstantiation)? ResolveChildValidatorType(
        ExpressionSyntax expression,
        SemanticModel model)
    {
        try
        {
            // Direct instantiation: new SomeValidator() or new SomeValidator(args)
            if (expression is ObjectCreationExpressionSyntax creation)
            {
                var typeInfo = model.GetTypeInfo(creation);
                var typeSymbol = typeInfo.Type as INamedTypeSymbol;
                if (typeSymbol == null)
                    return null;

                return (
                    typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    typeSymbol.Name,
                    true);
            }

            // Injected reference: field, parameter, property, or local variable
            var symbolInfo = model.GetSymbolInfo(expression);
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
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not StackOverflowException)
        {
            // Guard — skip unresolvable expressions
            return null;
        }
    }
}
