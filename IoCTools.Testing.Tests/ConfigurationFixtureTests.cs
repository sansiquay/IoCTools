using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace IoCTools.Testing.Tests;

public class ConfigurationFixtureTests
{
    [Fact]
    public void Cover_Attribute_Generates_IConfiguration_Helper()
    {
        // Arrange
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Configuration;

            [Scoped]
            public partial class ConfigurableService
            {
                [InjectConfiguration("App:Settings")]
                private readonly string _settings;

                [Inject] private readonly ILogger<ConfigurableService> _logger;
            }

            public interface ILogger<T> { }

            [Cover<ConfigurableService>]
            public partial class ConfigurableServiceTests
            {
            }
            """;

        // Act
        var result = TestHelper.Generate(source);
        var generatedCode = result.GeneratedTrees.FirstOrDefault()?.ToString() ?? "";

        // Assert
        generatedCode.Should().Contain("_mockConfiguration");
    }

    [Fact]
    public void Cover_Attribute_Generates_IOptions_Helper()
    {
        // Arrange - service with IOptions<T> config binding
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Options;

            [Scoped]
            public partial class OptionsService
            {
                [InjectConfiguration("Database")]
                private readonly DatabaseOptions _options;
                [Inject] private readonly ILogger<OptionsService> _logger;
            }

            public class DatabaseOptions { public string ConnectionString { get; set; } }
            public interface ILogger<T> { }

            [Cover<OptionsService>]
            public partial class OptionsServiceTests
            {
            }
            """;

        // Act
        var result = TestHelper.Generate(source);
        var generatedCode = result.GeneratedTrees.FirstOrDefault()?.ToString() ?? "";

        // Assert
        generatedCode.Should().Contain("IOptions");
    }
}
