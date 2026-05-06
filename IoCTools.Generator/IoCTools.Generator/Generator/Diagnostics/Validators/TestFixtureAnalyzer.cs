namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System.Collections.Immutable;

using IoCTools.Generator.Diagnostics;
using IoCTools.Generator.Diagnostics.Helpers;
using IoCTools.Generator.Models;
using IoCTools.Generator.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class TestFixtureAnalyzer
{
    /// <summary>
    /// Validates test fixture scenarios and emits appropriate diagnostics.
    /// Analyzes test classes for manual mocks, Cover{T} usage, and fixture opportunities.
    /// </summary>
    public static void Validate(
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic,
        DiagnosticConfiguration config)
    {
        if (!config.DiagnosticsEnabled) return;

        var testClasses = FindTestClasses(compilation);
        var coverTestClasses = FindCoverTestClasses(compilation);
        var fixturesByTestClass = new Dictionary<INamedTypeSymbol, CoverClassInfo>(SymbolEqualityComparer.Default);
        foreach (var coverClass in coverTestClasses.Where(c => c.ServiceSymbol != null))
        {
            fixturesByTestClass[coverClass.Symbol] = coverClass;
        }

        // Emit TDIAG-04 and TDIAG-05 for Cover<T> usage
        foreach (var coverClass in coverTestClasses)
        {
            EmitCoverDiagnostics(coverClass, compilation, reportDiagnostic);
        }

        foreach (var testClass in testClasses)
        {
            AnalyzeTestClass(testClass, fixturesByTestClass, compilation, reportDiagnostic, config);
        }
    }

    private static ImmutableArray<INamedTypeSymbol> FindTestClasses(Compilation compilation)
    {
        var testClasses = new List<INamedTypeSymbol>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var typeDecls = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in typeDecls)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
                if (symbol == null) continue;

                if (IsTestClass(symbol))
                {
                    testClasses.Add(symbol);
                }
            }
        }

        return testClasses.ToImmutableArray();
    }

    /// <summary>
    /// Finds all test classes that use [Cover<T>] attribute.
    /// </summary>
    private static ImmutableArray<CoverClassInfo> FindCoverTestClasses(Compilation compilation)
    {
        var results = new List<CoverClassInfo>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var typeDecls = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in typeDecls)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (symbol == null) continue;

                var coverAttr = symbol.GetAttributes()
                    .FirstOrDefault(a =>
                        a.AttributeClass?.Name == "CoverAttribute" &&
                        a.AttributeClass?.ToDisplayString().Contains("IoCTools.Testing") == true);

                if (coverAttr == null) continue;

                // Extract TService
                INamedTypeSymbol? serviceSymbol = null;
                if (coverAttr.AttributeClass is INamedTypeSymbol namedAttr &&
                    namedAttr.TypeArguments.Length > 0 &&
                    namedAttr.TypeArguments[0] is INamedTypeSymbol serviceSym)
                {
                    serviceSymbol = serviceSym;
                }

                results.Add(new CoverClassInfo(symbol, typeDecl, serviceSymbol, UsesNullLogger(coverAttr)));
            }
        }

        return results.ToImmutableArray();
    }

    private static void EmitCoverDiagnostics(
        CoverClassInfo coverClass,
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic)
    {
        // TDIAG-05: Cover<T> class is not partial
        if (!coverClass.Declaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TestClassNotPartial,
                coverClass.Symbol.Locations.FirstOrDefault(),
                coverClass.Symbol.Name,
                coverClass.ServiceSymbol?.Name ?? "?"));
            return; // No point checking TDIAG-04 if class isn't partial
        }

        // TDIAG-04: Cover<T> service has no generated constructor
        if (coverClass.ServiceSymbol != null)
        {
            var hasGeneratedConstructor = ServiceHasGeneratedConstructor(coverClass.ServiceSymbol, compilation);
            if (!hasGeneratedConstructor)
            {
                reportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ServiceMissingConstructor,
                    coverClass.Symbol.Locations.FirstOrDefault(),
                    coverClass.Symbol.Name,
                    coverClass.ServiceSymbol.Name));
            }

            // TDIAG-06: Check for fixture member name collisions
            EmitCollisionDiagnostics(coverClass, reportDiagnostic);
        }
    }

    /// <summary>
    /// Determines if a service has a generated constructor.
    /// A service has a generated constructor if:
    /// - It is marked partial
    /// - Has a lifetime attribute ([Scoped], [Singleton], [Transient])
    /// - Has at least one [DependsOn], [DependsOnConfiguration], [DependsOnOptions], or [Inject]
    /// </summary>
    private static bool ServiceHasGeneratedConstructor(INamedTypeSymbol service, Compilation compilation)
    {
        if (service.DeclaringSyntaxReferences.Length == 0)
            return MetadataServiceHasGeneratedConstructorShape(service);

        // Check if the service itself is partial (find its declaration)
        var isPartial = false;
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var typeDecls = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();
            foreach (var typeDecl in typeDecls)
            {
                var declSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (declSymbol != null && SymbolEqualityComparer.Default.Equals(declSymbol, service))
                {
                    if (typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    {
                        isPartial = true;
                    }
                    break;
                }
            }
            if (isPartial) break;
        }

        if (!isPartial) return false;

        return HasConstructorGenerationIntent(service);
    }

    private static bool MetadataServiceHasGeneratedConstructorShape(INamedTypeSymbol service)
    {
        if (!HasConstructorGenerationIntent(service))
            return false;

        var dependencyTypes = GetServiceDependencyTypes(service);
        return service.Constructors.Any(ctor =>
            !ctor.IsStatic &&
            !ctor.IsImplicitlyDeclared &&
            ctor.Parameters.Length > 0 &&
            (dependencyTypes.IsEmpty || ctor.Parameters.Length >= dependencyTypes.Length));
    }

    private static bool IsTestClass(INamedTypeSymbol symbol)
    {
        var containingAssembly = symbol.ContainingAssembly?.Name ?? string.Empty;

        if (containingAssembly.EndsWith(".Tests", StringComparison.Ordinal) ||
            containingAssembly.Contains(".Test"))
            return true;

        if (symbol.Name.EndsWith("Tests", StringComparison.Ordinal) ||
            symbol.Name.EndsWith("Test", StringComparison.Ordinal))
            return true;

        if (symbol.GetMembers().Any(m =>
            m.GetAttributes().Any(a =>
                a.AttributeClass?.Name.Contains("Fact") == true ||
                a.AttributeClass?.Name.Contains("Test") == true)))
            return true;

        // Also treat any class with [Cover<T>] as a test class
        if (symbol.GetAttributes().Any(a =>
                a.AttributeClass?.Name == "CoverAttribute" &&
                a.AttributeClass?.ToDisplayString().Contains("IoCTools.Testing") == true))
            return true;

        return false;
    }

    private static ImmutableDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> FindServicesWithFixtures(
        Compilation compilation)
    {
        var result = new Dictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var typeDecls = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in typeDecls)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (symbol == null) continue;

                var coverAttr = symbol.GetAttributes()
                    .FirstOrDefault(a =>
                        a.AttributeClass?.Name == "CoverAttribute" &&
                        a.AttributeClass?.ToDisplayString().Contains("IoCTools.Testing") == true);

                if (coverAttr != null &&
                    coverAttr.AttributeClass is INamedTypeSymbol namedAttr &&
                    namedAttr.TypeArguments.Length > 0 &&
                    namedAttr.TypeArguments[0] is INamedTypeSymbol serviceSymbol)
                {
                    result[symbol] = ImmutableArray.Create(serviceSymbol);
                }
            }
        }

        return result.ToImmutableDictionary(SymbolEqualityComparer.Default);
    }

    private static void AnalyzeTestClass(
        INamedTypeSymbol testClass,
        Dictionary<INamedTypeSymbol, CoverClassInfo> fixturesByTestClass,
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic,
        DiagnosticConfiguration config)
    {
        if (fixturesByTestClass.TryGetValue(testClass, out var coverClass) &&
            coverClass.ServiceSymbol != null)
        {
            AnalyzeFixtureUsage(testClass, coverClass.ServiceSymbol, coverClass, compilation, reportDiagnostic);
        }

        if (!fixturesByTestClass.ContainsKey(testClass))
        {
            AnalyzeFixtureOpportunity(testClass, compilation, reportDiagnostic);
            // TDIAG-08: Check for manual construction of IoCTools-owned services
            EmitCoverOpportunityDiagnostics(testClass, compilation, reportDiagnostic, config);
        }
    }

    private static void AnalyzeFixtureUsage(
        INamedTypeSymbol testClass,
        INamedTypeSymbol service,
        CoverClassInfo coverClass,
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic)
    {
        // Get the service's constructor dependencies
        var serviceDeps = GetServiceDependencyTypes(service);
        var serviceDepNames = GetDependsOnDependencyDisplayNames(service);

        // TDIAG-01: Only report manual Mock<T> fields that match service dependencies
        var mockFields = testClass.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => IsMoqMock(f.Type));

        foreach (var mockField in mockFields)
        {
            var mockedType = ((INamedTypeSymbol)mockField.Type).TypeArguments[0];

            // Only report if this mock type matches a service dependency
            if (DependencyMatches(serviceDeps, serviceDepNames, mockedType) &&
                GeneratesMockDependency(coverClass, mockedType))
            {
                reportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ManualMockField,
                    mockField.Locations.FirstOrDefault(),
                    testClass.Name,
                    mockedType.Name,
                    service.Name));
            }
        }

        // TDIAG-02: Detect manual service construction in method bodies,
        // property accessors, local functions, and field/property initializers.
        // Covers both explicit new Service(...) and target-typed new(...) expressions.
        foreach (var member in testClass.GetMembers())
        {
            // Find the declaration node for this member from the syntax tree
            var syntaxRef = member.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null)
                continue;

            var syntaxNode = syntaxRef.GetSyntax();
            if (syntaxNode == null)
                continue;

            var syntaxTree = syntaxNode.SyntaxTree;
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Traverse all descendant object creation expressions (explicit new Type(...))
            foreach (var objectCreation in syntaxNode.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var typeSymbol = semanticModel.GetTypeInfo(objectCreation).Type;
                if (typeSymbol != null &&
                    SymbolEqualityComparer.Default.Equals(typeSymbol, service))
                {
                    reportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ManualSutConstruction,
                        objectCreation.GetLocation(),
                        testClass.Name,
                        service.Name));
                }
            }

            // TDIAG-02: Also detect target-typed new(...) expressions (C# 9+)
            foreach (var implicitCreation in syntaxNode.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
            {
                var typeSymbol = semanticModel.GetTypeInfo(implicitCreation).Type;
                if (typeSymbol != null &&
                    SymbolEqualityComparer.Default.Equals(typeSymbol, service))
                {
                    reportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ManualSutConstruction,
                        implicitCreation.GetLocation(),
                        testClass.Name,
                        service.Name));
                }
            }

            // TDIAG-07: Detect fixture helper calls (Setup*, Configure*, Use*) after Sut access
            // Only works on method-like members with a body (methods, local functions, constructors)
            if (member is IMethodSymbol)
            {
                EmitSetupAfterSutDiagnostics(syntaxNode, semanticModel, testClass.Name, member.Name, reportDiagnostic);
            }
        }
    }

    /// <summary>
    /// TDIAG-07: Detects fixture helper calls (Setup*, Configure*, Use*) that appear
    /// after a Sut property access in the same method body.
    /// </summary>
    private static void EmitSetupAfterSutDiagnostics(
        SyntaxNode memberSyntax,
        SemanticModel semanticModel,
        string testClassName,
        string memberName,
        Action<Diagnostic> reportDiagnostic)
    {
        // The memberSyntax is the declaring syntax for the member itself.
        // A method member may be a MethodDeclarationSyntax (which IS a BaseMethodDeclarationSyntax).
        // We treat the member syntax itself as the method if it has a body.
        if (memberSyntax is not BaseMethodDeclarationSyntax methodDecl)
            return;

        if (methodDecl.Body == null)
            return;

        var bodyStatements = methodDecl.Body.Statements;

        // Find Sut identifier accesses within the method body
        var sutAccessNodes = methodDecl.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.Text == "Sut")
            .ToList();

        if (sutAccessNodes.Count == 0)
            return;

        // Find the earliest statement containing a Sut access
        var earliestSutStatement = GetContainingStatement(bodyStatements, sutAccessNodes.First());
        if (earliestSutStatement == null)
            return;

        var earliestSutLine = earliestSutStatement.GetLocation().GetLineSpan().StartLinePosition.Line;

        // Find all fixture helper invocations (Setup*, Configure*, Use*) that appear
        // on lines after the earliest Sut access
        var helperCalls = methodDecl.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(inv => (Syntax: inv, Name: GetFixtureHelperInvocationName(inv)))
            .Where(x => IsGeneratedFixtureHelperCandidate(x.Syntax, x.Name, semanticModel))
            .ToList();

        foreach (var helperCall in helperCalls)
        {
            var helperLine = helperCall.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (helperLine > earliestSutLine)
            {
                reportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.SetupAfterSutAccess,
                    helperCall.Syntax.GetLocation(),
                    helperCall.Name ?? "?",
                    memberName));
            }
        }
    }

    /// <summary>
    /// Gets the name of a possible generated fixture helper invocation.
    /// Deliberately ignores arbitrary member access like SomeMock.Setup(...).
    /// </summary>
    private static string? GetFixtureHelperInvocationName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => name.Identifier.Text,
            _ => null
        };
    }

    private static bool IsGeneratedFixtureHelperCandidate(
        InvocationExpressionSyntax invocation,
        string? invocationName,
        SemanticModel semanticModel)
    {
        if (invocationName == null || !HasGeneratedHelperPrefix(invocationName))
            return false;

        var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol == null)
            return true;

        // User-declared helpers are semantic harness code, not generated fixture helpers.
        return symbol.DeclaringSyntaxReferences.Length == 0;
    }

    private static bool HasGeneratedHelperPrefix(string name)
    {
        return HasPrefixWithSuffix(name, "Setup") ||
            HasPrefixWithSuffix(name, "Configure") ||
            HasPrefixWithSuffix(name, "Use");
    }

    private static bool HasPrefixWithSuffix(string name, string prefix)
    {
        return name.Length > prefix.Length &&
            name.StartsWith(prefix, StringComparison.Ordinal) &&
            char.IsUpper(name[prefix.Length]);
    }

    /// <summary>
    /// Finds the containing statement for a given syntax node within a list of statements.
    /// Uses span containment: a statement contains the node if the node's span falls within
    /// the statement's span.
    /// </summary>
    private static StatementSyntax? GetContainingStatement(SyntaxList<StatementSyntax> statements, SyntaxNode node)
    {
        foreach (var statement in statements)
        {
            if (statement.Span.Contains(node.Span))
                return statement;

            // Check nested blocks recursively (blocks inside if, foreach, using, etc.)
            var nestedBlocks = statement.DescendantNodes().OfType<BlockSyntax>();
            foreach (var block in nestedBlocks)
            {
                var result = GetContainingStatement(block.Statements, node);
                if (result != null)
                    return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the dependency types from a service's constructor parameters.
    /// </summary>
    private static ImmutableArray<ITypeSymbol> GetServiceDependencyTypes(INamedTypeSymbol service)
    {
        var declaredDependencies = GetDependsOnDependencyTypes(service);
        if (!declaredDependencies.IsEmpty)
            return declaredDependencies;

        var constructor = service.Constructors
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault(c => !c.IsStatic);

        if (constructor == null)
            return ImmutableArray<ITypeSymbol>.Empty;

        return constructor.Parameters
            .Select(p => p.Type)
            .ToImmutableArray();
    }

    private static ImmutableArray<ITypeSymbol> GetDependsOnDependencyTypes(INamedTypeSymbol service)
    {
        var dependencies = new List<ITypeSymbol>();
        foreach (var attribute in service.GetAttributes().Where(AttributeTypeChecker.IsDependsOnAttribute))
        {
            if (attribute.AttributeClass is INamedTypeSymbol attributeType)
            {
                dependencies.AddRange(attributeType.TypeArguments);
            }

            foreach (var constructorArgument in attribute.ConstructorArguments)
            {
                AddTypedConstantDependency(constructorArgument, dependencies);
            }
        }

        return dependencies
            .Distinct<ITypeSymbol>(SymbolEqualityComparer.Default)
            .ToImmutableArray();
    }

    private static ImmutableArray<string> GetDependsOnDependencyDisplayNames(INamedTypeSymbol service)
    {
        var dependencyNames = new List<string>();
        foreach (var attribute in service.GetAttributes().Where(AttributeTypeChecker.IsDependsOnAttribute))
        {
            if (attribute.AttributeClass == null) continue;
            var displayName = attribute.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            dependencyNames.AddRange(ExtractGenericTypeArguments(displayName));
        }

        return dependencyNames
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static IEnumerable<string> ExtractGenericTypeArguments(string typeName)
    {
        var start = typeName.IndexOf('<');
        var end = typeName.LastIndexOf('>');
        if (start < 0 || end <= start) yield break;

        foreach (var part in SplitTopLevelTypeArguments(typeName.Substring(start + 1, end - start - 1)))
        {
            var normalized = part.Trim();
            if (normalized.Length > 0)
                yield return normalized;
        }
    }

    private static IEnumerable<string> SplitTopLevelTypeArguments(string typeArguments)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < typeArguments.Length; i++)
        {
            switch (typeArguments[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return typeArguments.Substring(start, i - start);
                    start = i + 1;
                    break;
            }
        }

        yield return typeArguments.Substring(start);
    }

    private static bool DependencyMatches(
        ImmutableArray<ITypeSymbol> dependencyTypes,
        ImmutableArray<string> dependencyDisplayNames,
        ITypeSymbol mockedType)
    {
        if (dependencyTypes.Any(d => SymbolEqualityComparer.Default.Equals(d, mockedType)))
            return true;

        var mockedTypeName = mockedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return dependencyDisplayNames.Any(d => string.Equals(d, mockedTypeName, StringComparison.Ordinal));
    }

    private static void AddTypedConstantDependency(
        TypedConstant value,
        List<ITypeSymbol> dependencies)
    {
        // Guard: Array TypedConstants do not support .Value — calling .Value on an Array
        // TypedConstant throws "TypedConstant is an array. Use Values property." This occurs
        // when a params Type[] attribute argument is stored as a true array (multiple args).
        // Process array elements recursively and return early to avoid the .Value access below.
        if (value.Kind == TypedConstantKind.Array)
        {
            foreach (var nested in value.Values)
            {
                AddTypedConstantDependency(nested, dependencies);
            }
            return;
        }

        // Guard: scalar TypedConstants (Primitive, Enum, Error) with a non-type value are not
        // ITypeSymbol refs — skip them. Only Type-kinded constants carry an ITypeSymbol in .Value.
        if (value.Value is ITypeSymbol type)
        {
            dependencies.Add(type);
        }
    }

    private static void AnalyzeFixtureOpportunity(
        INamedTypeSymbol testClass,
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic)
    {
        var mockFields = testClass.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => IsMoqMock(f.Type))
            .ToImmutableArray();

        if (mockFields.IsEmpty) return;

        var mockedTypesSet = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var mockField in mockFields)
        {
            mockedTypesSet.Add(((INamedTypeSymbol)mockField.Type).TypeArguments[0]);
        }

        // Find strong single matches first
        var matches = new List<INamedTypeSymbol>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var typeDecls = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in typeDecls)
            {
                var serviceSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (serviceSymbol == null) continue;
                if (SymbolEqualityComparer.Default.Equals(serviceSymbol, testClass)) continue;
                if (IsTestClass(serviceSymbol)) continue;
                if (!IsIoCToolsManagedService(serviceSymbol, compilation)) continue;

                if (ServiceDependenciesMatch(serviceSymbol, mockedTypesSet))
                {
                    matches.Add(serviceSymbol);
                }
            }
        }

        // TDIAG-03: Report only strong single matches; skip ambiguity
        if (matches.Count == 1)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.CouldUseFixture,
                testClass.Locations.FirstOrDefault(),
                testClass.Name,
                mockFields.First().Type.Name,
                matches[0].Name));
        }


    }

    /// <summary>
    /// TDIAG-08: Detects test classes that manually construct IoCTools-owned service types
    /// and could benefit from [Cover&lt;T&gt;] attribute.
    /// </summary>
    private static void EmitCoverOpportunityDiagnostics(
        INamedTypeSymbol testClass,
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic,
        DiagnosticConfiguration config)
    {
        // Skip if already has [Cover<T>] (we only get here for non-Cover classes, but double-check)
        var hasCover = testClass.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "CoverAttribute" &&
            a.AttributeClass?.ToDisplayString().Contains("IoCTools.Testing") == true);
        if (hasCover)
            return;

        // Find all object creation expressions in the test class
        var constructedServices = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var member in testClass.GetMembers())
        {
            var syntaxRef = member.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null) continue;

            var syntaxNode = syntaxRef.GetSyntax();
            if (syntaxNode == null) continue;

            var syntaxTree = syntaxNode.SyntaxTree;
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Check explicit new Type(...)
            foreach (var objectCreation in syntaxNode.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var createdType = semanticModel.GetTypeInfo(objectCreation).Type as INamedTypeSymbol;
                if (createdType != null &&
                    !IsTestClass(createdType) &&
                    IsIoCToolsManagedService(createdType, compilation))
                {
                    constructedServices.Add(createdType);
                }
            }

            // Check target-typed new(...)
            foreach (var implicitCreation in syntaxNode.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
            {
                var createdType = semanticModel.GetTypeInfo(implicitCreation).Type as INamedTypeSymbol;
                if (createdType != null &&
                    !IsTestClass(createdType) &&
                    IsIoCToolsManagedService(createdType, compilation))
                {
                    constructedServices.Add(createdType);
                }
            }
        }

        // Emit TDIAG-08 for each distinct IoCTools-managed service constructed manually
        var tdiag08Descriptor = DiagnosticDescriptorFactory.WithSeverity(
            DiagnosticDescriptors.CouldUseCoverAttribute,
            config.TestingDiagnosticSeverity);
        foreach (var service in constructedServices)
        {
            reportDiagnostic(Diagnostic.Create(
                tdiag08Descriptor,
                testClass.Locations.FirstOrDefault(),
                testClass.Name,
                service.Name));
        }
    }

    /// <summary>
    /// Determines if a type is an IoCTools-managed service (has lifetime + dependency attributes
    /// and is partial, meaning a constructor would be generated for it).
    /// </summary>
    private static bool IsIoCToolsManagedService(INamedTypeSymbol type, Compilation compilation)
    {
        return ServiceHasGeneratedConstructor(type, compilation);
    }

    private static bool ServiceDependenciesMatch(
        INamedTypeSymbol service,
        HashSet<ITypeSymbol> mockedTypes)
    {
        var dependencyTypes = GetServiceDependencyTypes(service);
        var dependencyTypeNames = GetDependsOnDependencyDisplayNames(service);
        if (dependencyTypes.IsEmpty && dependencyTypeNames.IsEmpty) return false;

        return dependencyTypes.All(mockedTypes.Contains) &&
               dependencyTypeNames.All(depName => mockedTypes.Any(mockedType =>
                   string.Equals(
                       depName,
                       mockedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                       StringComparison.Ordinal)));
    }

    private static bool HasConstructorGenerationIntent(INamedTypeSymbol service)
    {
        if (service.GetAttributes().Any(AttributeTypeChecker.IsDependsOnAttribute)) return true;
        if (service.GetAttributes().Any(AttributeTypeChecker.IsRegisterAsAttribute)) return true;
        if (service.GetAttributes().Any(a => AttributeTypeChecker.IsAttribute(a, AttributeTypeChecker.RegisterAsAllAttribute))) return true;
        if (service.GetAttributes().Any(a => AttributeTypeChecker.IsAttribute(a, AttributeTypeChecker.ConditionalServiceAttribute))) return true;
        if (ServiceDiscovery.GetLifetimeAttributes(service).HasAny) return true;
        if (ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(service)) return true;
        if (ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(service)) return true;
        if (ServiceDiscovery.InheritsFromIoCToolsManagedBase(service)) return true;
        // Detect services whose constructor was already generated by IoCTools in a prior pass
        // (e.g. Keel LoggedHandler<T> subclasses: no direct IoCTools attrs, but the generated
        // constructor carries [GeneratedCode("IoCTools", ...)]). Avoids false TDIAG04 positives.
        return service.Constructors.Any(ctor =>
            !ctor.IsStatic &&
            !ctor.IsImplicitlyDeclared &&
            ctor.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() ==
                "System.CodeDom.Compiler.GeneratedCodeAttribute" &&
                a.ConstructorArguments.Length > 0 &&
                a.ConstructorArguments[0].Value is string tool &&
                tool == "IoCTools"));
    }

    private static bool GeneratesMockDependency(
        CoverClassInfo coverClass,
        ITypeSymbol dependencyType)
    {
        return !coverClass.UsesNullLogger ||
               !IsLoggerDependency(dependencyType);
    }

    private static bool IsLoggerDependency(ITypeSymbol dependencyType)
    {
        return dependencyType is INamedTypeSymbol named &&
               named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
               "global::Microsoft.Extensions.Logging.ILogger<TCategoryName>";
    }

    private static bool UsesNullLogger(AttributeData coverAttribute)
    {
        foreach (var namedArgument in coverAttribute.NamedArguments)
        {
            if (namedArgument.Key == "Logger" &&
                namedArgument.Value.Value is int loggerValue)
            {
                return loggerValue == 1;
            }
        }

        return false;
    }

    /// <summary>
    /// Emits TDIAG-06 if the fixture member planner would produce ambiguous or colliding names.
    /// Detects duplicate field names based on the same naming algorithm used by FixtureMemberPlanner.
    /// </summary>
    private static void EmitCollisionDiagnostics(
        CoverClassInfo coverClass,
        Action<Diagnostic> reportDiagnostic)
    {
        if (coverClass.ServiceSymbol == null) return;

        var constructor = coverClass.ServiceSymbol.Constructors
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault(c => !c.IsStatic);

        if (constructor == null) return;

        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        var collisionParams = new List<string>();

        foreach (var param in constructor.Parameters)
        {
            var fieldName = GetMockFieldName(param.Type);

            if (!fieldNames.Add(fieldName))
            {
                collisionParams.Add(param.Name);
            }
        }

        if (collisionParams.Count > 0)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.FixtureMemberCollision,
                coverClass.Symbol.Locations.FirstOrDefault(),
                coverClass.ServiceSymbol.Name,
                string.Join(", ", collisionParams)));
        }
    }

    /// <summary>
    /// Gets the mock field name for a type, mirroring TypeNameUtilities.GetSimpleTypeName logic
    /// so collision detection works without a direct dependency on the testing project.
    /// </summary>
    private static string GetMockFieldName(ITypeSymbol type)
    {
        var baseName = GetSimpleTypeName(type);
        return $"_mock{baseName}";
    }

    /// <summary>
    /// Extracts a readable type name for mock/setup helpers, mirroring TypeNameUtilities.GetSimpleTypeName.
    /// </summary>
    private static string GetSimpleTypeName(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var genericArgs = string.Join(null, namedType.TypeArguments.Select(GetSimpleTypeName));
            var baseName = StripInterfacePrefix(namedType.Name);
            return $"{baseName}{genericArgs}";
        }

        return StripInterfacePrefix(type.Name);
    }

    /// <summary>
    /// Removes common interface prefixes (I, II) from type names.
    /// </summary>
    private static string StripInterfacePrefix(string name)
    {
        if (name.StartsWith("II", StringComparison.Ordinal) && name.Length > 2 && char.IsUpper(name[2]))
            return name.Substring(1);
        if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
            return name.Substring(1);
        return name;
    }

    private static bool IsMoqMock(ITypeSymbol type) =>
        type is INamedTypeSymbol
        {
            MetadataName: "Mock`1",
            TypeArguments.Length: 1
        } mockType &&
        mockType.ContainingNamespace.ToDisplayString() == "Moq";

    /// <summary>
    /// Internal info about a test class using [Cover<T>].
    /// </summary>
    private readonly struct CoverClassInfo
    {
        public CoverClassInfo(
            INamedTypeSymbol symbol,
            TypeDeclarationSyntax declaration,
            INamedTypeSymbol? serviceSymbol,
            bool usesNullLogger)
        {
            Symbol = symbol;
            Declaration = declaration;
            ServiceSymbol = serviceSymbol;
            UsesNullLogger = usesNullLogger;
        }

        public INamedTypeSymbol Symbol { get; }
        public TypeDeclarationSyntax Declaration { get; }
        public INamedTypeSymbol? ServiceSymbol { get; }
        public bool UsesNullLogger { get; }
    }
}
