namespace IoCTools.Generator.Generator.Diagnostics.Validators;

internal static class DependencyUsageValidator
{
    internal static void ValidateRedundantDependencies(
        ReportDiagnosticDelegate reportDiagnostic,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        var rawDependencies = hierarchyDependencies.RawAllDependencies;
        if (rawDependencies == null || rawDependencies.Count == 0) return;

        var grouped = rawDependencies
            .Where(d => d.Source == DependencySource.Inject ||
                        d.Source == DependencySource.DependsOn ||
                        d.Source == DependencySource.ConfigurationInjection)
            .GroupBy(d => d.ServiceType, SymbolEqualityComparer.Default);

        foreach (var group in grouped)
        {
            var entries = group.ToList();
            if (entries.Count <= 1) continue;
            if (!entries.Any(e => e.Level == 0)) continue; // Current class must participate
            if (!entries.Any(e => e.Source == DependencySource.Inject ||
                                  e.Source == DependencySource.ConfigurationInjection)) continue;

            if (group.Key is not ITypeSymbol dependencyType) continue;
            var detail = BuildSourceSummary(entries);
            if (string.IsNullOrEmpty(detail)) continue;

            var location = ResolveRedundantLocation(classSymbol, classDeclaration, entries, dependencyType);
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantDependencyDeclarations,
                location,
                TypeNameUtilities.FormatTypeNameForDiagnostic(dependencyType),
                detail,
                classSymbol.Name);

            reportDiagnostic(diagnostic);
        }
    }

    internal static void ValidateUnusedDependencies(ReportDiagnosticDelegate reportDiagnostic,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel? semanticModel,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        if (semanticModel == null) return;

        var compilation = semanticModel.Compilation;
        var partialDeclarations = GetPartialDeclarations(classSymbol, compilation);
        if (partialDeclarations.Count == 0)
            partialDeclarations.Add((classDeclaration, semanticModel));

        var injectFields = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Where(IsInjectField)
            .ToDictionary(f => f.Name, f => f, StringComparer.Ordinal);

        var rawDependencies = hierarchyDependencies.RawAllDependencies;
        if (rawDependencies == null || rawDependencies.Count == 0) return;

        var currentDependencies = rawDependencies
            .Where(d => d.Level == 0 && (d.Source == DependencySource.Inject ||
                                         d.Source == DependencySource.DependsOn ||
                                         d.Source == DependencySource.ConfigurationInjection))
            .GroupBy(d => d.FieldName)
            .Select(g => g.First())
            .ToList();

        foreach (var dependency in currentDependencies)
            if (dependency.Source == DependencySource.Inject)
            {
                if (dependency.FieldName == null) continue;
                if (!injectFields.TryGetValue(dependency.FieldName, out var fieldSymbol)) continue;
                if (!ShouldCheckInjectField(fieldSymbol)) continue;
                if (IsFieldUsed(fieldSymbol, partialDeclarations)) continue;

                var location = fieldSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation() ??
                               classDeclaration.Identifier.GetLocation();
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnusedDependency,
                    location,
                    dependency.FieldName,
                    TypeNameUtilities.FormatTypeNameForDiagnostic(dependency.ServiceType),
                    "the [Inject] field",
                    classSymbol.Name);
                reportDiagnostic(diagnostic);
            }
            else if (dependency.Source == DependencySource.DependsOn)
            {
                if (dependency.FieldName == null) continue;
                if (classSymbol.IsAbstract) continue; // Generated fields are protected for abstract types
                if (IsGeneratedFieldReferenced(dependency.FieldName, partialDeclarations, classSymbol)) continue;

                var location = FindDependsOnAttributeLocation(classSymbol, dependency.ServiceType) ??
                               classDeclaration.Identifier.GetLocation();
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnusedDependency,
                    location,
                    dependency.FieldName,
                    TypeNameUtilities.FormatTypeNameForDiagnostic(dependency.ServiceType),
                    "a [DependsOn] attribute",
                    classSymbol.Name);
                reportDiagnostic(diagnostic);
            }
            else if (dependency.Source == DependencySource.ConfigurationInjection)
            {
                if (dependency.FieldName == null) continue;

                // If it is a user-declared field, reuse Inject logic; otherwise treat like generated field
                var fieldSymbol = classSymbol.GetMembers().OfType<IFieldSymbol>()
                    .FirstOrDefault(f => f.Name == dependency.FieldName);

                if (fieldSymbol != null)
                {
                    if (!ShouldCheckInjectField(fieldSymbol)) continue;
                    if (IsFieldUsed(fieldSymbol, partialDeclarations)) continue;

                    var location = fieldSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation() ??
                                   classDeclaration.Identifier.GetLocation();
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnusedDependency,
                        location,
                        dependency.FieldName,
                        TypeNameUtilities.FormatTypeNameForDiagnostic(dependency.ServiceType),
                        "[InjectConfiguration]",
                        classSymbol.Name);
                    reportDiagnostic(diagnostic);
                }
                else
                {
                    if (IsGeneratedFieldReferenced(dependency.FieldName, partialDeclarations, classSymbol)) continue;

                    var location = classDeclaration.Identifier.GetLocation();
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnusedDependency,
                        location,
                        dependency.FieldName,
                        TypeNameUtilities.FormatTypeNameForDiagnostic(dependency.ServiceType),
                        "[DependsOnConfiguration]",
                        classSymbol.Name);
                    reportDiagnostic(diagnostic);
                }
            }
    }

    internal static void ValidateManualDependencyFieldShadows(ReportDiagnosticDelegate reportDiagnostic,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel? semanticModel,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        if (semanticModel == null) return;

        var rawDependencies = hierarchyDependencies.RawAllDependencies;
        if (rawDependencies == null || rawDependencies.Count == 0) return;

        // Only consider dependencies declared on the current class (level 0) that would generate fields
        var currentDependencies = rawDependencies
            .Where(d => d.Level == 0 &&
                        (d.Source == DependencySource.DependsOn ||
                         d.Source == DependencySource.ConfigurationInjection))
            .Where(d => !string.IsNullOrEmpty(d.FieldName))
            .ToList();

        if (currentDependencies.Count == 0) return;

        // Gather fields declared in the current class (not base types)
        var declaredFields = classSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => SymbolEqualityComparer.Default.Equals(f.ContainingType, classSymbol))
            .ToDictionary(f => f.Name, f => f, StringComparer.Ordinal);

        if (declaredFields.Count == 0) return;

        foreach (var dependency in currentDependencies)
        {
            if (dependency.FieldName == null) continue;

            if (!declaredFields.TryGetValue(dependency.FieldName, out var fieldSymbol))
                continue;

            // If the field is explicitly declared with IoCTools injection attributes, it's intentional and
            // not shadowing a generated field. Inject/InjectConfiguration already consume the field.
            var fieldAttributes = fieldSymbol.GetAttributes();
            if (fieldAttributes.Any(a => a.AttributeClass?.Name is "InjectAttribute" or "InjectConfigurationAttribute"))
                continue;

            var location = fieldSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation()
                           ?? classDeclaration.Identifier.GetLocation();

            var diag = Diagnostic.Create(DiagnosticDescriptors.ManualDependencyFieldShadowsGenerated,
                location,
                dependency.FieldName,
                classSymbol.Name,
                TypeNameUtilities.FormatTypeNameForDiagnostic(dependency.ServiceType));
            reportDiagnostic(diag);

            // MemberNames entry for this dependency is suppressed; warn so the attribute is corrected
            ReportSuppressedMemberNames(reportDiagnostic, classSymbol, dependency.ServiceType, dependency.FieldName);
        }
    }

    private static void ReportSuppressedMemberNames(ReportDiagnosticDelegate reportDiagnostic,
        INamedTypeSymbol classSymbol,
        ITypeSymbol dependencyType,
        string fieldName)
    {
        foreach (var attribute in classSymbol.GetAttributes())
        {
            if (attribute.AttributeClass == null) continue;
            var isDependsOn = attribute.AttributeClass.Name.StartsWith("DependsOnAttribute", StringComparison.Ordinal);
            var isDependsOnConfig = AttributeParser.IsDependsOnConfigurationAttribute(attribute);
            if (!isDependsOn && !isDependsOnConfig) continue;

            var typeArguments = attribute.AttributeClass.TypeArguments;
            if (typeArguments.Length == 0) continue;
            if (!typeArguments.Any(t => SymbolEqualityComparer.Default.Equals(t, dependencyType))) continue;

            var memberNames = isDependsOn
                ? AttributeParser.GetDependsOnOptionsFromAttribute(attribute).memberNames
                : GetStringArray(attribute, "MemberNames");

            if (memberNames is null || memberNames.Length == 0) continue;

            foreach (var memberName in memberNames)
            {
                if (string.IsNullOrWhiteSpace(memberName)) continue;
                if (!string.Equals(memberName, fieldName, StringComparison.Ordinal)) continue;

                var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                               ?? classSymbol.Locations.FirstOrDefault();
                if (location == null) continue;

                var diag = Diagnostic.Create(DiagnosticDescriptors.DependsOnMemberNameSuppressedByField,
                    location,
                    memberName,
                    attribute.AttributeClass.Name,
                    classSymbol.Name);
                reportDiagnostic(diag);
            }
        }
    }

    private static string[] GetStringArray(AttributeData attribute, string argumentName)
    {
        var named = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == argumentName);
        if (named.Value.Values is { Length: > 0 } values)
            return values.Select(v => v.Value?.ToString() ?? string.Empty).ToArray();

        return Array.Empty<string>();
    }

    internal static void ValidateRedundantDependencyWrappers(ReportDiagnosticDelegate reportDiagnostic,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel? semanticModel,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        if (semanticModel == null) return;

        var dependencyLookup = hierarchyDependencies.RawAllDependencies
            ?.Where(d => d.FieldName is not null)
            .GroupBy(d => d.FieldName!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().ServiceType, StringComparer.Ordinal);

        if (dependencyLookup == null || dependencyLookup.Count == 0) return;

        var compilation = semanticModel.Compilation;
        var partialDeclarations = GetPartialDeclarations(classSymbol, compilation);
        if (partialDeclarations.Count == 0)
            partialDeclarations.Add((classDeclaration, semanticModel));

        foreach (var (typeSyntax, model) in partialDeclarations)
            foreach (var property in typeSyntax.Members.OfType<PropertyDeclarationSyntax>())
            {
                var propertySymbol = model.GetDeclaredSymbol(property) as IPropertySymbol;
                if (propertySymbol == null) continue;
                if (propertySymbol.SetMethod != null) continue;
                if (propertySymbol.IsIndexer) continue;
                if (propertySymbol.ExplicitInterfaceImplementations.Length > 0) continue;

                var returnedIdentifier = ExtractReturnedIdentifier(property);
                if (returnedIdentifier == null) continue;

                var fieldName = returnedIdentifier.Identifier.Text;
                if (!dependencyLookup.TryGetValue(fieldName, out var dependencyType)) continue;

                var referencedSymbol = model.GetSymbolInfo(returnedIdentifier).Symbol;
                if (referencedSymbol != null && referencedSymbol is not IFieldSymbol) continue;

                if (!SymbolEqualityComparer.Default.Equals(
                        dependencyType.WithNullableAnnotation(NullableAnnotation.None),
                        propertySymbol.Type.WithNullableAnnotation(NullableAnnotation.None)))
                    continue;

                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.RedundantDependencyWrapper,
                    property.GetLocation(),
                    propertySymbol.Name,
                    classSymbol.Name,
                    fieldName);

                reportDiagnostic(diagnostic);
            }
    }

    private static bool IsInjectField(IFieldSymbol fieldSymbol) => fieldSymbol.GetAttributes()
        .Any(attr => attr.AttributeClass?.Name == "InjectAttribute");

    private static bool ShouldCheckInjectField(IFieldSymbol fieldSymbol) =>
        fieldSymbol.DeclaredAccessibility == Accessibility.Private;

    private static bool IsFieldUsed(IFieldSymbol fieldSymbol,
        List<(TypeDeclarationSyntax TypeSyntax, SemanticModel Model)> partialDeclarations)
    {
        var declarationSpans = fieldSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax().Span)
            .ToList();

        foreach (var (typeSyntax, model) in partialDeclarations)
            foreach (var identifier in typeSyntax.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var symbol = model.GetSymbolInfo(identifier).Symbol;
                if (!SymbolEqualityComparer.Default.Equals(symbol, fieldSymbol)) continue;

                var span = identifier.Span;
                if (declarationSpans.Any(s => s.Equals(span))) continue;
                return true;
            }

        return false;
    }

    private static bool IsGeneratedFieldReferenced(string fieldName,
        List<(TypeDeclarationSyntax TypeSyntax, SemanticModel Model)> partialDeclarations,
        INamedTypeSymbol classSymbol)
    {
        foreach (var (typeSyntax, model) in partialDeclarations)
            foreach (var identifier in typeSyntax.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!string.Equals(identifier.Identifier.Text, fieldName, StringComparison.Ordinal)) continue;

                var symbol = model.GetSymbolInfo(identifier).Symbol;
                if (symbol == null) return true; // Referencing generated field before it exists in compilation model

                if (symbol is IFieldSymbol fieldSymbol &&
                    SymbolEqualityComparer.Default.Equals(fieldSymbol.ContainingType, classSymbol))
                    return true;
            }

        return false;
    }

    private static Location? FindDependsOnAttributeLocation(INamedTypeSymbol classSymbol,
        ITypeSymbol dependencyType)
    {
        foreach (var attribute in classSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.Name?.StartsWith("DependsOn", StringComparison.Ordinal) != true)
                continue;

            var typeArguments = attribute.AttributeClass?.TypeArguments ?? default;
            if (typeArguments.Length == 0) continue;

            if (!typeArguments.Any(arg => SymbolEqualityComparer.Default.Equals(arg, dependencyType))) continue;

            return attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
        }

        return null;
    }

    private static List<(TypeDeclarationSyntax TypeSyntax, SemanticModel Model)> GetPartialDeclarations(
        INamedTypeSymbol classSymbol,
        Compilation compilation)
    {
        var result = new List<(TypeDeclarationSyntax, SemanticModel)>();
        foreach (var syntaxReference in classSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax typeSyntax) continue;
            var model = compilation.GetSemanticModel(typeSyntax.SyntaxTree);
            result.Add((typeSyntax, model));
        }

        return result;
    }

    private static IdentifierNameSyntax? ExtractReturnedIdentifier(PropertyDeclarationSyntax property)
    {
        if (property.ExpressionBody?.Expression is IdentifierNameSyntax arrowIdentifier)
            return arrowIdentifier;

        var getter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
        if (getter?.ExpressionBody?.Expression is IdentifierNameSyntax getterIdentifier)
            return getterIdentifier;

        if (getter?.Body is { Statements: { Count: 1 } statements } &&
            statements[0] is ReturnStatementSyntax { Expression: IdentifierNameSyntax returnIdentifier })
            return returnIdentifier;

        return null;
    }

    private static string BuildSourceSummary(
        List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)> entries)
    {
        var injectNames = entries.Where(e => e.Source == DependencySource.Inject && !string.IsNullOrEmpty(e.FieldName))
            .Select(e => FormatFieldName(e.FieldName!, e.Level))
            .Distinct()
            .ToList();
        var dependsOnNames = entries
            .Where(e => e.Source == DependencySource.DependsOn && !string.IsNullOrEmpty(e.FieldName))
            .Select(e => FormatFieldName(e.FieldName!, e.Level))
            .Distinct()
            .ToList();
        var configNames = entries
            .Where(e => e.Source == DependencySource.ConfigurationInjection && !string.IsNullOrEmpty(e.FieldName))
            .Select(e => FormatFieldName(e.FieldName!, e.Level))
            .Distinct()
            .ToList();

        var parts = new List<string>();
        if (injectNames.Any()) parts.Add($"[Inject] fields {string.Join(", ", injectNames)}");
        if (dependsOnNames.Any()) parts.Add($"[DependsOn] attributes {string.Join(", ", dependsOnNames)}");
        if (configNames.Any()) parts.Add($"configuration bindings {string.Join(", ", configNames)}");

        return string.Join(" and ", parts);
    }

    private static string FormatFieldName(string fieldName,
        int level) => level == 0 ? $"'{fieldName}'" : $"'{fieldName}' (base)";

    private static Location ResolveRedundantLocation(INamedTypeSymbol classSymbol,
        TypeDeclarationSyntax classDeclaration,
        List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)> entries,
        ITypeSymbol dependencyType)
    {
        var injectCurrent = entries.FirstOrDefault(e =>
            e.Source == DependencySource.Inject && e.Level == 0 && !string.IsNullOrEmpty(e.FieldName));
        if (!string.IsNullOrEmpty(injectCurrent.FieldName))
        {
            var fieldSymbol = classSymbol.GetMembers().OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.Name == injectCurrent.FieldName);
            var location = fieldSymbol?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation();
            if (location != null) return location;
        }

        var dependsOnCurrent = entries.FirstOrDefault(e =>
            e.Source == DependencySource.DependsOn && e.Level == 0);
        if (dependsOnCurrent.FieldName != null)
        {
            var attrLocation = FindDependsOnAttributeLocation(classSymbol, dependencyType);
            if (attrLocation != null) return attrLocation;
        }

        return classDeclaration.Identifier.GetLocation();
    }
}
