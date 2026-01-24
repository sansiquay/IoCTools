using FluentAssertions;
using IoCTools.Abstractions.Annotations;

namespace IoCTools.Generator.Tests;

public class OptionsBindingGenerationTests
{
    [Fact]
    public void StrongOptionsType_GeneratesAddOptionsAndValueSingleton()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace Test;

public class AlphaOptions { public string Name { get; set; } = """"; }

[Scoped]
public partial class AlphaConsumer
{
    [InjectConfiguration] private readonly AlphaOptions _alpha;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var registration = result.GetServiceRegistrationText();
        registration.Should().NotBeNull();
        registration!.Should().Contain("Configure<global::Test.AlphaOptions>(options => configuration.GetSection(\"Alpha\").Bind(options))");
        registration.Should().Contain("TryAddSingleton(sp => sp.GetRequiredService<IOptions<global::Test.AlphaOptions>>().Value)");
        registration.Should().Contain("GetSection(\"Alpha\")");
    }

    [Fact]
    public void OptionsTypeWithoutSuffix_AppendsOptionsSuffixForFieldNameAndBindsSection()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace Test;

public class PaymentConfig { public int Timeout { get; set; } }

[Scoped]
public partial class PaymentConsumer
{
    [InjectConfiguration] private readonly PaymentConfig _config;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var registration = result.GetServiceRegistrationText();
        registration.Should().NotBeNull();
        registration!.Should().Contain("Configure<global::Test.PaymentConfig>(options => configuration.GetSection(\"Payment\").Bind(options))");
        registration.Should().Contain("GetSection(\"Payment\")");
    }

    [Fact]
    public void DependsOnConfiguration_EmitsConfigureAndSingletonValue()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace Delta;

public class OutboxPerformanceBaselineOptions { public int BatchSize { get; set; } }

[Scoped]
[DependsOnConfiguration<OutboxPerformanceBaselineOptions>]
public partial class OutboxConsumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var constructor = result.GetConstructorSourceText("OutboxConsumer");
        var registration = result.GetServiceRegistrationText();

        registration.Should().NotBeNull();
        registration!.Should().Contain("Configure<global::Delta.OutboxPerformanceBaselineOptions>(options => configuration.GetSection(\"OutboxPerformanceBaseline\").Bind(options))");
        registration.Should().Contain("TryAddSingleton(sp => sp.GetRequiredService<IOptions<global::Delta.OutboxPerformanceBaselineOptions>>().Value)");
    }
}
