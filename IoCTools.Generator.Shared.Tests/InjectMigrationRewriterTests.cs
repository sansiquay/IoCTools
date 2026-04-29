namespace IoCTools.Generator.Shared.Tests;

using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using IoCTools.Generator.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

public sealed class InjectMigrationRewriterTests
{
    private const string AttributeStubs = @"
namespace IoCTools.Abstractions.Annotations {
    public class InjectAttribute : System.Attribute { }
    public class ExternalServiceAttribute : System.Attribute { }
}
";

    private static (InjectMigrationRewriter.InjectFieldInfo[] fields, AutoDepsResolverOutput resolved) BuildFixture(
        string source,
        params string[] autoDepTypeNames)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("T", new[] { syntaxTree }, refs);
        var semantic = comp.GetSemanticModel(syntaxTree);

        var classDecl = syntaxTree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Svc");
        var classSymbol = (INamedTypeSymbol)semantic.GetDeclaredSymbol(classDecl)!;

        var fields = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => f.GetAttributes().Any(a => a.AttributeClass?.Name == "InjectAttribute"))
            .Select(f =>
            {
                var syntaxRef = f.DeclaringSyntaxReferences[0].GetSyntax();
                var fieldDecl = syntaxRef.FirstAncestorOrSelf<FieldDeclarationSyntax>()!;
                var hasExtern = f.GetAttributes().Any(a => a.AttributeClass?.Name == "ExternalServiceAttribute");
                return new InjectMigrationRewriter.InjectFieldInfo(fieldDecl, f.Type, f.Name, hasExtern);
            })
            .ToArray();

        var entries = fields
            .Where(f => autoDepTypeNames.Contains(f.Type.Name))
            .Select(f => new AutoDepResolvedEntry(
                SymbolIdentity.From(f.Type),
                ImmutableArray.Create(new AutoDepAttribution(AutoDepSourceKind.AutoUniversal, null, null))))
            .ToImmutableArray();

        return (fields, new AutoDepsResolverOutput(entries));
    }

    [Fact]
    public void Delete_covered_bare_field()
    {
        var source = AttributeStubs + @"
public class Svc {
    [IoCTools.Abstractions.Annotations.Inject]
    private readonly IFoo _foo = null!;
}
public interface IFoo { }
";
        var (fields, resolved) = BuildFixture(source, "IFoo");
        var result = InjectMigrationRewriter.Rewrite(fields, resolved);
        result.FieldsToDelete.Should().HaveCount(1);
        result.AttributesToAdd.Should().BeEmpty();
    }

    [Fact]
    public void Convert_bare_field_not_covered()
    {
        var source = AttributeStubs + @"
public class Svc {
    [IoCTools.Abstractions.Annotations.Inject]
    private readonly IFoo _foo = null!;
}
public interface IFoo { }
";
        var (fields, resolved) = BuildFixture(source);
        var result = InjectMigrationRewriter.Rewrite(fields, resolved);
        // Converted fields are replaced -- the original [Inject] field must be removed
        // in addition to the class-level [DependsOn<T>] attribute being added.
        result.FieldsToDelete.Should().HaveCount(1);
        result.AttributesToAdd.Should().HaveCount(1);
        var text = result.AttributesToAdd[0].ToFullString();
        text.Should().Contain("DependsOn").And.Contain("IFoo");
        // Bare field: no memberName arg should be emitted
        text.Should().NotContain("memberName");
    }

    [Fact]
    public void Convert_preserves_custom_name_via_memberName1()
    {
        var source = AttributeStubs + @"
public class Svc {
    [IoCTools.Abstractions.Annotations.Inject]
    private readonly IFoo _customName = null!;
}
public interface IFoo { }
";
        var (fields, resolved) = BuildFixture(source);
        var result = InjectMigrationRewriter.Rewrite(fields, resolved);
        result.AttributesToAdd.Should().HaveCount(1);
        var text = result.AttributesToAdd[0].ToFullString();
        text.Should().Contain("memberName1").And.Contain("_customName");
    }

    [Fact]
    public void Convert_preserves_ExternalService_as_external_true()
    {
        var source = AttributeStubs + @"
public class Svc {
    [IoCTools.Abstractions.Annotations.Inject]
    [IoCTools.Abstractions.Annotations.ExternalService]
    private readonly IFoo _foo = null!;
}
public interface IFoo { }
";
        var (fields, resolved) = BuildFixture(source);
        var result = InjectMigrationRewriter.Rewrite(fields, resolved);
        result.AttributesToAdd.Should().HaveCount(1);
        var text = result.AttributesToAdd[0].ToFullString();
        text.Should().Contain("external").And.Contain("true");
    }

    [Fact]
    public void Coalesce_multiple_bare_fields_into_single_attribute()
    {
        var source = AttributeStubs + @"
public class Svc {
    [IoCTools.Abstractions.Annotations.Inject] private readonly IFoo _foo = null!;
    [IoCTools.Abstractions.Annotations.Inject] private readonly IBar _bar = null!;
}
public interface IFoo { }
public interface IBar { }
";
        var (fields, resolved) = BuildFixture(source);
        var result = InjectMigrationRewriter.Rewrite(fields, resolved);
        result.AttributesToAdd.Should().HaveCount(1);
        var text = result.AttributesToAdd[0].ToFullString();
        text.Should().Contain("IFoo").And.Contain("IBar");
    }

    [Fact]
    public void Split_on_divergent_external_flags()
    {
        var source = AttributeStubs + @"
public class Svc {
    [IoCTools.Abstractions.Annotations.Inject]
    [IoCTools.Abstractions.Annotations.ExternalService]
    private readonly IFoo _foo = null!;

    [IoCTools.Abstractions.Annotations.Inject]
    private readonly IBar _bar = null!;
}
public interface IFoo { }
public interface IBar { }
";
        var (fields, resolved) = BuildFixture(source);
        var result = InjectMigrationRewriter.Rewrite(fields, resolved);
        result.AttributesToAdd.Should().HaveCount(2);
        var texts = result.AttributesToAdd.Select(a => a.ToFullString()).ToList();
        texts.Should().ContainSingle(t => t.Contains("external") && t.Contains("IFoo"));
        texts.Should().ContainSingle(t => !t.Contains("external") && t.Contains("IBar"));
    }

    private static (ClassDeclarationSyntax classDecl, SemanticModel semantic) BuildClassFixture(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Diagnostics.CodeAnalysis.SuppressMessageAttribute).Assembly.Location),
        };
        var comp = CSharpCompilation.Create("T", new[] { syntaxTree }, refs);
        var semantic = comp.GetSemanticModel(syntaxTree);

        var classDecl = syntaxTree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Svc");
        return (classDecl, semantic);
    }

    [Fact]
    public void Skip_field_with_SuppressMessage_IOC095_at_field_level()
    {
        var source = AttributeStubs + @"
public class Svc {
    [System.Diagnostics.CodeAnalysis.SuppressMessage(""IoCTools.Usage"", ""IOC095"", Justification = ""intentional demo"")]
    [IoCTools.Abstractions.Annotations.Inject]
    private readonly IFoo _foo = null!;
}
public interface IFoo { }
";
        var (classDecl, semantic) = BuildClassFixture(source);
        var fields = InjectMigrationRewriter.CollectInjectFields(classDecl, semantic);
        fields.Should().BeEmpty();
    }

    [Fact]
    public void Skip_all_inject_fields_when_class_carries_SuppressMessage_IOC095()
    {
        var source = AttributeStubs + @"
[System.Diagnostics.CodeAnalysis.SuppressMessage(""IoCTools.Usage"", ""IOC095"", Justification = ""whole class is the demo"")]
public class Svc {
    [IoCTools.Abstractions.Annotations.Inject] private readonly IFoo _foo = null!;
    [IoCTools.Abstractions.Annotations.Inject] private readonly IBar _bar = null!;
}
public interface IFoo { }
public interface IBar { }
";
        var (classDecl, semantic) = BuildClassFixture(source);
        var fields = InjectMigrationRewriter.CollectInjectFields(classDecl, semantic);
        fields.Should().BeEmpty();
    }

    [Fact]
    public void Skip_field_inside_pragma_warning_disable_IOC095_block()
    {
        var source = AttributeStubs + @"
public class Svc {
#pragma warning disable IOC095
    [IoCTools.Abstractions.Annotations.Inject] private readonly IFoo _foo = null!;
#pragma warning restore IOC095
    [IoCTools.Abstractions.Annotations.Inject] private readonly IBar _bar = null!;
}
public interface IFoo { }
public interface IBar { }
";
        var (classDecl, semantic) = BuildClassFixture(source);
        var fields = InjectMigrationRewriter.CollectInjectFields(classDecl, semantic);
        fields.Should().HaveCount(1);
        fields[0].Type.Name.Should().Be("IBar");
    }
}
