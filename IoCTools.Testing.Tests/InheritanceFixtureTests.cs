using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;
using Xunit;

namespace IoCTools.Testing.Tests;

public class InheritanceFixtureTests
{
    [Fact]
    public void Cover_Attribute_Includes_Base_Constructor_Dependencies()
    {
        // Arrange
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;

            [Scoped]
            public partial class BaseService
            {
                [Inject] private readonly ILogger<BaseService> _baseLogger;
                [Inject] private readonly IConfiguration _config;
            }

            public interface ILogger<T> { }
            public interface IConfiguration { }

            [Scoped]
            public partial class DerivedService : BaseService
            {
                [Inject] private readonly IDataRepository _repository;
            }

            public interface IDataRepository { }

            [Cover<DerivedService>]
            public partial class DerivedServiceTests
            {
            }
            """;

        // Act
        var result = TestHelper.Generate(source);
        var generatedCode = result.GeneratedTrees.FirstOrDefault()?.ToString() ?? "";

        // Assert
        // Should include both base and derived dependencies
        generatedCode.Should().Contain("_mockBaseLogger");
        generatedCode.Should().Contain("_mockConfig");
        generatedCode.Should().Contain("_mockRepository");

        // CreateSut should wire all parameters
        generatedCode.Should().Contain("CreateSut");
    }

    [Fact]
    public void Cover_Attribute_Handles_Multi_Level_Inheritance()
    {
        // Arrange - 3-level hierarchy
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;

            [Scoped]
            public partial class Level1Service
            {
                [Inject] private readonly IDependency1 _dep1;
            }

            public interface IDependency1 { }

            [Scoped]
            public partial class Level2Service : Level1Service
            {
                [Inject] private readonly IDependency2 _dep2;
            }

            public interface IDependency2 { }

            [Scoped]
            public partial class Level3Service : Level2Service
            {
                [Inject] private readonly IDependency3 _dep3;
            }

            public interface IDependency3 { }

            [Cover<Level3Service>]
            public partial class Level3ServiceTests
            {
            }
            """;

        // Act
        var result = TestHelper.Generate(source);
        var generatedCode = result.GeneratedTrees.FirstOrDefault()?.ToString() ?? "";

        // Assert - All 3 levels should be represented
        generatedCode.Should().Contain("_mockDep1");
        generatedCode.Should().Contain("_mockDep2");
        generatedCode.Should().Contain("_mockDep3");
    }
}
