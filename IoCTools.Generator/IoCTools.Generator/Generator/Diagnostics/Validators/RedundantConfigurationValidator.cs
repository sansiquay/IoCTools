namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System.Collections.Immutable;
using System.Linq;

using Intent;

using Utilities;

internal static class RedundantConfigurationValidator
{
    internal static void Validate(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        ValidateRegisterAsMatchesImplementedInterfaces(context, classDeclaration, classSymbol);
        ValidateScopedLifetimeRedundancy(context, classDeclaration, classSymbol);
        ValidateSingletonLifetimeRedundancy(context, classDeclaration, classSymbol);
        ValidateTransientLifetimeRedundancy(context, classDeclaration, classSymbol);
        ValidateDependencySetRedundancy(context, classDeclaration, classSymbol);
        ValidateRegisterAsRedundancy(context, classDeclaration, classSymbol);
        ValidateRegisterAsAllRedundancy(context, classDeclaration, classSymbol);
        ValidateConditionalServiceRedundancy(context, classDeclaration, classSymbol);
        ValidateMissingLifetimeForHostedService(context, classDeclaration, classSymbol);
        SuggestRegisterAsAllForMultiInterface(context, classDeclaration, classSymbol);
        ValidateInheritedLifetimeRedundancy(context, classDeclaration, classSymbol);
        ValidateRegisterAsCombinedWithRegisterAsAll(context, classDeclaration, classSymbol);
        ValidateConflictingLifetimeAttributes(context, classDeclaration, classSymbol);
        ValidateSkipRegistrationOverridesIntent(context, classDeclaration, classSymbol);
        ValidateSkipRegistrationIneffectiveMode(context, classDeclaration, classSymbol);
    }

    private static void ValidateRegisterAsMatchesImplementedInterfaces(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var registerAsAttributes = GetRegisterAsAttributes(classSymbol);
        if (registerAsAttributes.Count == 0) return;

        var implementedInterfaces = InterfaceDiscovery.GetAllInterfacesForService(classSymbol);
        if (implementedInterfaces.Count == 0) return;

        var declaredInterfaceSet = new HashSet<INamedTypeSymbol>(implementedInterfaces, SymbolEqualityComparer.Default);
        if (declaredInterfaceSet.Count == 0) return;

        var registerAsInterfaces = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var symbol in registerAsAttributes
                     .SelectMany(attr => attr.AttributeClass?.TypeArguments ?? ImmutableArray<ITypeSymbol>.Empty)
                     .OfType<INamedTypeSymbol>())
            if (symbol.TypeKind == TypeKind.Interface)
                registerAsInterfaces.Add(symbol);

        if (registerAsInterfaces.Count == 0) return;
        if (!registerAsInterfaces.SetEquals(declaredInterfaceSet)) return;

        var formattedInterfaces = string.Join(
            ", ",
            registerAsInterfaces
                .Select(symbol => symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                .OrderBy(name => name, StringComparer.Ordinal));

        foreach (var attribute in registerAsAttributes)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                           classDeclaration.GetLocation();
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantRegisterAsAttribute,
                location,
                classSymbol.Name,
                formattedInterfaces);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void ValidateScopedLifetimeRedundancy(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var (hasDirectLifetimeAttribute, hasDirectScopedAttribute, _, _) =
            ServiceDiscovery.GetDirectLifetimeAttributes(classSymbol);
        if (!hasDirectLifetimeAttribute || !hasDirectScopedAttribute) return;

        var attributes = classSymbol.GetAttributes();
        if (attributes.Any(IsRegisterAsAllAttribute)) return; // Required lifetime attribute
        if (attributes.Any(IsConditionalServiceAttribute)) return; // Conditional services require explicit lifetime

        var hasRegisterAs = attributes.Any(IsRegisterAsAttribute);
        var hasDependsOn = attributes.Any(IsDependsOnAttribute);
        var hasInjectFields = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(classSymbol);
        var hasInjectConfigurationFields =
            ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
        var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(classSymbol);
        var isPartialWithInterfaces = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) &&
                                      classSymbol.Interfaces.Any();

        var inheritedLifetime = classSymbol.BaseType != null
            ? ServiceDiscovery.GetLifetimeAttributes(classSymbol.BaseType).HasAny
            : false;

