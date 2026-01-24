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
                attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ScopedAttribute");
            var hasSingletonAttribute = current.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.SingletonAttribute");
            var hasTransientAttribute = current.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.TransientAttribute");

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
            attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ScopedAttribute");
        var hasSingletonAttribute = classSymbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.SingletonAttribute");
        var hasTransientAttribute = classSymbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.TransientAttribute");

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
        foreach (var declaringSyntaxRef in classSymbol.DeclaringSyntaxReferences)
            if (declaringSyntaxRef.GetSyntax() is TypeDeclarationSyntax typeDeclaration)
                foreach (var fieldDeclaration in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
                    foreach (var attributeList in fieldDeclaration.AttributeLists)
                        foreach (var attribute in attributeList.Attributes)
                        {
                            var attributeText = attribute.Name.ToString();
                            if (attributeText == "Inject" || attributeText == "InjectAttribute" ||
                                attributeText.EndsWith("Inject") || attributeText.EndsWith("InjectAttribute"))
                                return true;
                        }

        return classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"));
    }

    public static bool HasInjectConfigurationFieldsAcrossPartialClasses(INamedTypeSymbol classSymbol)
    {
        foreach (var declaringSyntaxRef in classSymbol.DeclaringSyntaxReferences)
            if (declaringSyntaxRef.GetSyntax() is TypeDeclarationSyntax typeDeclaration)
                foreach (var fieldDeclaration in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
                    foreach (var attributeList in fieldDeclaration.AttributeLists)
                        foreach (var attribute in attributeList.Attributes)
                        {
                            var attributeText = attribute.Name.ToString();
                            if (attributeText == "InjectConfiguration" || attributeText == "InjectConfigurationAttribute" ||
                                attributeText.EndsWith("InjectConfiguration") ||
                                attributeText.EndsWith("InjectConfigurationAttribute"))
                                return true;
                        }

        var hasFieldAttributes = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Any(field =>
                field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectConfigurationAttribute"));
        if (hasFieldAttributes) return true;

        return classSymbol.GetAttributes()
            .Any(AttributeParser.IsDependsOnConfigurationAttribute);
    }
}
