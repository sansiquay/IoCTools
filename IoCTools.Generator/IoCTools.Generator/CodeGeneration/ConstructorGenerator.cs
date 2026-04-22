namespace IoCTools.Generator.CodeGeneration;

using System.Text;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis.CSharp;

internal static partial class ConstructorGenerator
{
    /// <summary>
    ///     Generate inheritance-aware constructor with SourceProductionContext (for IIncrementalGenerator)
    /// </summary>
    public static string GenerateInheritanceAwareConstructorCodeWithContext(TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        SemanticModel semanticModel,
        SourceProductionContext context,
        IReadOnlyDictionary<string, string>? autoDepsOptions = null)
    {
        return GenerateInheritanceAwareConstructorCodeCore(classDeclaration, hierarchyDependencies, semanticModel,
            context.ReportDiagnostic, autoDepsOptions);
    }

    /// <summary>
    ///     Generate inheritance-aware constructor with GeneratorExecutionContext (for legacy support)
    /// </summary>
    public static string GenerateInheritanceAwareConstructorCode(TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        SemanticModel semanticModel,
        GeneratorExecutionContext context)
    {
        return GenerateInheritanceAwareConstructorCodeCore(classDeclaration, hierarchyDependencies, semanticModel,
            context.ReportDiagnostic, autoDepsOptions: null);
    }

