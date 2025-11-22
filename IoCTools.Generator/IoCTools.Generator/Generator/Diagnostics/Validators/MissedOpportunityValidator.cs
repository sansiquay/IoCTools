namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Utilities;

internal static class MissedOpportunityValidator
{
    internal static void Validate(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel,
        string implicitLifetime)
    {
        if (classSymbol.IsAbstract) return;

        // Skip if already opted-in or external/conditional/register-as
        var hasLifetime = ServiceDiscovery.GetLifetimeAttributes(classSymbol).HasAny;
        var hasDependsOn = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name?.StartsWith("DependsOn") == true);
        var hasInjectFields = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(classSymbol);
        var hasRegisterAsAll = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "RegisterAsAllAttribute");
        var hasRegisterAs = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name?.StartsWith("RegisterAsAttribute") == true);
        var hasConditional = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "ConditionalServiceAttribute");
        var isExternal = classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "ExternalServiceAttribute");

        if (hasLifetime || hasDependsOn || hasInjectFields || hasRegisterAsAll || hasRegisterAs || hasConditional ||
            isExternal)
            return;

        // Must be partial to benefit from generator; otherwise still suggest (info) since user can make it partial.

        var constructors = classSymbol.InstanceConstructors
            .Where(ctor => ctor.DeclaredAccessibility == Accessibility.Public || ctor.DeclaredAccessibility == Accessibility.Internal)
            .Where(ctor => !ctor.IsImplicitlyDeclared)
            .ToList();

        var ctor = constructors.FirstOrDefault(c => c.Parameters.Length > 0);
        if (ctor == null) return;

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
}
