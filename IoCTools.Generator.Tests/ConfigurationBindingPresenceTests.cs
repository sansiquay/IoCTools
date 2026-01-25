namespace IoCTools.Generator.Tests;


public class ConfigurationBindingPresenceTests
{
    [Fact]
    public void MissingBinding_ForRequiredOptions_EmitsIOC057()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Options;

namespace Test;

public class MyOptions { public string Value { get; set; } = string.Empty; }

[RegisterAs]
public partial class MissingBindingService
{
    [InjectConfiguration] private readonly IOptions<MyOptions> _options;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC057").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ConfigureCall_SuppressesMissingBindingDiagnostic()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Test;

public class MyOptions { public string Value { get; set; } = string.Empty; }

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MyOptions>(configuration.GetSection(""MyOptions""));
    }
}

[RegisterAs]
public partial class ConfiguredService
{
    [InjectConfiguration] private readonly IOptions<MyOptions> _options;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC057").Should().BeEmpty();
    }

    [Fact]
    public void ConfigurationBinderGet_SuppressesMissingBindingDiagnostic()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Test;

public class MyOptions { public int Count { get; set; } }

public static class Bootstrap
{
    public static void Load(IConfiguration configuration)
    {
        var opts = configuration.GetSection(""MyOptions"").Get<MyOptions>();
    }
}

[RegisterAs]
public partial class BinderService
{
    [InjectConfiguration] private readonly IOptions<MyOptions> _options;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC057").Should().BeEmpty();
    }

    [Fact]
    public void OptionsBuilderBindConfiguration_SuppressesDiagnostic()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Test;

public class MyOptions { public bool Flag { get; set; } }

public static class Module
{
    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MyOptions>().BindConfiguration(""MyOptions"");
    }
}

[RegisterAs]
public partial class OptionsBuilderService
{
    [InjectConfiguration] private readonly IOptionsSnapshot<MyOptions> _options;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC057").Should().BeEmpty();
    }

    [Fact]
    public void IConfigureOptionsImplementation_SuppressesDiagnostic()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Options;

namespace Test;

public class MyOptions { public string Name { get; set; } = string.Empty; }

public class ConfigureMyOptions : IConfigureOptions<MyOptions>
{
    public void Configure(MyOptions options) => options.Name = ""configured"";
}

[RegisterAs]
public partial class ConfigureOptionsService
{
    [InjectConfiguration] private readonly IOptionsMonitor<MyOptions> _options;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC057").Should().BeEmpty();
    }

    [Fact]
    public void OptionalConfiguration_DoesNotEmitDiagnostic()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Options;

namespace Test;

public class MyOptions { public string? Maybe { get; set; } }

[RegisterAs]
public partial class OptionalConfigService
{
    [InjectConfiguration(Required = false)] private readonly IOptions<MyOptions> _options;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC057").Should().BeEmpty();
    }

    [Fact]
    public void MixedBoundAndUnboundOptions_EmitsOnlyForUnbound()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Test;

public class BoundOptions { public int Value { get; set; } }
public class UnboundOptions { public int Value { get; set; } }

public static class Setup
{
    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BoundOptions>(configuration.GetSection(""BoundOptions""));
    }
}

[RegisterAs]
public partial class DualService
{
    [InjectConfiguration] private readonly IOptions<BoundOptions> _bound;
    [InjectConfiguration] private readonly IOptions<UnboundOptions> _unbound;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC057").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void MultipleBindings_DoNotDoubleReportOrWarn()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Test;

public class MyOptions { public string Name { get; set; } = string.Empty; }

public static class DualBinding
{
    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MyOptions>(configuration.GetSection(""MyOptions""));
        _ = configuration.GetSection(""MyOptions"").Get<MyOptions>();
    }
}

[RegisterAs]
public partial class DualBindingService
{
    [InjectConfiguration] private readonly IOptions<MyOptions> _options;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC057").Should().BeEmpty();
    }

    [Fact]
    public void JsonConfigurationBuilderAndGet_SuppressesDiagnostic()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Test;

public class MyOptions { public int Port { get; set; } }

public static class JsonConfig
{
    public static IConfiguration BuildConfig()
    {
        return new ConfigurationBuilder()
            .AddJsonFile(""appsettings.json"", optional: true, reloadOnChange: false)
            .Build();
    }

    public static void UseConfig()
    {
        var config = BuildConfig();
        var opts = config.GetSection(""MyOptions""), opts2 = config.GetSection(""Other""), optsObj = opts.Get<MyOptions>();
    }
}

[RegisterAs]
public partial class JsonConfigService
{
    [InjectConfiguration] private readonly IOptions<MyOptions> _options;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC057").Should().BeEmpty();
    }
}
