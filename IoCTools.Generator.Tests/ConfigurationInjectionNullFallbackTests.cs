namespace IoCTools.Generator.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;

/// <summary>
///     T61: when an <see cref="InjectConfigurationAttribute"/> / <c>DependsOnConfiguration&lt;T&gt;</c>
///     section is absent from <see cref="IConfiguration"/>, <c>GetSection(...).Get&lt;T&gt;()</c>
///     returns <c>null</c>. Prior to T61 the generator emitted <c>Get&lt;T&gt;()!</c> with the
///     null-forgiving operator, so the field was assigned <c>null</c> and downstream call sites
///     NPE'd at first dereference. This was observed in Delta's
///     <c>DeadLetterDiagnosticsProjector</c> when the diagnostics config section was missing
///     from <c>appsettings.*.json</c>.
///
///     Required-section behavior is unchanged: an absent section still throws
///     <see cref="InvalidOperationException"/>. Only the optional path gains a safe default.
/// </summary>
public class ConfigurationInjectionNullFallbackTests
{
    [Fact]
    public void OptionalSection_ComplexType_EmitsNewFallback_NotBangSuppression()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;
public class DiagnosticsSettings
{
    public string Mode { get; set; } = """";
    public int Threshold { get; set; }
}

public partial class DiagnosticsConsumer
{
    [InjectConfiguration(""Diagnostics"", Required = false)] private readonly DiagnosticsSettings _settings;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse();

        var ctor = result.GetConstructorSourceText("DiagnosticsConsumer");
        ctor.Should()
            .Contain(
                "configuration.GetSection(\"Diagnostics\").Get<DiagnosticsSettings>() ?? new DiagnosticsSettings()",
                "absent optional section must default-construct rather than leak null through `!`");
        ctor.Should().NotContain(
            "Get<DiagnosticsSettings>()!",
            "the previous null-forgiving emission masked the absent-section NPE");
    }

    [Fact]
    public void RequiredSection_ComplexType_StillThrows()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;
public class RequiredSettings { public string Name { get; set; } = """"; }

public partial class RequiredConsumer
{
    [InjectConfiguration(""RequiredCfg"", Required = true)] private readonly RequiredSettings _settings;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse();

        var ctor = result.GetConstructorSourceText("RequiredConsumer");
        ctor.Should().Contain(
            "throw new global::System.InvalidOperationException(\"Required configuration section 'RequiredCfg' is missing\")",
            "required sections preserve fail-fast semantics");
        ctor.Should().NotContain("?? new RequiredSettings()");
    }

    [Fact]
    public void OptionalSection_AbstractType_KeepsBangSuppression()
    {
        // Abstract types cannot be `new`'d. The generator must not emit `?? new T()` for them.
        // (Practically, an `[InjectConfiguration]` field of abstract type is exotic / a misuse,
        // but the generator must remain robust.)
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;
public abstract class AbstractSettings { public string Name { get; set; } = """"; }

public partial class AbstractConsumer
{
    [InjectConfiguration(""Abstract"", Required = false)] private readonly AbstractSettings _settings;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse();

        var ctor = result.GetConstructorSourceText("AbstractConsumer");
        ctor.Should().Contain("Get<AbstractSettings>()!");
        ctor.Should().NotContain("?? new AbstractSettings()");
    }

    [Fact]
    public void OptionalSection_AbsentAtRuntime_BindsToDefaultInstance_NoNullReference()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;
public class DiagnosticsSettings
{
    public string Mode { get; set; } = ""default-mode"";
    public int Threshold { get; set; } = 7;
}

public partial class DiagnosticsConsumer
{
    [InjectConfiguration(""Diagnostics"", Required = false)] private readonly DiagnosticsSettings _settings;
    public DiagnosticsSettings Settings => _settings;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse();

        var ctx = SourceGeneratorTestHelper.CreateRuntimeContext(result);
        var consumerType = ctx.Assembly.GetTypes().First(t => t.Name == "DiagnosticsConsumer");
        var settingsType = ctx.Assembly.GetTypes().First(t => t.Name == "DiagnosticsSettings");

        // Configuration with NO "Diagnostics" key at all — Get<T>() returns null.
        IConfiguration emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var instance = Activator.CreateInstance(consumerType, emptyConfig);
        instance.Should().NotBeNull("absent optional section must not NPE the constructor");

        var settings = consumerType.GetProperty("Settings")!.GetValue(instance);
        settings.Should().NotBeNull(
            "field must be a default-constructed instance, not a null leaked through `!`");
        settings!.GetType().Should().Be(settingsType);

        // Property defaults from the type are honored — confirms `new T()` ran, not a populated section.
        var mode = settingsType.GetProperty("Mode")!.GetValue(settings);
        mode.Should().Be("default-mode");
        var threshold = settingsType.GetProperty("Threshold")!.GetValue(settings);
        threshold.Should().Be(7);
    }
}
