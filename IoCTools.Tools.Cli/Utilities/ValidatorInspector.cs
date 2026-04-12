namespace IoCTools.Tools.Cli;

using System.Collections.Immutable;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Inspects a Roslyn compilation for FluentValidation validator classes.
/// Detection is name-based (matching AbstractValidator&lt;T&gt;) — no dependency on the FluentValidation generator package.
/// </summary>
internal static class ValidatorInspector
{
    /// <summary>
    /// Discovers all validator classes inheriting AbstractValidator&lt;T&gt; in the compilation.
    /// </summary>
    public static IReadOnlyList<ValidatorInfo> DiscoverValidators(CSharpCompilation compilation)
    {
        var validators = new List<ValidatorInfo>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(classDecl);
                if (symbol == null) continue;

                var modelType = GetValidatedModelType(symbol);
                if (modelType == null) continue;

                var lifetime = GetLifetimeFromAttributes(symbol);
                var edges = BuildCompositionEdges(classDecl, semanticModel);

                validators.Add(new ValidatorInfo(
                    symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", ""),
                    modelType,
                    lifetime,
                    edges));
            }
        }

        return validators;
    }

    /// <summary>
    /// Checks if a type inherits from AbstractValidator&lt;T&gt; (by name) and returns the model type name.
    /// </summary>
    private static string? GetValidatedModelType(INamedTypeSymbol symbol)
    {
        var current = symbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (current.IsGenericType &&
                current.Name == "AbstractValidator" &&
                current.TypeArguments.Length == 1)
            {
                return current.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            }

            current = current.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Detects the IoCTools lifetime attribute on a validator class.
    /// </summary>
    private static string? GetLifetimeFromAttributes(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var name = attribute.AttributeClass?.Name;
            if (name == null) continue;

            if (name == "ScopedAttribute" || name == "Scoped") return "Scoped";
            if (name == "SingletonAttribute" || name == "Singleton") return "Singleton";
            if (name == "TransientAttribute" || name == "Transient") return "Transient";
        }

        // Check base types for inherited lifetime
        var current = symbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            var baseLifetime = GetLifetimeFromAttributes(current);
            if (baseLifetime != null) return baseLifetime;
            current = current.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Parses the validator class body for SetValidator/Include/SetInheritanceValidator composition calls.
    /// </summary>
    private static IReadOnlyList<CompositionEdgeInfo> BuildCompositionEdges(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel)
    {
        var edges = new List<CompositionEdgeInfo>();

        var invocations = classDecl.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) continue;

            var methodName = memberAccess.Name switch
            {
                GenericNameSyntax generic => generic.Identifier.Text,
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                _ => null
            };

            if (methodName == null) continue;

            if (methodName == "SetValidator")
            {
                var childType = ResolveChildValidatorType(invocation, semanticModel);
                if (childType != null)
                {
                    var isDirect = IsDirectInstantiation(invocation.ArgumentList);
                    edges.Add(new CompositionEdgeInfo(childType, "SetValidator", isDirect));
                }
            }
            else if (methodName == "Include")
            {
                var childType = ResolveChildValidatorType(invocation, semanticModel);
                if (childType != null)
                {
                    var isDirect = IsDirectInstantiation(invocation.ArgumentList);
                    edges.Add(new CompositionEdgeInfo(childType, "Include", isDirect));
                }
            }
            else if (methodName == "SetInheritanceValidator")
            {
                // SetInheritanceValidator uses lambda with .Add<T>() calls
                var lambdaArg = invocation.ArgumentList.Arguments
                    .Select(a => a.Expression)
                    .OfType<LambdaExpressionSyntax>()
                    .FirstOrDefault();

                if (lambdaArg != null)
                {
                    var addCalls = lambdaArg.DescendantNodes()
                        .OfType<InvocationExpressionSyntax>()
                        .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                                      ma.Name is GenericNameSyntax gn &&
                                      gn.Identifier.Text == "Add");

                    foreach (var addCall in addCalls)
                    {
                        if (addCall.Expression is MemberAccessExpressionSyntax addMa &&
                            addMa.Name is GenericNameSyntax addGeneric &&
                            addGeneric.TypeArgumentList.Arguments.Count > 0)
                        {
                            var typeArg = addGeneric.TypeArgumentList.Arguments[0];
                            var typeInfo = semanticModel.GetTypeInfo(typeArg);
                            var typeName = typeInfo.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                                           ?? typeArg.ToString();
                            edges.Add(new CompositionEdgeInfo(typeName, "SetInheritanceValidator", false));
                        }
                    }
                }
            }
        }

        return edges;
    }

    private static string? ResolveChildValidatorType(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        if (invocation.ArgumentList.Arguments.Count == 0) return null;

        var firstArg = invocation.ArgumentList.Arguments[0].Expression;

        // new ChildValidator() - direct instantiation
        if (firstArg is ObjectCreationExpressionSyntax creation)
        {
            var typeInfo = semanticModel.GetTypeInfo(creation);
            return typeInfo.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                   ?? creation.Type.ToString();
        }

        // Field, parameter, property, or local variable access
        var symbolInfo = semanticModel.GetSymbolInfo(firstArg);
        var symbol = symbolInfo.Symbol;
        if (symbol != null)
        {
            var type = symbol switch
            {
                IFieldSymbol f => f.Type,
                IParameterSymbol p => p.Type,
                IPropertySymbol prop => prop.Type,
                ILocalSymbol l => l.Type,
                _ => null
            };

            if (type != null)
                return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        // Lambda expression - try to infer from return type
        if (firstArg is LambdaExpressionSyntax lambda)
        {
            var lambdaTypeInfo = semanticModel.GetTypeInfo(lambda);
            if (lambdaTypeInfo.ConvertedType is INamedTypeSymbol delegateType &&
                delegateType.DelegateInvokeMethod?.ReturnType is INamedTypeSymbol returnType)
            {
                return returnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            }
        }

        return null;
    }

    private static bool IsDirectInstantiation(ArgumentListSyntax argumentList)
    {
        if (argumentList.Arguments.Count == 0) return false;
        return argumentList.Arguments[0].Expression is ObjectCreationExpressionSyntax;
    }

    /// <summary>
    /// Builds a composition tree for display.
    /// </summary>
    public static IReadOnlyList<ValidatorTreeNode> BuildCompositionTree(IReadOnlyList<ValidatorInfo> validators)
    {
        var byName = validators.ToDictionary(v => v.FullName, StringComparer.Ordinal);
        var shortNameMap = validators.ToDictionary(
            v => v.FullName.Split('.').Last(),
            v => v.FullName,
            StringComparer.Ordinal);

        var roots = new List<ValidatorTreeNode>();
        var childSet = new HashSet<string>(StringComparer.Ordinal);

        // Identify all validators that appear as children
        foreach (var v in validators)
        {
            foreach (var edge in v.CompositionEdges)
            {
                var resolvedName = ResolveValidatorName(edge.ChildValidatorType, byName, shortNameMap);
                if (resolvedName != null)
                    childSet.Add(resolvedName);
            }
        }

        // Build tree starting from roots (validators not referenced as children)
        foreach (var v in validators)
        {
            if (!childSet.Contains(v.FullName))
            {
                roots.Add(BuildNode(v, byName, shortNameMap, new HashSet<string>(StringComparer.Ordinal)));
            }
        }

        // Include isolated validators (no edges and not referenced)
        // They're already in roots since they're not in childSet

        return roots;
    }

    private static ValidatorTreeNode BuildNode(
        ValidatorInfo validator,
        Dictionary<string, ValidatorInfo> byName,
        Dictionary<string, string> shortNameMap,
        HashSet<string> visited)
    {
        if (!visited.Add(validator.FullName))
            return new ValidatorTreeNode(validator, Array.Empty<ValidatorChildNode>());

        var children = new List<ValidatorChildNode>();
        foreach (var edge in validator.CompositionEdges)
        {
            var resolvedName = ResolveValidatorName(edge.ChildValidatorType, byName, shortNameMap);
            ValidatorTreeNode? childNode = null;
            if (resolvedName != null && byName.TryGetValue(resolvedName, out var childValidator))
            {
                childNode = BuildNode(childValidator, byName, shortNameMap, visited);
            }

            children.Add(new ValidatorChildNode(edge, childNode));
        }

        visited.Remove(validator.FullName);
        return new ValidatorTreeNode(validator, children);
    }

    private static string? ResolveValidatorName(
        string childType,
        Dictionary<string, ValidatorInfo> byName,
        Dictionary<string, string> shortNameMap)
    {
        if (byName.ContainsKey(childType)) return childType;
        if (shortNameMap.TryGetValue(childType, out var fullName)) return fullName;
        return null;
    }

    /// <summary>
    /// Traces why a validator has its lifetime through composition chains.
    /// </summary>
    public static string TraceLifetime(string validatorName, IReadOnlyList<ValidatorInfo> validators)
    {
        var explanation = TraceLifetimeExplanation(validatorName, validators);
        if (explanation.reason == "not-found")
            return $"Validator '{validatorName}' not found.";

        if (explanation.reason == "no-lifetime")
            return $"{explanation.validator} has no lifetime attribute.";

        if (explanation.steps.Count == 0)
            return $"{explanation.validator} is {explanation.lifetime} (set directly via [{explanation.lifetime}] attribute).";

        return $"{explanation.validator} is {explanation.lifetime} because:\n" +
               string.Join("\n", explanation.steps.Select(FormatStep));
    }

    public static ValidatorLifetimeExplanation TraceLifetimeExplanation(string validatorName,
        IReadOnlyList<ValidatorInfo> validators)
    {
        var byName = validators.ToDictionary(v => v.FullName, StringComparer.Ordinal);
        var shortNameMap = validators.ToDictionary(
            v => v.FullName.Split('.').Last(),
            v => v.FullName,
            StringComparer.Ordinal);

        // Resolve the target validator
        ValidatorInfo? target = null;
        if (byName.TryGetValue(validatorName, out target)) { }
        else if (shortNameMap.TryGetValue(validatorName, out var fullName) && byName.TryGetValue(fullName, out target)) { }
        else
        {
            // Try partial match
            var match = validators.FirstOrDefault(v =>
                v.FullName.EndsWith("." + validatorName, StringComparison.Ordinal) ||
                v.FullName.Equals(validatorName, StringComparison.OrdinalIgnoreCase));
            target = match;
        }

        if (target == null)
            return new ValidatorLifetimeExplanation(validatorName, null, "not-found", Array.Empty<ValidatorLifetimeStep>());

        if (target.Lifetime == null)
            return new ValidatorLifetimeExplanation(target.FullName, null, "no-lifetime",
                Array.Empty<ValidatorLifetimeStep>());

        // Check if any composed child has a dependency that forces the lifetime
        var reasons = new List<ValidatorLifetimeStep>();
        TraceLifetimeReasons(target, byName, shortNameMap, reasons, new HashSet<string>(StringComparer.Ordinal));

        return new ValidatorLifetimeExplanation(
            target.FullName,
            target.Lifetime,
            reasons.Count == 0 ? "attribute" : "composition",
            reasons);
    }

    private static void TraceLifetimeReasons(
        ValidatorInfo validator,
        Dictionary<string, ValidatorInfo> byName,
        Dictionary<string, string> shortNameMap,
        List<ValidatorLifetimeStep> reasons,
        HashSet<string> visited)
    {
        if (!visited.Add(validator.FullName)) return;

        foreach (var edge in validator.CompositionEdges)
        {
            var resolvedName = ResolveValidatorName(edge.ChildValidatorType, byName, shortNameMap);
            if (resolvedName != null && byName.TryGetValue(resolvedName, out var child))
            {
                if (child.Lifetime != null && child.Lifetime != validator.Lifetime)
                {
                    reasons.Add(new ValidatorLifetimeStep(
                        "composes",
                        child.FullName,
                        edge.CompositionMethod,
                        edge.IsDirect,
                        child.Lifetime));
                }

                if (child.Lifetime == "Scoped" && validator.Lifetime == "Scoped")
                {
                    reasons.Add(new ValidatorLifetimeStep(
                        "matching-lifetime",
                        child.FullName,
                        edge.CompositionMethod,
                        edge.IsDirect,
                        child.Lifetime));
                }

                TraceLifetimeReasons(child, byName, shortNameMap, reasons, visited);
            }
        }

        visited.Remove(validator.FullName);
    }

    private static string FormatStep(ValidatorLifetimeStep step)
    {
        var instantiation = step.isDirect ? "direct instantiation" : "injected";
        var lifetime = step.lifetime == null ? string.Empty : $" [{step.lifetime}]";
        return $"  - {step.kind} {step.target}{lifetime} via {step.method} ({instantiation})";
    }
}