    /// <summary>
    ///     Core constructor generation logic that can be used with different context types
    /// </summary>
    private static string GenerateInheritanceAwareConstructorCodeCore(TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        SemanticModel semanticModel,
        Action<Diagnostic> reportDiagnostic,
        IReadOnlyDictionary<string, string>? autoDepsOptions)
    {
        try
        {
            // Defensive null checks
            if (classDeclaration == null)
                throw new ArgumentNullException(nameof(classDeclaration));
            if (hierarchyDependencies == null)
                throw new ArgumentNullException(nameof(hierarchyDependencies));
            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            // Check if class is marked as partial - constructor generation only works for partial classes
            // The diagnostic for missing partial keyword is now handled by IOC080 in DiagnosticsRunner
            var isPartial = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
            if (!isPartial)
            {
                // Return empty string - do not generate constructor for non-partial classes
                return "";
            }

            var classSymbol = ResolveDeclaredClassSymbol(classDeclaration, semanticModel, reportDiagnostic);
            if (classSymbol == null)
                return "";

            var uniqueNamespaces = new HashSet<string>();

            if (hierarchyDependencies.AllDependencies != null)
                foreach (var (serviceType, _, _) in hierarchyDependencies.AllDependencies)
                    if (serviceType != null)
                    {
                        CollectNamespaces(serviceType, uniqueNamespaces);

                        // Special handling for common generic types like ILogger<T>
                        if (serviceType is INamedTypeSymbol namedType && namedType.IsGenericType)
                        {
                            var fullTypeName = namedType.OriginalDefinition.ToDisplayString();
                            if (fullTypeName.StartsWith("Microsoft.Extensions.Logging.ILogger<"))
                                uniqueNamespaces.Add("Microsoft.Extensions.Logging");
                        }
                    }

            var configFields = classSymbol != null
                ? DependencyAnalyzer.GetConfigurationInjectedFieldsForType(classSymbol, semanticModel)
                : new List<ConfigurationInjectionInfo>();

            // Add configuration dependencies to namespace collection
            if (classSymbol != null)
            {
                var configDependenciesForNamespaces =
                    GetConfigurationDependencies(classSymbol, semanticModel, uniqueNamespaces);
                foreach (var (serviceType, _, _) in configDependenciesForNamespaces)
                    if (serviceType != null)
                        CollectNamespaces(serviceType, uniqueNamespaces);

                // Add configuration-specific namespaces for section binding
                if (configFields.Any())
                {
                    // Always add System namespace for built-in types like TimeSpan
                    uniqueNamespaces.Add("System");

                    // Add Microsoft.Extensions.Configuration for IConfiguration interface and extension methods
                    uniqueNamespaces.Add("Microsoft.Extensions.Configuration");

                    // Add Microsoft.Extensions.Options for options pattern types or SupportsReloading
                    if (configFields.Any(f => f.IsOptionsPattern || f.SupportsReloading))
                        uniqueNamespaces.Add("Microsoft.Extensions.Options");

                    // Add System.Collections.Generic for collection types
                    if (configFields.Any(f => CollectionUtilities.IsCollectionType(f.FieldType)))
                    {
                        uniqueNamespaces.Add("System.Collections.Generic");
                        uniqueNamespaces.Add("System.Collections");
                    }

                    // CRITICAL FIX: Collect namespaces from all configuration field types
                    foreach (var configField in configFields)
                        CollectNamespaces(configField.FieldType, uniqueNamespaces);
                }
            }

            // Collect namespaces from generic constraint types
            if (classDeclaration.TypeParameterList != null && classDeclaration.ConstraintClauses.Any())
                foreach (var constraintClause in classDeclaration.ConstraintClauses)
                    CollectNamespacesFromConstraints(constraintClause, semanticModel, uniqueNamespaces);

            var namespaceName = GetClassNamespace(classDeclaration) ?? "";

            var accessibilityModifier = classSymbol != null ? GetClassAccessibilityModifier(classSymbol) : "public";

            // Get the type declaration keyword (class, record, struct, etc.)
            var typeKeyword = GetTypeDeclarationKeyword(classDeclaration);

            // Create namespaces for using statements, excluding self-namespace to avoid redundant using statements
            var namespacesForUsings = new HashSet<string>(uniqueNamespaces);
            if (!string.IsNullOrEmpty(namespaceName)) namespacesForUsings.Remove(namespaceName);

            // Create namespaces for type name stripping (includes self-namespace for clean type names)
            var namespacesForStripping = new HashSet<string>(uniqueNamespaces);

            var usings = new StringBuilder();
            foreach (var ns in namespacesForUsings)
                if (!string.IsNullOrEmpty(ns))
                    usings.AppendLine($"using {ns};");
            var fullClassName = classDeclaration.Identifier.Text;
            var constructorName = classDeclaration.Identifier.Text;

            // Handle generic classes
            var constraintClauses = "";
            if (classDeclaration.TypeParameterList != null)
            {
                var typeParameters = classDeclaration.TypeParameterList.Parameters
                    .Select(param => param.Identifier.Text);
                fullClassName += $"<{string.Join(", ", typeParameters)}>";
                // NOTE: Constructor name should NOT include type parameters - only class declaration should

                // Extract generic constraints if they exist
                if (classDeclaration.ConstraintClauses.Any())
                {
                    var constraints = classDeclaration.ConstraintClauses
                        .Select(clause => clause.ToString().Trim());
                    constraintClauses = $"\n    {string.Join("\n    ", constraints)}";
                }
            }

            // Get existing field names from the class symbol (includes all partial declarations)
            var currentClassSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
            var existingFieldNames = new HashSet<string>();
            if (currentClassSymbol != null)
            {
                // Check for ALL fields, not just [Inject] fields, because [DependsOn] fields 
                // might already be generated in other partial declarations
                var allFields = currentClassSymbol.GetMembers().OfType<IFieldSymbol>();

                foreach (var field in allFields)
                    existingFieldNames.Add(field.Name);
            }

            // Check if a constructor with the same signature already exists
            var allHierarchyDependencies = hierarchyDependencies.AllDependencies ??
                                           new List<(ITypeSymbol, string, DependencySource)>();
            var parameterTypes = allHierarchyDependencies.Select(d => d.ServiceType).ToList();

            var derivedClassConstructors = currentClassSymbol?.GetMembers().OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Constructor && !m.IsStatic)
                .ToList() ?? new List<IMethodSymbol>();

            foreach (var existingCtor in derivedClassConstructors)
                if (existingCtor.Parameters.Length == parameterTypes.Count)
                {
                    var signaturesMatch = true;
                    for (var i = 0; i < parameterTypes.Count; i++)
                        if (!SymbolEqualityComparer.Default.Equals(existingCtor.Parameters[i].Type, parameterTypes[i]))
                        {
                            signaturesMatch = false;
                            break;
                        }

                    if (signaturesMatch)
                    {
                        // Skip if this is an implicit (compiler-generated) default constructor
                        // We want to generate explicit constructors for services
                        if (existingCtor.IsImplicitlyDeclared)
                            continue;

                        // Constructor with same signature already exists, don't generate
                        return "";
                    }
                }

            // Only generate fields for dependencies not already declared
            // CRITICAL FIX: Also check if field has [InjectConfiguration] attribute - these fields already exist in source
            var configFieldNames = new HashSet<string>();
            foreach (var configField in configFields) configFieldNames.Add(configField.FieldName);

            // CRITICAL FIX: Use deduplicated AllDependencies instead of RawAllDependencies to prevent duplicate fields
            // This ensures duplicate [DependsOn<T>] attributes only generate one field
            var allLevelZeroDependencies = hierarchyDependencies.DerivedDependencies ??
                                           new List<(ITypeSymbol, string, DependencySource)>();

            // Apply unified field generation logic
            var fieldsToGenerate = allLevelZeroDependencies
                .Where(f => ShouldGenerateField(f, existingFieldNames, configFieldNames))
                .ToList();

            // CRITICAL FIX: Handle collision between [DependsOn] and [Inject] for same ServiceType in field generation
            // If both exist for same ServiceType, don't generate DependsOn field when Inject field exists
            fieldsToGenerate = ResolveInjectDependsOnCollisions(fieldsToGenerate);

            // CRITICAL FIX: Determine if fields should be protected for inheritance scenarios
            var accessModifier = ShouldUseProtectedFields(classDeclaration, classSymbol) ? "protected" : "private";

            var fieldDeclarations = fieldsToGenerate.Select(d =>
                $"{accessModifier} readonly {RemoveNamespacesAndDots(d.ServiceType, namespacesForStripping)} {d.FieldName};");

            var fieldsStr = string.Join("\n    ", fieldDeclarations);

            var generatedConfigFields = configFields.Where(f => f.GeneratedField).ToList();
            if (generatedConfigFields.Any())
            {
                var configFieldDeclarations = generatedConfigFields.Select(f =>
                    $"{accessModifier} readonly {RemoveNamespacesAndDots(f.FieldType, namespacesForStripping)} {f.FieldName};");
                var configFieldsStr = string.Join("\n    ", configFieldDeclarations);
                fieldsStr = string.IsNullOrWhiteSpace(fieldsStr)
                    ? configFieldsStr
                    : $"{fieldsStr}\n    {configFieldsStr}";
            }

            // CRITICAL FIX: Use AllDependencies which already has correct inheritance ordering
            // DependencyAnalyzer already provides dependencies ordered by level and source type
            // DO NOT re-group - preserve the inheritance-aware ordering from DependencyAnalyzer
            var constructorDependencies = hierarchyDependencies.AllDependencies ??
                                          new List<(ITypeSymbol, string, DependencySource)>();

            // CRITICAL FIX: Filter out individual configuration field dependencies from constructor parameters
            // Only keep the IConfiguration parameter itself - individual config fields are handled via binding in constructor body
            var allDependencies = constructorDependencies
                .Where(d => d.Source != DependencySource.ConfigurationInjection || d.FieldName == "_configuration")
                .ToList();


            // CRITICAL FIX: Handle collision between [DependsOn] and [Inject] for same ServiceType
            // If both exist for same ServiceType, prefer [Inject] field over [DependsOn] field
            allDependencies = ResolveInjectDependsOnCollisions(allDependencies);

            // Generate unique parameter names to avoid CS0100 duplicate parameter errors
            var parameterNames = new HashSet<string>();
            var parametersWithNames =
                new List<(string TypeString, string ParamName, (ITypeSymbol ServiceType, string FieldName,
                    DependencySource Source) Dependency)>();

            foreach (var f in allDependencies)
            {
                var baseParamName = GetParameterNameFromFieldName(f.FieldName);

                var paramName = baseParamName;
                var counter = 1;
                while (parameterNames.Contains(paramName))
                {
                    paramName = $"{baseParamName}{counter}";
                    counter++;
                }

                parameterNames.Add(paramName);

                var typeString = GetTypeStringWithNullableAnnotation(f.ServiceType, f.FieldName, classSymbol,
                    namespacesForStripping);
                parametersWithNames.Add((typeString, paramName, f));
            }

            // CRITICAL FIX: Check if base class requires parameters we cannot provide
            // If so, skip constructor generation - user must provide their own
            if (BaseConstructorCallBuilder.ShouldSkipConstructorGeneration(classSymbol?.BaseType))
                return "";

            // Generate base constructor call
            var baseCallStr = GenerateBaseConstructorCall(classSymbol?.BaseType, parametersWithNames, semanticModel, classSymbol, autoDepsOptions);

            // Regenerate parameter string
            var allParameters = parametersWithNames.Select(p => $"{p.TypeString} {p.ParamName}");
            var parameterStr = parametersWithNames.Count <= 3
                ? string.Join(", ", allParameters)
                : string.Join(",\n        ", allParameters);

            // Generate assignments ONLY for derived dependencies (fields that exist in current class)
            // Base class dependencies are passed to base constructor, not assigned directly
            var derivedFieldNames = new HashSet<string>(
                (hierarchyDependencies.DerivedDependencies ?? new List<(ITypeSymbol, string, DependencySource)>())
                .Select(d => d.FieldName));

            // Create mapping from ServiceType to parameter name for field assignments
            // This handles the case where multiple fields of the same ServiceType map to one parameter
            var serviceTypeToParamName = parametersWithNames
                .ToDictionary(p => p.Dependency.ServiceType, p => p.ParamName, SymbolEqualityComparer.Default);

            // Generate assignments for ALL derived dependencies using the ServiceType mapping
            // Skip primitive SupportsReloading fields - they're handled in config assignments section
            var regularAssignments = hierarchyDependencies.DerivedDependencies
                .Where(d => d.Source != DependencySource.ConfigurationInjection ||
                            IsOptionsPatternOrConfigObjectAssignment(d.FieldName, classSymbol, semanticModel))
                .Where(d => !IsPrimitiveSupportsReloadingField(d.FieldName, classSymbol, semanticModel))
                .Where(d => serviceTypeToParamName.ContainsKey(d.ServiceType)) // Ensure parameter exists
                .Select(d =>
                {
                    var paramName = serviceTypeToParamName[d.ServiceType];

                    // Check if this is a SupportsReloading field that uses Options pattern (complex objects only)
                    if (d.Source == DependencySource.Inject &&
                        IsSupportsReloadingFieldWithOptionsPattern(d.FieldName, classSymbol, semanticModel))
                        return $"this.{d.FieldName} = {paramName}.Value;";
                    return $"this.{d.FieldName} = {paramName};";
                });

            // Add configuration injection assignments
            // Find IConfiguration parameter name
            var iConfigurationType =
                semanticModel.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");
            var configurationParameterName = "configuration"; // default fallback
            if (iConfigurationType != null)
            {
                var configParam = parametersWithNames.FirstOrDefault(p =>
                    SymbolEqualityComparer.Default.Equals(p.Dependency.ServiceType, iConfigurationType));
                if (!string.IsNullOrEmpty(configParam.ParamName))
                    configurationParameterName = configParam.ParamName;
            }

            var configAssignments =
                GenerateConfigurationAssignments(classSymbol, semanticModel, namespacesForStripping,
                    configurationParameterName);

            var allAssignments = regularAssignments.Concat(configAssignments);
            var assignmentStr = string.Join("\n        ", allAssignments);

            // Detect if this is a file-scoped namespace
            var isFileScopedNamespace = false;
            var namespaceParent = classDeclaration.Parent;
            while (namespaceParent != null && namespaceParent is not BaseNamespaceDeclarationSyntax)
                namespaceParent = namespaceParent.Parent;

            if (namespaceParent is FileScopedNamespaceDeclarationSyntax)
                isFileScopedNamespace = true;

            var namespaceDeclaration = string.IsNullOrEmpty(namespaceName) ? "" : $"namespace {namespaceName};";
            var beforeUsings = isFileScopedNamespace ? namespaceDeclaration : "";
            var afterUsings = !isFileScopedNamespace ? namespaceDeclaration : "";

            var isNestedClass = classDeclaration.Parent is TypeDeclarationSyntax;

            var openingBraces = "";
            var closingBraces = "";
            if (isNestedClass)
            {
                var containingClasses =
                    new List<(TypeDeclarationSyntax syntax, string declaration, string accessibility)>();
                var current = classDeclaration.Parent;
                while (current is TypeDeclarationSyntax parentClass)
                {
                    var parentAccessibility = parentClass.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))
                        ? "public"
                        : parentClass.Modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword))
                            ? "internal"
                            : parentClass.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword))
                                ? "protected"
                                : parentClass.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))
                                    ? "private"
                                    : "public";

                    var parentKeyword = GetTypeDeclarationKeyword(parentClass);
                    containingClasses.Add((parentClass,
                        $"{parentAccessibility} partial {parentKeyword} {parentClass.Identifier.Text}",
                        parentAccessibility));
                    current = current.Parent;
                }

                containingClasses.Reverse();
                openingBraces = string.Join("\n", containingClasses.Select(c => $"{c.declaration}\n{{"));
                closingBraces = string.Join("\n", Enumerable.Range(0, containingClasses.Count).Select(_ => "}"));
            }

            return RenderConstructorSource(
                beforeUsings,
                usings.ToString(),
                afterUsings,
                isNestedClass,
                openingBraces,
                closingBraces,
                accessibilityModifier,
                typeKeyword,
                fullClassName,
                constraintClauses,
                fieldsStr,
                parameterStr,
                baseCallStr,
                constructorName,
                assignmentStr);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Re-throw with OOM/SOF filter per D-07/D-08. The caller (ConstructorEmitter.cs)
            // has SourceProductionContext and emits IOC995/IOC992 diagnostics in its catch handlers.
            // This ensures the error surfaces in build output rather than being silently swallowed.
            throw;
        }
    }

    // Helper methods needed by the constructor generator


    private static bool HasInjectFields(InheritanceHierarchyDependencies hierarchyDependencies)
    {
        // CRITICAL FIX: Check RawAllDependencies instead of AllDependencies
        // AllDependencies may be modified by GetConstructorDependencies deduplication logic
        // but RawAllDependencies always contains the original, unprocessed dependencies
        return hierarchyDependencies.RawAllDependencies?.Any(d => d.Source == DependencySource.Inject) ?? false;
    }


    /// <summary>
    ///     Determines if DependsOn fields should be protected instead of private to allow inheritance access
    /// </summary>
    private static bool ShouldUseProtectedFields(TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol? classSymbol)
    {
        if (classSymbol == null)
            return false;

        // CRITICAL FIX: Use protected fields ONLY for abstract classes
        // Abstract classes are explicitly designed to be inherited and need protected access
        // for derived classes to access the generated DependsOn fields
        return classSymbol.IsAbstract;
    }

    /// <summary>
    ///     Resolves collisions between [Inject] and [DependsOn] for the same ServiceType.
    ///     When both exist for the same type, prefers [Inject] over [DependsOn] since [Inject]
    ///     fields typically already exist in source code.
    /// </summary>
    private static List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>
        ResolveInjectDependsOnCollisions(
            List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> dependencies)
    {
        var result = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();
        var dependenciesByServiceType = dependencies.GroupBy(d => d.ServiceType, SymbolEqualityComparer.Default);

        foreach (var group in dependenciesByServiceType)
        {
            var dependenciesForType = group.ToList();
            var injectItems = dependenciesForType.Where(d => d.Source == DependencySource.Inject).ToList();
            var dependsOnItems = dependenciesForType.Where(d => d.Source == DependencySource.DependsOn).ToList();

            if (injectItems.Any() && dependsOnItems.Any())
            {
                // COLLISION SCENARIO: Both [Inject] and [DependsOn] exist for same ServiceType
                // Prefer [Inject] - it represents an existing field in source code that should take precedence
                result.AddRange(injectItems);

                // Add any other dependencies that aren't part of the collision
                var otherItems = dependenciesForType.Where(d =>
                    d.Source != DependencySource.Inject && d.Source != DependencySource.DependsOn);
                result.AddRange(otherItems);
            }
            else
            {
                // Normal case: no collision, add all dependencies for this type
                result.AddRange(dependenciesForType);
            }
        }

        return result;
    }

    private static string GenerateBaseConstructorCall(
        INamedTypeSymbol? baseClass,
        List<(string TypeString, string ParamName, (ITypeSymbol ServiceType, string FieldName, DependencySource Source) Dependency)> parametersWithNames,
        SemanticModel semanticModel,
        INamedTypeSymbol? currentClassSymbol,
        IReadOnlyDictionary<string, string>? autoDepsOptions)
    {
        return BaseConstructorCallBuilder.Build(baseClass, parametersWithNames, semanticModel, currentClassSymbol, autoDepsOptions);
    }

    internal static INamedTypeSymbol? ResolveDeclaredClassSymbol(
        TypeDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        Action<Diagnostic> reportDiagnostic)
    {
        try
        {
            return semanticModel.GetDeclaredSymbol(classDeclaration);
        }
        catch (ArgumentException)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ServiceAnalysisFailure,
                classDeclaration.Identifier.GetLocation(),
                classDeclaration.Identifier.Text,
                nameof(ConstructorGenerator)));
            return null;
        }
    }

    private static bool ShouldGenerateField(
        (ITypeSymbol ServiceType, string FieldName, DependencySource Source) dep,
        HashSet<string> existingFieldNames,
        HashSet<string> configFieldNames)
    {
        // Configuration Injection is only a constructor parameter, not a stored field
        if (dep.Source == DependencySource.ConfigurationInjection)
            return false;

        // Config fields with [InjectConfiguration] are defined by user attributes
        if (configFieldNames.Contains(dep.FieldName))
            return false;

        // For ALL other dependencies (Inject AND DependsOn), skip if the name
        // is already taken by a manual field or generated in another partial
        return !existingFieldNames.Contains(dep.FieldName);
    }
}
