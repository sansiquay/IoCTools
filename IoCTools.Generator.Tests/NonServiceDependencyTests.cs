namespace IoCTools.Generator.Tests;


public class NonServiceDependencyTests
{
    [Theory]
    [InlineData("int")]
    [InlineData("string")]
    [InlineData("System.Guid")]
    [InlineData("System.Nullable<int>")]
    public void DependsOn_PrimitiveLike_ProducesIOC044(string typeName)
    {
        var source = $@"
using IoCTools.Abstractions.Annotations;

namespace Test;

[DependsOn<{typeName}>]
public partial class Consumer {{ }}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC044");
        diags.Should().ContainSingle();
        diags[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Inject_PrimitiveField_ProducesIOC044()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public partial class Consumer
{
    [Inject] private readonly int _value;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC044").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void DependsOn_Interface_NoIOC044()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IFoo { }

[DependsOn<IFoo>]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC044").Should().BeEmpty();
    }

    [Fact]
    public void DependsOn_IEnumerable_Interface_NoIOC044()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Test;

public interface IFoo { }

[DependsOn<IEnumerable<IFoo>>]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC044").Should().BeEmpty();
    }

    [Fact]
    public void DependsOn_IEnumerable_Primitive_ProducesIOC044()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Test;

[DependsOn<IEnumerable<int>>]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC044").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }
}
