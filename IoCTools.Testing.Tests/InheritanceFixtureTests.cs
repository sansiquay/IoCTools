using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;
using Microsoft.CodeAnalysis;
using Xunit;

namespace IoCTools.Testing.Tests;

/// <summary>
/// Tests for inheritance hierarchy fixture generation scenarios.
/// </summary>
public class InheritanceFixtureTests
{
    [Fact]
    public void Cover_Attribute_With_Derived_Service_Processes_Successfully()
    {
        // Arrange
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;

            [Scoped]
            public partial class BaseService
            {
                [Inject] private readonly ILogger<BaseService> _baseLogger;
                [Inject] private readonly IAppConfiguration _config;
            }

            public interface ILogger<T> { }
            public interface IAppConfiguration { }

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

        // Assert
        result.GeneratedTrees.Should().NotBeEmpty();
        var blockingErrors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error && !d.Id.StartsWith("TDIAG") && d.Id != "IOC001")
            .ToList();
        blockingErrors.Should().BeEmpty();
    }

    [Fact]
    public void Cover_Attribute_With_Multi_Level_Inheritance_Processes_Successfully()
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

        // Assert
        result.GeneratedTrees.Should().NotBeEmpty();
        var blockingErrors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error && !d.Id.StartsWith("TDIAG") && d.Id != "IOC001")
            .ToList();
        blockingErrors.Should().BeEmpty();
    }
}
