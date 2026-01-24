using FluentAssertions;

namespace IoCTools.Generator.Tests;

public class ManualOptionsBindingTests
{
    [Fact]
    public void Manual_AddOptions_ForGeneratedOptionsType_ProducesIOC083()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Test;

public class AlphaOptions { public string Name { get; set; } = ""default""; }

[Scoped]
public partial class AlphaConsumer
{
    [InjectConfiguration] private readonly AlphaOptions _alpha;
}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        var cfg = new ConfigurationBuilder().Build();
        services.AddOptions<AlphaOptions>().Bind(cfg.GetSection(""Alpha""));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC083");
    }

    [Fact]
    public void Manual_OptionsBuilderBind_ForGeneratedOptionsType_ProducesIOC083()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Test;

public class BetaOptions { public string Name { get; set; } = ""default""; }

[Scoped]
public partial class BetaConsumer
{
    [InjectConfiguration] private readonly BetaOptions _beta;
}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddOptions<BetaOptions>().Bind(cfg.GetSection(""Beta""));
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC083").Should().ContainSingle();
    }

    [Fact]
    public void Manual_Configure_ForGeneratedOptionsType_ProducesIOC083()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Test;

public class GammaOptions { public string Name { get; set; } = ""default""; }

[Scoped]
public partial class GammaConsumer
{
    [InjectConfiguration] private readonly GammaOptions _gamma;
}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.Configure<GammaOptions>(cfg);
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC083").Should().ContainSingle();
    }
}
