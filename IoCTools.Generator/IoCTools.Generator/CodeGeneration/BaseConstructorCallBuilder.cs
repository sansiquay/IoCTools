namespace IoCTools.Generator.CodeGeneration;

using IoCTools.Generator.Analysis;
using IoCTools.Generator.Models;

using Microsoft.CodeAnalysis;

using Utilities;

/// <summary>
///     Responsible for determining if and how to call base class constructors
///     in generated constructor code. Extracted from ConstructorGenerator to
///     simplify complex boolean logic with clear decision tree methods.
/// </summary>
internal static class BaseConstructorCallBuilder
{
    /// <summary>
    ///     Checks if constructor generation should be skipped because the base class
    ///     requires parameters that cannot be provided through IoC.
    /// </summary>
    public static bool ShouldSkipConstructorGeneration(INamedTypeSymbol? baseClass)
    {
        if (baseClass == null)
            return false;

        var analysis = Analyze(baseClass);

        // Skip generation for non-IoC base classes that require constructor parameters
        // but don't have a parameterless constructor
        if (!analysis.HasExternalService &&
            !analysis.WillHaveConstructor &&
            !HasParameterlessConstructor(baseClass) &&
            HasConstructors(baseClass))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Analyzes a base class to determine constructor call requirements.
    /// </summary>
    public static BaseClassAnalysis Analyze(INamedTypeSymbol? baseClass)
    {
        if (baseClass == null)
            return BaseClassAnalysis.None;

        var canAcceptDIParameters = CanAcceptDIParameters(baseClass);
        var isPartial = IsPartialClass(baseClass);
        var willHaveConstructor = WillHaveGeneratedConstructor(baseClass);
        var hasExternalService = HasExternalServiceAttribute(baseClass);

        return new BaseClassAnalysis
        {
            CanAcceptDIParameters = canAcceptDIParameters,
            IsPartial = isPartial,
            WillHaveConstructor = willHaveConstructor,
            HasExternalService = hasExternalService
        };
    }

    /// <summary>
    ///     Builds the base constructor call string for the given base class and parameters.
    /// </summary>
    public static string Build(
        INamedTypeSymbol? baseClass,
        List<(string TypeString, string ParamName, (ITypeSymbol ServiceType, string FieldName, DependencySource Source) Dependency)> parametersWithNames,
        SemanticModel semanticModel,
        INamedTypeSymbol? currentClassSymbol)
    {
        if (baseClass == null)
            return string.Empty;

        var analysis = Analyze(baseClass);

        // ExternalService classes should not have IoC-generated constructors called
        if (analysis.HasExternalService)
            return BuildExternalServiceCall(baseClass);

        // Main IoC path: partial base class with generated constructor
        if (analysis.CanAcceptDIParameters && analysis.IsPartial && analysis.WillHaveConstructor)
            return BuildIoCBaseCall(baseClass, parametersWithNames, semanticModel);

        // Non-IoC base class fallback: handle constructors requiring parameters
        return BuildNonIoCBaseCall(baseClass, analysis, currentClassSymbol);
    }

    private static string BuildIoCBaseCall(
        INamedTypeSymbol baseClass,
        List<(string TypeString, string ParamName, (ITypeSymbol ServiceType, string FieldName, DependencySource Source) Dependency)> parametersWithNames,
        SemanticModel semanticModel)
    {
        var baseHierarchyDependencies =
            DependencyAnalyzer.GetConstructorDependencies(baseClass, semanticModel);

        if (!baseHierarchyDependencies.AllDependencies.Any())
            return string.Empty;

        var baseParamNames = MatchParametersToBaseDependencies(
            baseHierarchyDependencies.AllDependencies,
            parametersWithNames);

        return baseParamNames.Count > 0
            ? $" : base({string.Join(", ", baseParamNames)})"
            : string.Empty;
    }

    private static string BuildNonIoCBaseCall(
        INamedTypeSymbol baseClass,
        BaseClassAnalysis analysis,
        INamedTypeSymbol? currentClassSymbol)
    {
        // Non-IoC base classes: if they have a parameterless constructor, it will be called implicitly
        // If they require parameters, the user must provide their own constructor in the derived class
        // This is a documented limitation
        return string.Empty;
    }

    private static string BuildExternalServiceCall(INamedTypeSymbol baseClass)
    {
        // [ExternalService] classes should not have IoC-generated constructors
        // Leave baseCallStr empty to use default base() call
        return string.Empty;
    }

    private static List<string> MatchParametersToBaseDependencies(
        List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> baseDependencies,
        List<(string TypeString, string ParamName, (ITypeSymbol ServiceType, string FieldName, DependencySource Source) Dependency)> parametersWithNames)
    {
        var baseParamNames = new List<string>();

        // Process base dependencies in order - preserves inheritance-aware ordering
        foreach (var baseDep in baseDependencies)
        {
            // Skip configuration dependencies unless they're the _configuration parameter
            if (baseDep.Source == DependencySource.ConfigurationInjection &&
                baseDep.FieldName != "_configuration")
                continue;

            // Try exact match (type + field name)
            var matchingParam = parametersWithNames.FirstOrDefault(p =>
                SymbolEqualityComparer.Default.Equals(p.Dependency.ServiceType, baseDep.ServiceType) &&
                p.Dependency.FieldName == baseDep.FieldName);

            if (!string.IsNullOrEmpty(matchingParam.ParamName))
            {
                baseParamNames.Add(matchingParam.ParamName);
                continue;
            }

            // Fallback: type-only match
            var typeMatchingParam = parametersWithNames.FirstOrDefault(p =>
                SymbolEqualityComparer.Default.Equals(p.Dependency.ServiceType, baseDep.ServiceType));

            if (!string.IsNullOrEmpty(typeMatchingParam.ParamName))
                baseParamNames.Add(typeMatchingParam.ParamName);
        }

        return baseParamNames;
    }

    /// <summary>
    ///     Checks if the base class can accept DI parameters (not marked as ExternalService).
    /// </summary>
    private static bool CanAcceptDIParameters(INamedTypeSymbol baseClass)
    {
        return !baseClass.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString() ==
            "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");
    }

