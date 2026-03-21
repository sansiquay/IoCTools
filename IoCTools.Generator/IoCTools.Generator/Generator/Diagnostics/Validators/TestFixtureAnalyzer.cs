namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System.Collections.Immutable;

using IoCTools.Generator.Diagnostics;
using IoCTools.Generator.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class TestFixtureAnalyzer
{
    /// <summary>
    /// Validates test fixture scenarios and emits appropriate diagnostics.
    /// Analyzes test classes for manual mocks, Cover<T> usage, and fixture opportunities.
    /// </summary>
    public static void Validate(
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic,
        DiagnosticConfiguration config)
    {
        if (!config.DiagnosticsEnabled) return;

        var testClasses = FindTestClasses(compilation);
        var servicesWithFixtures = FindServicesWithFixtures(compilation);

        foreach (var testClass in testClasses)
        {
            AnalyzeTestClass(testClass, servicesWithFixtures, compilation, reportDiagnostic, config);
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

    private static bool IsTestClass(INamedTypeSymbol symbol)
    {
        // Check for test project indicators: .Tests suffix, xUnit/NUnit/MSTest attributes
        var containingAssembly = symbol.ContainingAssembly?.Name ?? string.Empty;

        // Direct test assembly check
        if (containingAssembly.EndsWith(".Tests", StringComparison.Ordinal) ||
            containingAssembly.Contains(".Test"))
            return true;

        // Check for test framework attributes
        if (symbol.GetMembers().Any(m =>
            m.GetAttributes().Any(a =>
                a.AttributeClass?.Name.Contains("Fact") == true ||
                a.AttributeClass?.Name.Contains("Test") == true)))
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

                // Check for [Cover<T>] attribute
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
        ImmutableDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> servicesWithFixtures,
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic,
        DiagnosticConfiguration config)
    {
        // TDIAG-01/02: Check if this test class has a fixture and emits manual mocks/construction
        if (servicesWithFixtures.TryGetValue(testClass, out var services))
        {
            AnalyzeFixtureUsage(testClass, services[0], compilation, reportDiagnostic);
        }

        // TDIAG-03: Check for manual mocks that could use Cover<T>
        if (!servicesWithFixtures.ContainsKey(testClass))
        {
            AnalyzeFixtureOpportunity(testClass, compilation, reportDiagnostic);
        }
    }

    private static void AnalyzeFixtureUsage(
        INamedTypeSymbol testClass,
        INamedTypeSymbol service,
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic)
    {
        // TDIAG-01: Check for manual Mock<T> fields matching service dependencies
        var mockFields = testClass.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.Type.Name == "Mock`1" &&
                        f.Type is INamedTypeSymbol mockType &&
                        mockType.TypeArguments.Length > 0);

        foreach (var mockField in mockFields)
        {
            var mockedType = ((INamedTypeSymbol)mockField.Type).TypeArguments[0];
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ManualMockField,
                mockField.Locations.FirstOrDefault(),
                testClass.Name,
                mockedType.Name,
                service.Name));
        }

        // TDIAG-02: Check for manual `new Service(...)` construction
        var members = testClass.GetMembers();
        foreach (var member in members)
        {
            foreach (var location in member.Locations)
            {
                if (location.IsInSource)
                {
                    var syntaxTree = location.SourceTree;
                    if (syntaxTree != null)
                    {
                        var root = syntaxTree.GetRoot();
                        var node = root.FindNode(location.SourceSpan);
                        if (node is ObjectCreationExpressionSyntax objectCreation)
                        {
                            var semanticModel = compilation.GetSemanticModel(syntaxTree);
                            var typeSymbol = semanticModel.GetTypeInfo(objectCreation).Type;
                            if (typeSymbol != null &&
                                SymbolEqualityComparer.Default.Equals(typeSymbol, service))
                            {
                                reportDiagnostic(Diagnostic.Create(
                                    DiagnosticDescriptors.ManualSutConstruction,
                                    location,
                                    testClass.Name,
                                    service.Name));
                            }
                        }
                    }
                }
            }
        }
    }

    private static void AnalyzeFixtureOpportunity(
        INamedTypeSymbol testClass,
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic)
    {
        // Find all Mock<T> fields in the test class
        var mockFields = testClass.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.Type.Name == "Mock`1" &&
                        f.Type is INamedTypeSymbol mockType &&
                        mockType.TypeArguments.Length > 0)
            .ToImmutableArray();

        if (mockFields.IsEmpty) return;

        // Try to match mock types to a service's constructor dependencies
        var mockedTypesSet = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var mockField in mockFields)
        {
            mockedTypesSet.Add(((INamedTypeSymbol)mockField.Type).TypeArguments[0]);
        }

        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var typeDecls = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in typeDecls)
            {
                var serviceSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (serviceSymbol == null) continue;

                // Check if this service's constructor matches the mock types
                if (ServiceDependenciesMatch(serviceSymbol, mockedTypesSet))
                {
                    reportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.CouldUseFixture,
                        testClass.Locations.FirstOrDefault(),
                        testClass.Name,
                        mockFields.First().Type.Name,
                        serviceSymbol.Name));
                    return; // Only suggest one service
                }
            }
        }
    }

    private static bool ServiceDependenciesMatch(
        INamedTypeSymbol service,
        HashSet<ITypeSymbol> mockedTypes)
    {
        var constructor = service.Constructors
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault(c => !c.IsStatic);

        if (constructor == null) return false;

        return constructor.Parameters.All(p => mockedTypes.Contains(p.Type));
    }
}
