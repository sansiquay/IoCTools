namespace IoCTools.Generator.Generator;

internal static class ServiceDiscovery
{
    public static (bool HasAny, bool IsScoped, bool IsSingleton, bool IsTransient) GetLifetimeAttributes(
        INamedTypeSymbol classSymbol)
    {
        var current = classSymbol;
        while (current != null)
        {
            var hasScopedAttribute = current.GetAttributes().Any(attr =>
                AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.ScopedAttribute));
            var hasSingletonAttribute = current.GetAttributes().Any(attr =>
                AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.SingletonAttribute));
            var hasTransientAttribute = current.GetAttributes().Any(attr =>
                AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.TransientAttribute));

            if (hasScopedAttribute || hasSingletonAttribute || hasTransientAttribute)
                return (true, hasScopedAttribute, hasSingletonAttribute, hasTransientAttribute);

            current = current.BaseType;
        }

        return (false, false, false, false);
    }

    public static string GetServiceLifetimeFromAttributes(INamedTypeSymbol classSymbol,
        string implicitLifetime = "Scoped")
    {
        var (_, isScoped, isSingleton, isTransient) = GetLifetimeAttributes(classSymbol);
        if (isSingleton) return "Singleton";
        if (isTransient) return "Transient";
        if (isScoped) return "Scoped";
        return implicitLifetime;
    }

    public static (bool HasAny, bool IsScoped, bool IsSingleton, bool IsTransient) GetDirectLifetimeAttributes(
        INamedTypeSymbol classSymbol)
    {
        var hasScopedAttribute = classSymbol.GetAttributes().Any(attr =>
            AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.ScopedAttribute));
        var hasSingletonAttribute = classSymbol.GetAttributes().Any(attr =>
            AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.SingletonAttribute));
        var hasTransientAttribute = classSymbol.GetAttributes().Any(attr =>
            AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.TransientAttribute));

        var hasAny = hasScopedAttribute || hasSingletonAttribute || hasTransientAttribute;
        return (hasAny, hasScopedAttribute, hasSingletonAttribute, hasTransientAttribute);
    }

    public static string? GetDirectLifetimeName(INamedTypeSymbol classSymbol)
    {
        var (_, isScoped, isSingleton, isTransient) = GetDirectLifetimeAttributes(classSymbol);
        if (isSingleton) return "Singleton";
        if (isTransient) return "Transient";
        if (isScoped) return "Scoped";
        return null;
    }

    public static bool HasInjectFieldsAcrossPartialClasses(INamedTypeSymbol classSymbol)
    {
        if (HasFieldWithAttributeAcrossPartialClasses(classSymbol, "Inject", "InjectAttribute"))
            return true;

        return classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"));
    }

    public static bool HasInjectConfigurationFieldsAcrossPartialClasses(INamedTypeSymbol classSymbol)
    {
        if (HasFieldWithAttributeAcrossPartialClasses(classSymbol, "InjectConfiguration", "InjectConfigurationAttribute"))
            return true;

        var hasFieldAttributes = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Any(field =>
                field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectConfigurationAttribute"));
        if (hasFieldAttributes) return true;

        return classSymbol.GetAttributes()
            .Any(AttributeParser.IsDependsOnConfigurationAttribute);
    }

    /// <summary>
    /// Returns true if any base class in the inheritance chain has IoCTools-managed status,
    /// meaning it carries at least one of: [DependsOn*], a lifetime attribute ([Scoped]/[Singleton]/[Transient]),
    /// [ConditionalService], [RegisterAsAll], [RegisterAs], [Inject] field, or [InjectConfiguration] field.
    /// This allows bare derived partial classes to inherit service intent from their base.
    /// </summary>
    public static bool InheritsFromIoCToolsManagedBase(INamedTypeSymbol classSymbol)
    {
        var current = classSymbol.BaseType;
        while (current != null && current.SpecialType == SpecialType.None)
        {
            var attrs = current.GetAttributes();

            // Lifetime attrs
            if (attrs.Any(a => AttributeTypeChecker.IsAttribute(a, AttributeTypeChecker.ScopedAttribute) ||
                               AttributeTypeChecker.IsAttribute(a, AttributeTypeChecker.SingletonAttribute) ||
                               AttributeTypeChecker.IsAttribute(a, AttributeTypeChecker.TransientAttribute)))
                return true;

            // [DependsOn*]
            if (attrs.Any(AttributeTypeChecker.IsDependsOnAttribute))
                return true;

            // [ConditionalService]
            if (attrs.Any(a => AttributeTypeChecker.IsAttribute(a, AttributeTypeChecker.ConditionalServiceAttribute)))
                return true;

            // [RegisterAsAll]
            if (attrs.Any(a => AttributeTypeChecker.IsAttribute(a, AttributeTypeChecker.RegisterAsAllAttribute)))
                return true;

            // [RegisterAs<T>]
            if (attrs.Any(a => AttributeTypeChecker.IsRegisterAsAttribute(a)))
                return true;

            // [Inject] or [InjectConfiguration] fields
            if (current.GetMembers().OfType<IFieldSymbol>().Any(f =>
                    f.GetAttributes().Any(a =>
                        AttributeTypeChecker.IsAttribute(a, AttributeTypeChecker.InjectAttribute) ||
                        AttributeTypeChecker.IsAttribute(a, AttributeTypeChecker.InjectConfigurationAttribute))))
                return true;

            current = current.BaseType;
        }

        return false;
    }

    private static bool HasFieldWithAttributeAcrossPartialClasses(INamedTypeSymbol classSymbol, params string[] attributeNames)
    {
        foreach (var declaringSyntaxRef in classSymbol.DeclaringSyntaxReferences)
            if (declaringSyntaxRef.GetSyntax() is TypeDeclarationSyntax typeDeclaration)
                foreach (var fieldDeclaration in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
                    foreach (var attributeList in fieldDeclaration.AttributeLists)
                        foreach (var attribute in attributeList.Attributes)
                        {
                            var attributeText = attribute.Name.ToString();
                            foreach (var attributeName in attributeNames)
                            {
                                if (attributeText == attributeName || attributeText.EndsWith(attributeName))
                                    return true;
                                // Check for attribute without "Attribute" suffix (e.g., "Inject" matches "InjectAttribute")
                                if (!attributeName.EndsWith("Attribute") &&
                                    (attributeText == attributeName + "Attribute" || attributeText.EndsWith(attributeName + "Attribute")))
                                    return true;
                            }
                        }

        return false;
    }
}
