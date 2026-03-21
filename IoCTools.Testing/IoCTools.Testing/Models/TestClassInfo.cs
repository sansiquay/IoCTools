namespace IoCTools.Testing.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Test class information captured for fixture generation.
/// Carries the test class symbol, the service being tested, and source location.
/// </summary>
internal readonly struct TestClassInfo
{
    public TestClassInfo(
        INamedTypeSymbol testClassSymbol,
        TypeDeclarationSyntax testClassDeclaration,
        SemanticModel semanticModel,
        INamedTypeSymbol serviceSymbol)
    {
        TestClassSymbol = testClassSymbol;
        TestClassDeclaration = testClassDeclaration;
        SemanticModel = semanticModel;
        ServiceSymbol = serviceSymbol;
    }

    public INamedTypeSymbol TestClassSymbol { get; }
    public TypeDeclarationSyntax TestClassDeclaration { get; }
    public SemanticModel SemanticModel { get; }
    public INamedTypeSymbol ServiceSymbol { get; }

    public string TestClassName => TestClassSymbol.Name;
    public string TestClassNamespace => TestClassSymbol.ContainingNamespace?.ToString() ?? string.Empty;
    public string ServiceName => ServiceSymbol.Name;
}
