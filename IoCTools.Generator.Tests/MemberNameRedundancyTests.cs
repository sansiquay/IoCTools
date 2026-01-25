namespace IoCTools.Generator.Tests;


public class MemberNameRedundancyTests
{
    [Fact]
    public void DependsOn_MemberNameEqualsDefault_EmitsWarning()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[Scoped]
public partial class Service : IService { }

[Scoped]
[DependsOn<IService>(memberName1: ""_service"")]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var diagnostics = result.GetDiagnosticsByCode("IOC085");

        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("_service");
    }

    [Fact]
    public void DependsOn_MemberNameDiffersFromDefault_NoWarning()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[Scoped]
public partial class Service : IService { }

[Scoped]
[DependsOn<IService>(memberName1: ""custom"")]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var diagnostics = result.GetDiagnosticsByCode("IOC085");

        diagnostics.Should().BeEmpty();
    }
}