internal sealed class ValidatorInfo
{
    public ValidatorInfo(string fullName, string modelType, string? lifetime, IReadOnlyList<CompositionEdgeInfo> compositionEdges)
    {
        FullName = fullName;
        ModelType = modelType;
        Lifetime = lifetime;
        CompositionEdges = compositionEdges;
    }

    public string FullName { get; }
    public string ModelType { get; }
    public string? Lifetime { get; }
    public IReadOnlyList<CompositionEdgeInfo> CompositionEdges { get; }
    public bool HasCompositionEdges => CompositionEdges.Count > 0;
}

internal sealed class CompositionEdgeInfo
{
    public CompositionEdgeInfo(string childValidatorType, string compositionMethod, bool isDirect)
    {
        ChildValidatorType = childValidatorType;
        CompositionMethod = compositionMethod;
        IsDirect = isDirect;
    }

    public string ChildValidatorType { get; }
    public string CompositionMethod { get; }
    public bool IsDirect { get; }
}

internal sealed class ValidatorTreeNode
{
    public ValidatorTreeNode(ValidatorInfo validator, IReadOnlyList<ValidatorChildNode> children)
    {
        Validator = validator;
        Children = children;
    }

    public ValidatorInfo Validator { get; }
    public IReadOnlyList<ValidatorChildNode> Children { get; }
}

internal sealed class ValidatorChildNode
{
    public ValidatorChildNode(CompositionEdgeInfo edge, ValidatorTreeNode? resolved)
    {
        Edge = edge;
        Resolved = resolved;
    }

    public CompositionEdgeInfo Edge { get; }
    public ValidatorTreeNode? Resolved { get; }
}

internal sealed record ValidatorLifetimeExplanation(
    string validator,
    string? lifetime,
    string reason,
    IReadOnlyList<ValidatorLifetimeStep> steps);

internal sealed record ValidatorLifetimeStep(
    string kind,
    string target,
    string method,
    bool isDirect,
    string? lifetime);
