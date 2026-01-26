namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using IoCTools.Generator.Models;
using Utilities;

internal static class MissedOpportunityValidator
{
    /// <summary>
    ///     Known framework base types that have their own registration mechanisms.
    ///     Classes inheriting from these should not be suggested for IoCTools attributes.
    /// </summary>
    private static HashSet<string> FrameworkBaseTypes => DiagnosticConfiguration.GetDefaultFrameworkBaseTypes();

    internal static void Validate(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel,
        string implicitLifetime,
        DiagnosticConfiguration? diagnosticConfig = null)
    {
        if (classSymbol.IsAbstract) return;

        // Skip if already opted-in or external/conditional/register-as
        var hasLifetime = ServiceDiscovery.GetLifetimeAttributes(classSymbol).HasAny;
        var hasDependsOn = classSymbol.GetAttributes().Any(AttributeTypeChecker.IsDependsOnAttribute);
        var hasInjectFields = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(classSymbol);
        var hasRegisterAsAll = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "RegisterAsAllAttribute");
        var hasRegisterAs = classSymbol.GetAttributes().Any(a => AttributeTypeChecker.IsRegisterAsAttribute(a));
        var hasConditional = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "ConditionalServiceAttribute");
        var isExternal = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "ExternalServiceAttribute");
        var hasSkipRegistration = classSymbol.GetAttributes().Any(AttributeTypeChecker.IsSkipRegistrationAttribute);

        if (hasLifetime || hasDependsOn || hasInjectFields || hasRegisterAsAll || hasRegisterAs || hasConditional ||
            isExternal || hasSkipRegistration)
            return;

        // Skip classes that inherit from known framework base types
        var frameworkBaseTypes = diagnosticConfig?.FrameworkBaseTypes ?? FrameworkBaseTypes;
        if (InheritsFromFrameworkType(classSymbol, frameworkBaseTypes))
            return;

        // Must be partial to benefit from generator; otherwise still suggest (info) since user can make it partial.

        var constructors = classSymbol.InstanceConstructors
            .Where(ctor => ctor.DeclaredAccessibility == Accessibility.Public || ctor.DeclaredAccessibility == Accessibility.Internal)
            .Where(ctor => !ctor.IsImplicitlyDeclared)
            .ToList();

        var ctor = constructors.FirstOrDefault(c => c.Parameters.Length > 0);
        if (ctor == null) return;

        // Skip if constructor calls base(...) with arguments - indicates framework integration
        if (HasBaseConstructorCallWithArguments(classDeclaration, ctor))
            return;

        // Require all parameters to be non-primitive reference or interface types
        var injectableParams = ctor.Parameters
            .Where(p => !p.Type.IsValueType && p.Type.SpecialType == SpecialType.None)
            .OfType<IParameterSymbol>()
            .ToList();

        if (injectableParams.Count != ctor.Parameters.Length) return;

        var paramInterfaces = injectableParams
            .Select(p => p.Type.ToDisplayString())
            .ToArray();

        var joined = string.Join(", ", paramInterfaces);

        var location = classDeclaration.Identifier.GetLocation();
        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.ConstructorCouldUseDependsOn,
            location,
            classSymbol.Name,
            joined);
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    ///     Checks if the class inherits from any known framework base type.
    /// </summary>
    private static bool InheritsFromFrameworkType(INamedTypeSymbol classSymbol, HashSet<string> frameworkBaseTypes)
    {
        // Check base type chain
        var current = classSymbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (IsFrameworkType(current, frameworkBaseTypes))
                return true;
            current = current.BaseType;
        }

        // Check implemented interfaces
        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (IsFrameworkType(iface, frameworkBaseTypes))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Checks if a type matches any known framework type by name.
    /// </summary>
    private static bool IsFrameworkType(INamedTypeSymbol type, HashSet<string> frameworkBaseTypes)
    {
        var fullName = type.ToDisplayString();
        var simpleName = type.Name;

        // Check exact full name match
        if (frameworkBaseTypes.Contains(fullName))
            return true;

        // Check simple name match
        if (frameworkBaseTypes.Contains(simpleName))
            return true;

        // For generic types, also check the constructed-from type
        if (type.IsGenericType)
        {
            var constructedFrom = type.ConstructedFrom;
            var constructedFullName = constructedFrom.ToDisplayString();
            var constructedSimpleName = constructedFrom.Name;

            if (frameworkBaseTypes.Contains(constructedFullName) ||
                frameworkBaseTypes.Contains(constructedSimpleName))
                return true;

            // Check with arity suffix (e.g., "AuthenticationHandler`1")
            var nameWithArity = constructedSimpleName + "`" + type.TypeArguments.Length;
            if (frameworkBaseTypes.Any(f => f.EndsWith(nameWithArity, StringComparison.Ordinal) ||
                                            f.EndsWith("." + nameWithArity, StringComparison.Ordinal)))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Checks if a constructor has a base(...) initializer with arguments.
    ///     This is a strong signal that the class is integrating with a framework base class.
    /// </summary>
    private static bool HasBaseConstructorCallWithArguments(TypeDeclarationSyntax classDeclaration,
        IMethodSymbol ctorSymbol)
    {
        // Find the constructor syntax that matches this symbol
        var ctorSyntaxes = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => c.ParameterList.Parameters.Count == ctorSymbol.Parameters.Length);

        foreach (var ctorSyntax in ctorSyntaxes)
        {
            // Check if this constructor has a base(...) initializer with arguments
            if (ctorSyntax.Initializer is { ThisOrBaseKeyword.Text: "base" } initializer &&
                initializer.ArgumentList.Arguments.Count > 0)
            {
                return true;
            }
        }

        return false;
    }
}