        var hasImplicitIntent = ServiceIntentEvaluator.HasExplicitServiceIntent(
            classSymbol,
            hasInjectFields,
            hasInjectConfigurationFields,
            hasDependsOn,
            attributes.Any(IsConditionalServiceAttribute),
            attributes.Any(IsRegisterAsAllAttribute),
            hasRegisterAs,
            inheritedLifetime, // Evaluate intent assuming this class's attribute is removed but inheritance remains
            isHostedService,
            isPartialWithInterfaces);

        if (!hasImplicitIntent) return;

        var reasons = BuildScopedRedundancyReasons(hasDependsOn, hasRegisterAs, hasInjectFields,
            hasInjectConfigurationFields, isHostedService, isPartialWithInterfaces);
        var (baseHasLifetime, baseIsScoped, _, _) = classSymbol.BaseType != null
            ? ServiceDiscovery.GetLifetimeAttributes(classSymbol.BaseType)
            : (false, false, false, false);
        if (baseHasLifetime && baseIsScoped && classSymbol.BaseType != null)
            reasons.Add($"inherits [Scoped] from {classSymbol.BaseType.Name}");
        var reasonText = reasons.Count > 0
            ? string.Join(", ", reasons.Distinct(StringComparer.Ordinal))
            : "existing service intent";

