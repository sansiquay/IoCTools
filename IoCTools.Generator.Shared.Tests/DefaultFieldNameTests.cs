namespace IoCTools.Generator.Shared.Tests;

using System.Linq;
using FluentAssertions;
using IoCTools.Generator.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

public sealed class DefaultFieldNameTests
{
    [Theory]
    [InlineData("IFoo", "_foo")]
    [InlineData("Foo", "_foo")]
    [InlineData("IBar", "_bar")]
    [InlineData("IRepository", "_repository")]
    [InlineData("IDerivedService", "_derivedService")]
    [InlineData("MyService", "_myService")]
    // Single "I" followed by lowercase is NOT the interface pattern -> keep as-is, apply camelCase
    [InlineData("Iinstance", "_iinstance")]
    // Single-letter type "I" has no uppercase-next-char -> not stripped
    [InlineData("I", "_i")]
    public void Strip_I_only_when_uppercase_second_char(string typeName, string expected)
    {
        DefaultFieldName.Compute(typeName).Should().Be(expected);
    }

    [Theory]
    // Reserved keyword with "_" prefix stays as-is (valid identifier because of "_")
    [InlineData("string", "_", "_string")]
    // Reserved keyword with empty prefix gets the "Value" suffix
    [InlineData("string", "", "stringValue")]
    // "Istring" — second char is lowercase, so NOT stripped; applies camelCase: "istring"
    [InlineData("Istring", "_", "_istring")]
    // "IString" — uppercase-next-char pattern, stripped; becomes reserved keyword
    [InlineData("IString", "_", "_string")]
    [InlineData("IString", "", "stringValue")]
    public void Reserved_keyword_handling(string typeName, string prefix, string expected)
    {
        DefaultFieldName.Compute(typeName, "CamelCase", true, prefix).Should().Be(expected);
    }

    [Theory]
    [InlineData("IFoo", "CamelCase", "_foo")]
    [InlineData("IFoo", "PascalCase", "_Foo")]
    [InlineData("IMyService", "SnakeCase", "_my_service")]
    public void Naming_convention_variants(string typeName, string convention, string expected)
    {
        DefaultFieldName.Compute(typeName, convention, true, "_").Should().Be(expected);
    }

    [Theory]
    // Custom prefix ending with underscore
    [InlineData("IFoo", "m_", "m_foo")]
    // Custom prefix not ending with underscore: concatenate, apply camelCase, add "_"
    [InlineData("IFoo", "my", "_myFoo")]
    public void Custom_prefix_handling(string typeName, string prefix, string expected)
    {
        DefaultFieldName.Compute(typeName, "CamelCase", true, prefix).Should().Be(expected);
    }

    [Fact]
    public void Array_type_gets_Array_suffix()
    {
        var type = GetTypeSymbol(@"
public class Svc { public IFoo[] Field; }
public interface IFoo { }", "Svc", "Field");
        DefaultFieldName.Compute(type).Should().Be("_fooArray");
    }

    [Fact]
    public void Generic_collection_unwraps_to_element_type()
    {
        var type = GetTypeSymbol(@"
using System.Collections.Generic;
public class Svc { public IEnumerable<IFoo> Field; }
public interface IFoo { }", "Svc", "Field");
        DefaultFieldName.Compute(type).Should().Be("_foo");
    }

    [Fact]
    public void Generic_non_collection_uses_outer_type_name()
    {
        // ILogger<T> is not in the collection list -> use "Logger"
        var type = GetTypeSymbol(@"
public interface ILogger<T> { }
public class Svc { public ILogger<Svc> Field; }", "Svc", "Field");
        DefaultFieldName.Compute(type).Should().Be("_logger");
    }

    [Fact]
    public void Nested_collection_unwraps_repeatedly()
    {
        var type = GetTypeSymbol(@"
using System.Collections.Generic;
public class Svc { public List<IList<IFoo>> Field; }
public interface IFoo { }", "Svc", "Field");
        DefaultFieldName.Compute(type).Should().Be("_foo");
    }

    [Fact]
    public void Rewriter_uses_canonical_helper_for_edge_cases()
    {
        // ILogger<Svc> would be "_iLogger<Svc>" under the OLD simplified logic
        // (fails because simple.Name is "ILogger" without accounting for generics).
        // With the canonical helper it correctly resolves to "_logger".
        var source = @"
namespace IoCTools.Abstractions.Annotations {
    public class InjectAttribute : System.Attribute { }
}
public interface ILogger<T> { }
public class Svc {
    [IoCTools.Abstractions.Annotations.Inject]
    private readonly ILogger<Svc> _logger = null!;
}
";
        var tree = CSharpSyntaxTree.ParseText(source);
        var comp = CSharpCompilation.Create("T", new[] { tree }, new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
        });
        var sem = comp.GetSemanticModel(tree);
        var svc = (INamedTypeSymbol)sem.GetDeclaredSymbol(
            tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.ValueText == "Svc"))!;
        var field = svc.GetMembers().OfType<IFieldSymbol>()
            .First(f => f.GetAttributes().Any(a => a.AttributeClass?.Name == "InjectAttribute"));
        var fieldDecl = field.DeclaringSyntaxReferences[0].GetSyntax().FirstAncestorOrSelf<FieldDeclarationSyntax>()!;

        var info = new InjectMigrationRewriter.InjectFieldInfo(fieldDecl, field.Type, field.Name, false);
        var result = InjectMigrationRewriter.Rewrite(
            new[] { info },
            AutoDepsResolverOutput.Empty);

        // Field name "_logger" IS the default for ILogger<Svc> -> no memberName override needed.
        result.AttributesToAdd.Should().HaveCount(1);
        result.AttributesToAdd[0].ToFullString().Should().NotContain("memberName");
    }

    private static ITypeSymbol GetTypeSymbol(string source, string className, string fieldName)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var comp = CSharpCompilation.Create("T", new[] { tree }, new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location),
        });
        var sem = comp.GetSemanticModel(tree);
        var cls = (INamedTypeSymbol)sem.GetDeclaredSymbol(
            tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.ValueText == className))!;
        var field = cls.GetMembers().OfType<IFieldSymbol>().First(f => f.Name == fieldName);
        return field.Type;
    }
}