    /// <summary>
    ///     Checks if the base class is marked as partial.
    /// </summary>
    private static bool IsPartialClass(INamedTypeSymbol baseClass)
    {
        return baseClass.DeclaringSyntaxReferences.Any(syntaxRef =>
            syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDecl &&
            typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
    }

    /// <summary>
    ///     Checks if the base class will have a constructor generated by IoCTools.
    /// </summary>
    private static bool WillHaveGeneratedConstructor(INamedTypeSymbol baseClass)
    {
        var hasInjectFields = baseClass.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"));
        var hasInjectConfigurationFields = baseClass.GetMembers().OfType<IFieldSymbol>()
            .Any(field =>
                field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectConfigurationAttribute"));
        var hasDependsOnAttribute = baseClass.GetAttributes()
            .Any(AttributeTypeChecker.IsDependsOnAttribute);
        var hasConditionalServiceAttribute = baseClass.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString() ==
            "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
        var hasRegisterAsAllAttribute = baseClass.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name == "RegisterAsAllAttribute");
        var hasRegisterAsAttribute = baseClass.GetAttributes()
            .Any(attr => AttributeTypeChecker.IsRegisterAsAttribute(attr));
        var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(baseClass);

        return hasInjectFields || hasInjectConfigurationFields || hasDependsOnAttribute ||
               hasConditionalServiceAttribute || hasRegisterAsAllAttribute || hasRegisterAsAttribute ||
               isHostedService;
    }

    /// <summary>
    ///     Checks if the base class has the ExternalService attribute.
    /// </summary>
    private static bool HasExternalServiceAttribute(INamedTypeSymbol baseClass)
    {
        return baseClass.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString() ==
            "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");
    }

    /// <summary>
    ///     Checks if the base class has a parameterless constructor.
    /// </summary>
    private static bool HasParameterlessConstructor(INamedTypeSymbol baseClass)
    {
        return baseClass.GetMembers().OfType<IMethodSymbol>()
            .Any(m => m.MethodKind == MethodKind.Constructor &&
                     !m.IsStatic &&
                     m.Parameters.Length == 0);
    }

    /// <summary>
    ///     Checks if the base class has any constructors.
    /// </summary>
    private static bool HasConstructors(INamedTypeSymbol baseClass)
    {
        return baseClass.GetMembers().OfType<IMethodSymbol>()
            .Any(m => m.MethodKind == MethodKind.Constructor && !m.IsStatic);
    }

    /// <summary>
    ///     Analysis result for a base class regarding constructor generation.
    /// </summary>
    public sealed class BaseClassAnalysis
    {
        /// <summary>
        ///     Represents the case where there is no base class to analyze.
        /// </summary>
        public static readonly BaseClassAnalysis None = new()
        {
            CanAcceptDIParameters = false,
            IsPartial = false,
            WillHaveConstructor = false,
            HasExternalService = false
        };

        public bool CanAcceptDIParameters { get; set; }
        public bool IsPartial { get; set; }
        public bool WillHaveConstructor { get; set; }
        public bool HasExternalService { get; set; }
    }
}
