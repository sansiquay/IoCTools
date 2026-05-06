using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace IoCTools.Testing.Tests;

using System.Collections.Immutable;

/// <summary>
/// Tests for configuration and options injection fixture generation scenarios.
/// Verifies IConfiguration, IOptions{T}, IOptionsSnapshot{T}, and IOptionsMonitor{T} helpers.
/// </summary>
public sealed class ConfigurationFixtureTests
{
    #region Helpers

    private static TestHelper.GenerationResult GenerateFixture(string source)
    {
        var iocToolsAssembly = typeof(Abstractions.Annotations.ScopedAttribute).Assembly;
        var iocTestingAssembly = typeof(IoCTools.Testing.Annotations.CoverAttribute<>).Assembly;

        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => MetadataReference.CreateFromFile(p))
            .Cast<MetadataReference>()
            .ToList();

        var metadataRefs = new List<MetadataReference>(trustedAssemblies)
        {
            MetadataReference.CreateFromFile(iocToolsAssembly.Location),
            MetadataReference.CreateFromFile(iocTestingAssembly.Location),
        };

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

        var compilation = CSharpCompilation.Create(
            "Test",
            new[] { syntaxTree },
            metadataRefs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable));

        var testingGenerator = new IoCTools.Testing.IoCToolsTestingGenerator();
        var driver = CSharpGeneratorDriver.Create(new[]
            {
                testingGenerator.AsSourceGenerator()
            },
            Array.Empty<AdditionalText>(),
            new CSharpParseOptions(LanguageVersion.Preview));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees
            .Skip(1)
            .ToImmutableArray();

        return new TestHelper.GenerationResult(generatedTrees, diagnostics.ToImmutableArray());
    }

    private static string? GetFixtureSource(ImmutableArray<SyntaxTree> trees, string hintName)
    {
        return trees
            .Select(t => t.ToString())
            .FirstOrDefault(s => s.Contains(hintName));
    }

    #endregion

    #region IConfiguration

    [Fact]
    public void Configuration_Helper_Pattern_Supports_Indexer_And_GetValue()
    {
        var mockConfiguration = new Mock<IConfiguration>();

        ConfigureMockIConfiguration(
            mockConfiguration,
            ("Feature:Enabled", true),
            ("Feature:Name", "Delta"),
            ("Feature:Limit", 5));

        var configuration = mockConfiguration.Object;

        configuration["Feature:Name"].Should().Be("Delta");
        configuration.GetValue<string>("Feature:Name").Should().Be("Delta");
        configuration.GetValue<bool>("Feature:Enabled").Should().BeTrue();
        configuration.GetValue<int>("Feature:Limit").Should().Be(5);
        configuration.GetRequiredSection("Feature").Exists().Should().BeTrue();
        configuration.GetRequiredSection("Feature").GetChildren().Select(c => c.Key)
            .Should().BeEquivalentTo("Enabled", "Name", "Limit");
    }

    [Fact]
    public void Service_With_IConfiguration_Gets_Mock_Field_And_Helper()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Configuration;

            public partial class ConfigService
            {
                private readonly IConfiguration _configuration;

                public ConfigService(IConfiguration configuration)
                {
                    _configuration = configuration;
                }
            }

            [Cover<ConfigService>]
            public partial class ConfigServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "ConfigServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("Mock<IConfiguration> _mockConfiguration", "should have mock field for IConfiguration");
        fixtureSource.Should().Contain("ConfigureIConfiguration(", "should have configuration helper");
        fixtureSource.Should().Contain("ConfigureConfiguration(", "should have shorter configuration helper alias");
        fixtureSource.Should().Contain("Func<string, object?> valueProvider", "helper should accept value provider");
        fixtureSource.Should().NotContain(".Setup(x => x.GetValue", "GetValue<T> is an extension method and cannot be mocked directly");
        fixtureSource.Should().Contain(".Setup(x => x.GetSection(It.IsAny<string>()))", "GetValue<T> resolves through IConfiguration.GetSection");
        fixtureSource.Should().Contain("Mock<IConfigurationSection>", "helper should provide sections for typed configuration reads");
        fixtureSource.Should().Contain(".Setup(x => x[It.IsAny<string>()])", "should setup indexer");
        fixtureSource.Should().Contain(".Setup(x => x.GetChildren())", "GetRequiredSection and binder APIs need child sections");
    }

    #endregion

    #region IOptions<T>

    [Fact]
    public void Service_With_IOptions_Gets_Mock_And_Use_Configure_Helpers()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Options;

            public class DbOptions { public string ConnectionString { get; set; } }

            public partial class OptionsService
            {
                private readonly IOptions<DbOptions> _options;

                public OptionsService(IOptions<DbOptions> options)
                {
                    _options = options;
                }
            }

            [Cover<OptionsService>]
            public partial class OptionsServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "OptionsServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("Mock<IOptions<DbOptions>>", "should have mock field");
        fixtureSource.Should().Contain("UseDbOptions(", "should have Use{OptionsName} helper");
        fixtureSource.Should().Contain("ConfigureDbOptions(", "should have Configure{OptionsName} helper");
        fixtureSource.Should().Contain(".Setup(x => x.Value).Returns(value)", "Use helper should setup .Value");
    }

    [Fact]
    public void Options_Without_Parameterless_Constructor_Uses_Value_Based_Configure_Helper()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Options;

            public class RequiredOptions
            {
                public RequiredOptions(string name) => Name = name;
                public string Name { get; set; }
            }

            public partial class RequiredOptionsService
            {
                private readonly IOptions<RequiredOptions> _options;

                public RequiredOptionsService(IOptions<RequiredOptions> options)
                {
                    _options = options;
                }
            }

            [Cover<RequiredOptionsService>]
            public partial class RequiredOptionsServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "RequiredOptionsServiceTests");
        var compileResult = TestHelper.VerifyCompiles(source, result);

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("UseRequiredOptions(RequiredOptions value)");
        fixtureSource.Should().Contain("ConfigureRequiredOptions(RequiredOptions value, Action<RequiredOptions> configure)");
        fixtureSource.Should().NotContain("new RequiredOptions()");
        compileResult.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Options_With_Internal_Parameterless_Constructor_Uses_Value_Based_Configure_Helper()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Options;

            public class InternalOptions
            {
                internal InternalOptions() { }
                public string? Name { get; set; }
            }

            public partial class InternalOptionsService
            {
                private readonly IOptions<InternalOptions> _options;

                public InternalOptionsService(IOptions<InternalOptions> options)
                {
                    _options = options;
                }
            }

            [Cover<InternalOptionsService>]
            public partial class InternalOptionsServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "InternalOptionsServiceTests");
        var compileResult = TestHelper.VerifyCompiles(source, result);

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("ConfigureInternalOptions(InternalOptions value, Action<InternalOptions> configure)");
        fixtureSource.Should().NotContain("new InternalOptions()");
        compileResult.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Options_Helpers_Use_And_Configure_Are_Distinct()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Options;

            public class CacheConfig { public int TtlSeconds { get; set; } }

            public partial class CacheService
            {
                private readonly IOptions<CacheConfig> _options;

                public CacheService(IOptions<CacheConfig> options)
                {
                    _options = options;
                }
            }

            [Cover<CacheService>]
            public partial class CacheServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "CacheServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        // Use method assigns value directly
        fixtureSource.Should().Contain("private CacheConfig UseCacheConfig(CacheConfig value)");
        fixtureSource.Should().Contain(".Setup(x => x.Value).Returns(value);");
        // Configure method creates options and applies action
        fixtureSource.Should().Contain("private CacheConfig ConfigureCacheConfig(Action<CacheConfig> configure)");
        fixtureSource.Should().Contain("Options.Create(new CacheConfig())");
    }

    #endregion

    #region IOptionsSnapshot<T>

    [Fact]
    public void Service_With_IOptionsSnapshot_Gets_Named_Get_Support()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Options;

            public class FeatureFlags { public bool NewUI { get; set; } }

            public partial class FeatureService
            {
                private readonly IOptionsSnapshot<FeatureFlags> _options;

                public FeatureService(IOptionsSnapshot<FeatureFlags> options)
                {
                    _options = options;
                }
            }

            [Cover<FeatureService>]
            public partial class FeatureServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "FeatureServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("Mock<IOptionsSnapshot<FeatureFlags>>", "should have mock field");
        fixtureSource.Should().Contain("UseFeatureFlags(", "should have Use{OptionsName} helper");
        fixtureSource.Should().Contain("FeatureFlags ConfigureFeatureFlags(Action<FeatureFlags> configure)", "should have simple configure");
        fixtureSource.Should().Contain("FeatureFlags ConfigureFeatureFlags(string name, Action<FeatureFlags> configure)", "should have named configure");
        fixtureSource.Should().Contain(".Setup(x => x.Get(name))", "named configure should setup Get(name)");
    }

    #endregion

    #region IOptionsMonitor<T>

    [Fact]
    public void Service_With_IOptionsMonitor_Gets_CurrentValue_And_Get_Support()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Options;

            public class AppConfig { public string Environment { get; set; } }

            public partial class MonitorService
            {
                private readonly IOptionsMonitor<AppConfig> _options;

                public MonitorService(IOptionsMonitor<AppConfig> options)
                {
                    _options = options;
                }
            }

            [Cover<MonitorService>]
            public partial class MonitorServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "MonitorServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("Mock<IOptionsMonitor<AppConfig>>", "should have mock field");
        fixtureSource.Should().Contain("UseAppConfig(", "should have Use helper");
        fixtureSource.Should().Contain("ConfigureAppConfig(", "should have Configure helper");
        fixtureSource.Should().Contain(".Setup(x => x.CurrentValue)", "should setup CurrentValue");
        fixtureSource.Should().Contain(".Setup(x => x.Get(It.IsAny<string>()))", "should setup named Get");
    }

    #endregion

    #region Correctness Assertions

    [Fact]
    public void IOptionsMonitor_EmitsSingleConfigure_WithCurrentValue()
    {
        // Verify IOptionsMonitor emits only one Configure{Name} (no duplicate)
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Options;

            public class AppConfig { public string Environment { get; set; } }

            public partial class MonitorService
            {
                private readonly IOptionsMonitor<AppConfig> _options;
                public MonitorService(IOptionsMonitor<AppConfig> options) { _options = options; }
            }

            [Cover<MonitorService>]
            public partial class MonitorServiceTests { }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "MonitorServiceTests");
        fixtureSource.Should().NotBeNull();

        // Exactly one ConfigureAppConfig( method
        int configureCount = 0, idx = 0;
        while ((idx = fixtureSource.IndexOf("ConfigureAppConfig(", idx, StringComparison.Ordinal)) != -1)
        { configureCount++; idx++; }
        configureCount.Should().Be(1, "IOptionsMonitor should have exactly one Configure helper");

        fixtureSource.Should().Contain("CurrentValue", "IOptionsMonitor should use CurrentValue, not Value");
        fixtureSource.Should().Contain(".Get(It.IsAny<string>())", "IOptionsMonitor should support named Get");
    }

    [Fact]
    public void TimeProvider_PassesFieldDirectly_NotObject()
    {
        // Verify TimeProvider constructor arg uses field directly, not .Object
        var source = """
            using IoCTools.Testing.Annotations;
            using System;

            public partial class TimeService
            {
                private readonly TimeProvider _timeProvider;
                public TimeService(TimeProvider timeProvider) { _timeProvider = timeProvider; }
            }

            [Cover<TimeService>]
            public partial class TimeServiceTests { }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "TimeServiceTests");
        fixtureSource.Should().NotBeNull();

        fixtureSource.Should().Contain("private TimeProvider TimeProvider { get; set; } = System.TimeProvider.System;", "should declare configurable TimeProvider property");
        fixtureSource.Should().Contain("UseTimeProvider(TimeProvider timeProvider) => TimeProvider = timeProvider", "should expose setup helper");
        fixtureSource.Should().NotContain("TimeProvider.Object", "should NOT use .Object");
        fixtureSource.Should().Contain("CreateSut() => new", "should have factory");
    }

    [Fact]
    public void IOptionsSnapshot_HasBothSimpleAndNamedOverloads()
    {
        // IOptionsSnapshot should have both simple Configure and named Configure(string name, ...)
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Options;

            public class Features { public bool Enabled { get; set; } }

            public partial class FeatureService
            {
                private readonly IOptionsSnapshot<Features> _opts;
                public FeatureService(IOptionsSnapshot<Features> opts) { _opts = opts; }
            }

            [Cover<FeatureService>]
            public partial class FeatureServiceTests { }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "FeatureServiceTests");
        fixtureSource.Should().NotBeNull();

        fixtureSource.Should().Contain("Features ConfigureFeatures(Action<Features> configure)", "simple overload");
        fixtureSource.Should().Contain("Features ConfigureFeatures(string name, Action<Features> configure)", "named overload");
    }

    #endregion

    #region No Blocking Errors

    [Fact]
    public void Configuration_Fixture_Has_No_Blocking_Errors()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.Options;

            public class AppOptions { public string Key { get; set; } }

            public partial class ConfigService
            {
                private readonly IConfiguration _config;
                private readonly IOptions<AppOptions> _options;

                public ConfigService(IConfiguration config, IOptions<AppOptions> options)
                {
                    _config = config;
                    _options = options;
                }
            }

            [Cover<ConfigService>]
            public partial class ConfigServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);

        // Assert
        var blockingErrors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        blockingErrors.Should().BeEmpty();
    }

    [Fact]
    public void ConfigurationFixture_VerifyCompiles()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.Options;

            public class AppOptions { public string Key { get; set; } }

            public partial class ConfigService
            {
                private readonly IConfiguration _config;
                private readonly IOptions<AppOptions> _options;

                public ConfigService(IConfiguration config, IOptions<AppOptions> options)
                {
                    _config = config;
                    _options = options;
                }
            }

            [Cover<ConfigService>]
            public partial class ConfigServiceTests { }
            """;

        // Act
        var genResult = GenerateFixture(source);

        // Add Configuration.Binder for GetValue<T> extension method
        var binderRef = new MetadataReference[0];
        var binderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages", "microsoft.extensions.configuration.binder", "6.0.0", "lib", "netstandard2.0", "Microsoft.Extensions.Configuration.Binder.dll");
        if (File.Exists(binderPath))
            binderRef = new[] { MetadataReference.CreateFromFile(binderPath) };
        var compileResult = TestHelper.VerifyCompiles(source, genResult, binderRef);

        // Assert
        var errors = compileResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty("generated configuration fixture + original source should compile without errors");
    }

    [Fact]
    public void IOptionsMonitorFixture_VerifyCompiles()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Options;

            public class AppConfig { public string Environment { get; set; } }

            public partial class MonitorService
            {
                private readonly IOptionsMonitor<AppConfig> _options;

                public MonitorService(IOptionsMonitor<AppConfig> options)
                {
                    _options = options;
                }
            }

            [Cover<MonitorService>]
            public partial class MonitorServiceTests { }
            """;

        // Act
        var genResult = GenerateFixture(source);
        var compileResult = TestHelper.VerifyCompiles(source, genResult);

        // Assert
        var errors = compileResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty("generated IOptionsMonitor fixture should compile without errors");
    }

    [Fact]
    public void TimeProviderFixture_VerifyCompiles()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using System;

            public partial class TimeService
            {
                private readonly TimeProvider _timeProvider;

                public TimeService(TimeProvider timeProvider)
                {
                    _timeProvider = timeProvider;
                }
            }

            [Cover<TimeService>]
            public partial class TimeServiceTests { }
            """;

        // Act
        var genResult = GenerateFixture(source);
        var compileResult = TestHelper.VerifyCompiles(source, genResult);

        // Assert
        var errors = compileResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty("generated TimeProvider fixture should compile without errors");
    }

    #endregion

    private static void ConfigureMockIConfiguration(Mock<IConfiguration> configuration, params (string Key, object? Value)[] values)
    {
        var map = values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

        IConfigurationSection BuildSection(string path)
        {
            var section = new Mock<IConfigurationSection>();
            var key = path.Contains(':') ? path[(path.LastIndexOf(':') + 1)..] : path;
            section.SetupGet(x => x.Key).Returns(key);
            section.SetupGet(x => x.Path).Returns(path);
            section.SetupGet(x => x.Value).Returns(() => map.TryGetValue(path, out var val) ? val?.ToString() : null);
            section.Setup(x => x.GetSection(It.IsAny<string>()))
                .Returns((string childKey) => BuildSection(string.IsNullOrEmpty(path) ? childKey : $"{path}:{childKey}"));
            section.Setup(x => x.GetChildren())
                .Returns(() =>
                {
                    var prefix = string.IsNullOrEmpty(path) ? string.Empty : path + ":";
                    return map.Keys
                        .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        .Select(k => k[prefix.Length..])
                        .Where(k => k.Length > 0)
                        .Select(k => k.Contains(':') ? k[..k.IndexOf(':')] : k)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(childKey => BuildSection(string.IsNullOrEmpty(path) ? childKey : $"{path}:{childKey}"))
                        .ToArray();
                });
            return section.Object;
        }

        configuration.Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => map.TryGetValue(key, out var val) ? val?.ToString() : null);
        configuration.Setup(x => x.GetSection(It.IsAny<string>()))
            .Returns((string key) => BuildSection(key));
    }
}