        var scopedAttribute = attributes.FirstOrDefault(IsScopedAttribute);
        var location = scopedAttribute?.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                       classDeclaration.GetLocation();

        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantScopedLifetimeAttribute,
            location,
            classSymbol.Name,
            reasonText);
        context.ReportDiagnostic(diagnostic);
    }

    private static void ValidateSingletonLifetimeRedundancy(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var (hasDirectLifetimeAttribute, _, hasDirectSingletonAttribute, _) =
            ServiceDiscovery.GetDirectLifetimeAttributes(classSymbol);
        if (!hasDirectLifetimeAttribute || !hasDirectSingletonAttribute) return;

        var baseType = classSymbol.BaseType;
        if (baseType == null) return;

        var (baseHasLifetime, _, baseIsSingleton, _) = ServiceDiscovery.GetLifetimeAttributes(baseType);
        if (!baseHasLifetime || !baseIsSingleton) return;

        var singletonAttribute = classSymbol.GetAttributes().FirstOrDefault(IsSingletonAttribute);
        var location = singletonAttribute?.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                       classDeclaration.GetLocation();

        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantSingletonLifetimeAttribute,
            location,
            classSymbol.Name,
            baseType.Name);
        context.ReportDiagnostic(diagnostic);
    }

    private static void ValidateTransientLifetimeRedundancy(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var (hasDirectLifetimeAttribute, _, _, hasDirectTransientAttribute) =
            ServiceDiscovery.GetDirectLifetimeAttributes(classSymbol);
        if (!hasDirectLifetimeAttribute || !hasDirectTransientAttribute) return;

        var baseType = classSymbol.BaseType;
        if (baseType == null) return;

        var (baseHasLifetime, _, _, baseIsTransient) = ServiceDiscovery.GetLifetimeAttributes(baseType);
        if (!baseHasLifetime || !baseIsTransient) return;

        var transientAttribute = classSymbol.GetAttributes().FirstOrDefault(IsTransientAttribute);
        var location = transientAttribute?.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                       classDeclaration.GetLocation();

        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantTransientLifetimeAttribute,
            location,
            classSymbol.Name,
            baseType.Name);
        context.ReportDiagnostic(diagnostic);
    }

    private static void ValidateDependencySetRedundancy(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var baseType = classSymbol.BaseType;
        if (baseType == null || baseType.SpecialType == SpecialType.System_Object) return;

        var baseSetTypes = GetDependencySetTypes(baseType);
        if (baseSetTypes.Count == 0) return;

        var derivedSetTypes = GetDependencySetTypes(classSymbol);
        if (derivedSetTypes.Count == 0) return;

        foreach (var set in derivedSetTypes)
        {
            if (!baseSetTypes.Contains(set, SymbolEqualityComparer.Default)) continue;

            var attribute = classSymbol.GetAttributes()
                .FirstOrDefault(attr => IsDependsOnAttribute(attr) &&
                                        attr.AttributeClass?.TypeArguments.Any(t => SymbolEqualityComparer.Default.Equals(t, set)) == true);

            var location = attribute?.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                           classDeclaration.GetLocation();

            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantDependencySetInInheritance,
                location,
                classSymbol.Name,
                set.Name,
                baseType.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void ValidateRegisterAsRedundancy(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var baseType = classSymbol.BaseType;
        if (baseType == null || baseType.SpecialType == SpecialType.System_Object) return;

        var registerAsAttributes = GetRegisterAsAttributes(classSymbol);
        if (registerAsAttributes.Count == 0) return;

        var baseRegisterAs = GetRegisterAsAttributes(baseType);
        if (baseRegisterAs.Count == 0) return;

        var baseInterfaces = new HashSet<INamedTypeSymbol>(
            baseRegisterAs
                .SelectMany(attr => attr.AttributeClass?.TypeArguments ?? ImmutableArray<ITypeSymbol>.Empty)
                .OfType<INamedTypeSymbol>(),
            SymbolEqualityComparer.Default);

        var derivedInterfaces = new HashSet<INamedTypeSymbol>(
            registerAsAttributes
                .SelectMany(attr => attr.AttributeClass?.TypeArguments ?? ImmutableArray<ITypeSymbol>.Empty)
                .OfType<INamedTypeSymbol>(),
            SymbolEqualityComparer.Default);

        if (!baseInterfaces.SetEquals(derivedInterfaces)) return;

        foreach (var attribute in registerAsAttributes)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ?? classDeclaration.GetLocation();
            var formatted = string.Join(", ",
                baseInterfaces.Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)).OrderBy(n => n,
                    StringComparer.Ordinal));
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantRegisterAsInheritance,
                location,
                classSymbol.Name,
                formatted,
                baseType.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void ValidateRegisterAsAllRedundancy(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var baseType = classSymbol.BaseType;
        if (baseType == null || baseType.SpecialType == SpecialType.System_Object) return;

        var hasRegisterAsAll = classSymbol.GetAttributes().Any(IsRegisterAsAllAttribute);
        if (!hasRegisterAsAll) return;

        var baseHasRegisterAsAll = baseType.GetAttributes().Any(IsRegisterAsAllAttribute);
        if (!baseHasRegisterAsAll) return;

        var registerAsAllAttribute = classSymbol.GetAttributes().FirstOrDefault(IsRegisterAsAllAttribute);
        var location = registerAsAllAttribute?.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                       classDeclaration.GetLocation();

        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantRegisterAsAllInheritance,
            location,
            classSymbol.Name,
            baseType.Name);
        context.ReportDiagnostic(diagnostic);
    }

    private static void ValidateConditionalServiceRedundancy(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var baseType = classSymbol.BaseType;
        if (baseType == null || baseType.SpecialType == SpecialType.System_Object) return;

        var derivedConditional = classSymbol.GetAttributes()
            .Where(IsConditionalServiceAttribute)
            .ToList();
        if (derivedConditional.Count == 0) return;

        var baseConditional = baseType.GetAttributes()
            .Where(IsConditionalServiceAttribute)
            .ToList();
        if (baseConditional.Count == 0) return;

        // Compare constructor arguments; if identical, it's redundant
        foreach (var attr in derivedConditional)
        {
            var match = baseConditional.Any(b => HaveSameConditionalArguments(b, attr));
            if (!match) continue;

            var location = attr.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ?? classDeclaration.GetLocation();
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantConditionalServiceInheritance,
                location,
                classSymbol.Name,
                baseType.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void ValidateMissingLifetimeForHostedService(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var isHosted = TypeAnalyzer.IsAssignableFromIHostedService(classSymbol);
        if (!isHosted) return;

        var hasLifetime = ServiceDiscovery.GetLifetimeAttributes(classSymbol).HasAny;

        // If hosted service only exposes hosting contracts, lifetime should be implicit; flag explicit lifetimes as redundant
        var hasAdditionalInterfaces = classSymbol.Interfaces.Any(iface =>
            iface.ToDisplayString() != "Microsoft.Extensions.Hosting.IHostedService" &&
            iface.ToDisplayString() != "System.IAsyncDisposable");

        if (!hasAdditionalInterfaces)
        {
            if (!hasLifetime) return; // pure hosted, implicit lifetime OK

            var lifetimeAttr = classSymbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() is "IoCTools.Abstractions.Annotations.ScopedAttribute" or
                "IoCTools.Abstractions.Annotations.SingletonAttribute" or
                "IoCTools.Abstractions.Annotations.TransientAttribute");
            var location = lifetimeAttr?.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                           classDeclaration.GetLocation();

            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.HostedServiceMissingLifetime,
                location,
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Hosted service + additional service interfaces: require explicit lifetime so registrations are clear
        if (hasLifetime) return;
        var loc = classDeclaration.Identifier.GetLocation();
        var diag = Diagnostic.Create(DiagnosticDescriptors.DependsOnMissingLifetime,
            loc,
            classSymbol.Name);
        context.ReportDiagnostic(diag);
    }

    private static void SuggestRegisterAsAllForMultiInterface(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var implementedInterfaces = classSymbol.Interfaces
            .Where(i => i.TypeKind == TypeKind.Interface)
            .Select(i => i.ToDisplayString())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (implementedInterfaces.Count < 2) return;

        var hasRegisterAsAll = classSymbol.GetAttributes().Any(IsRegisterAsAllAttribute);
        var hasRegisterAs = classSymbol.GetAttributes().Any(a => AttributeTypeChecker.IsRegisterAsAttribute(a));
        if (hasRegisterAsAll || hasRegisterAs) return;

        var hasLifetime = ServiceDiscovery.GetLifetimeAttributes(classSymbol).HasAny;
        if (!hasLifetime) return; // lifetime suggestion will be handled elsewhere

        var location = classDeclaration.Identifier.GetLocation();
        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.MissingRegisterAsAllForMultiInterface,
            location,
            classSymbol.Name);
        context.ReportDiagnostic(diagnostic);
    }

    private static List<INamedTypeSymbol> GetDependencySetTypes(INamedTypeSymbol symbol)
    {
        var result = new List<INamedTypeSymbol>();
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsDependsOnAttribute(attribute)) continue;
            foreach (var arg in attribute.AttributeClass?.TypeArguments ?? ImmutableArray<ITypeSymbol>.Empty)
            {
                if (arg is INamedTypeSymbol named && DependencySetUtilities.IsDependencySet(named))
                    result.Add(named);
            }
        }

        return result;
    }

    private static void ValidateRegisterAsCombinedWithRegisterAsAll(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var registerAsAttributes = GetRegisterAsAttributes(classSymbol);
        if (registerAsAttributes.Count == 0) return;

        var hasRegisterAsAll = classSymbol.GetAttributes().Any(IsRegisterAsAllAttribute);
        if (!hasRegisterAsAll) return;

        foreach (var attribute in registerAsAttributes)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                           classDeclaration.GetLocation();
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantRegisterAsWithRegisterAsAll,
                location,
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void ValidateConflictingLifetimeAttributes(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var lifetimeAttributes = classSymbol.GetAttributes()
            .Where(attr => IsScopedAttribute(attr) || IsSingletonAttribute(attr) || IsTransientAttribute(attr))
            .ToList();
        if (lifetimeAttributes.Count <= 1) return;

        var formattedNames = lifetimeAttributes
            .Select(attr => attr.AttributeClass?.Name?.Replace("Attribute", string.Empty) ?? "Lifetime")
            .Distinct(StringComparer.Ordinal)
            .Select(name => $"[{name}]")
            .ToList();

        var location = lifetimeAttributes[1].ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                       classDeclaration.GetLocation();

        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.MultipleLifetimeAttributes,
            location,
            classSymbol.Name,
            string.Join(", ", formattedNames));
        context.ReportDiagnostic(diagnostic);
    }

    private static void ValidateInheritedLifetimeRedundancy(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        if (classSymbol.BaseType == null) return;

        var derivedLifetime = ServiceDiscovery.GetDirectLifetimeName(classSymbol);
        if (derivedLifetime == null) return;

        var baseLifetime = ServiceDiscovery.GetDirectLifetimeName(classSymbol.BaseType);
        if (baseLifetime == null) return;

        if (!string.Equals(derivedLifetime, baseLifetime, StringComparison.Ordinal)) return;

        var lifetimeAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(attr =>
                IsScopedAttribute(attr) || IsSingletonAttribute(attr) || IsTransientAttribute(attr));
        var location = lifetimeAttribute?.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                       classDeclaration.GetLocation();

        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.InheritedLifetimeRedundant,
            location,
            classSymbol.Name,
            derivedLifetime,
            classSymbol.BaseType.Name);
        context.ReportDiagnostic(diagnostic);
    }

    private static void ValidateSkipRegistrationOverridesIntent(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var skipAllAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(IsNonGenericSkipRegistrationAttribute);
        if (skipAllAttribute == null) return;

        var conflicts = new List<string>();

        var lifetimeAttributes = classSymbol.GetAttributes()
            .Where(attr => IsScopedAttribute(attr) || IsSingletonAttribute(attr) || IsTransientAttribute(attr))
            .Select(attr => attr.AttributeClass?.Name?.Replace("Attribute", string.Empty) ?? "Lifetime")
            .ToList();
        if (lifetimeAttributes.Any())
            conflicts.AddRange(lifetimeAttributes.Select(name => $"[{name}]"));

        if (classSymbol.GetAttributes().Any(IsRegisterAsAllAttribute)) conflicts.Add("[RegisterAsAll]");
        if (GetRegisterAsAttributes(classSymbol).Any()) conflicts.Add("[RegisterAs]");
        if (classSymbol.GetAttributes().Any(IsConditionalServiceAttribute)) conflicts.Add("[ConditionalService]");

        if (!conflicts.Any()) return;

        var location = skipAllAttribute.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                       classDeclaration.GetLocation();
        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SkipRegistrationOverridesOtherAttributes,
            location,
            classSymbol.Name,
            string.Join(", ", conflicts.Distinct(StringComparer.Ordinal)));
        context.ReportDiagnostic(diagnostic);
    }

    private static void ValidateSkipRegistrationIneffectiveMode(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var registerAsAllAttribute = classSymbol.GetAttributes().FirstOrDefault(IsRegisterAsAllAttribute);
        if (registerAsAllAttribute == null) return;

        var registrationMode = AttributeParser.GetRegistrationMode(registerAsAllAttribute);
        if (!string.Equals(registrationMode, "DirectOnly", StringComparison.Ordinal)) return;

        var genericSkipAttributes = classSymbol.GetAttributes()
            .Where(IsGenericSkipRegistrationAttribute)
            .ToList();
        if (!genericSkipAttributes.Any()) return;

        foreach (var attribute in genericSkipAttributes)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                           classDeclaration.GetLocation();
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SkipRegistrationIneffectiveInDirectMode,
                location,
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static List<string> BuildScopedRedundancyReasons(bool hasDependsOn,
        bool hasRegisterAs,
        bool hasInjectFields,
        bool hasInjectConfigurationFields,
        bool isHostedService,
        bool isPartialWithInterfaces)
    {
        var reasons = new List<string>();
        if (hasDependsOn) reasons.Add("[DependsOn]");
        if (hasRegisterAs) reasons.Add("[RegisterAs]");
        if (hasInjectFields && !hasInjectConfigurationFields) reasons.Add("[Inject]");
        if (isHostedService) reasons.Add("BackgroundService inheritance");
        if (!reasons.Any() && isPartialWithInterfaces) reasons.Add("partial interface type");
        return reasons;
    }

    private static List<AttributeData> GetRegisterAsAttributes(INamedTypeSymbol classSymbol)
    {
        return classSymbol.GetAttributes()
            .Where(IsRegisterAsAttribute)
            .ToList();
    }

    private static bool IsRegisterAsAttribute(AttributeData attribute)
        => AttributeTypeChecker.IsRegisterAsAttribute(attribute);

    private static bool IsRegisterAsAllAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
           "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute";

    private static bool IsConditionalServiceAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
           "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute";

    private static bool IsDependsOnAttribute(AttributeData attribute)
        => attribute.AttributeClass?.Name?.StartsWith("DependsOn", StringComparison.Ordinal) == true;

    private static bool IsScopedAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
           "IoCTools.Abstractions.Annotations.ScopedAttribute";

    private static bool IsSingletonAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
           "IoCTools.Abstractions.Annotations.SingletonAttribute";

    private static bool IsTransientAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
           "IoCTools.Abstractions.Annotations.TransientAttribute";

    private static bool IsNonGenericSkipRegistrationAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
           "IoCTools.Abstractions.Annotations.SkipRegistrationAttribute";

    private static bool IsGenericSkipRegistrationAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString()
                .StartsWith("IoCTools.Abstractions.Annotations.SkipRegistrationAttribute", StringComparison.Ordinal) ==
            true && attribute.AttributeClass?.IsGenericType == true;

    private static bool IsExternalServiceAttribute(AttributeData attribute)
        => AttributeTypeChecker.IsAttribute(attribute, AttributeTypeChecker.ExternalServiceAttribute);

    private static bool HaveSameConditionalArguments(AttributeData left, AttributeData right)
    {
        if (left.ConstructorArguments.Length != right.ConstructorArguments.Length) return false;
        for (var i = 0; i < left.ConstructorArguments.Length; i++)
        {
            if (!Equals(left.ConstructorArguments[i].Value, right.ConstructorArguments[i].Value))
                return false;
        }

        return true;
    }
}
