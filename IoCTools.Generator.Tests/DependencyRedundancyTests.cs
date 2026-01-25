namespace IoCTools.Generator.Tests;


public class DependencyRedundancyTests
{
    [Fact]
    public void InjectAndDependsOnAcrossInheritance_WarnsIOC040()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IFoo { }

public partial class Base
{
    [Inject] private readonly IFoo _foo;
}

[DependsOn<IFoo>]
public partial class Derived : Base
{
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC040");
        diags.Should().ContainSingle();
        diags[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void DuplicateInjectConfiguration_WarnsIOC040()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public partial class Service
{
    [InjectConfiguration(""App:Name"")] private readonly string _name1;
    [InjectConfiguration(""App:Name"")] private readonly string _name2;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC040").Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public void OptionsAndSubConfigurationOverlap_WarnsIOC046()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public class MyOptions { public string Name { get; set; } }

[DependsOnConfiguration<MyOptions>(""Settings"")]
public partial class Service
{
    [InjectConfiguration(""Settings:Name"")] private readonly string _name;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC046").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void DeepOptionsAndFieldOverlap_WarnsIOC046()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public class MyOptions { public string Name { get; set; } }

[DependsOnConfiguration<MyOptions>(""Settings"")]
public partial class Base { }

public partial class Derived : Base
{
    [InjectConfiguration(""Settings:Name"")] private readonly string _name;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC046").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void DuplicateConfigurationAcrossInheritance_WarnsIOC040()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public partial class Base
{
    [InjectConfiguration(""App:Name"")] private readonly string _nameBase;
}

public partial class Derived : Base
{
    [InjectConfiguration(""App:Name"")] private readonly string _nameDerived;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC040").Should().HaveCountGreaterOrEqualTo(1);
    }
}
