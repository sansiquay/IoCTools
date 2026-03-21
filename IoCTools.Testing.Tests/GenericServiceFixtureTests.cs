using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;
using Microsoft.CodeAnalysis;
using Xunit;

namespace IoCTools.Testing.Tests;

/// <summary>
/// Tests for generic service fixture generation scenarios.
/// </summary>
public class GenericServiceFixtureTests
{
    [Fact]
    public void Cover_Attribute_With_Generic_Dependencies_Processes_Successfully()
    {
        // Arrange
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;

            [Scoped]
            public partial class RepositoryService
            {
                [Inject] private readonly IRepository<SampleUser> _userRepository;
                [Inject] private readonly IRepository<SampleOrder> _orderRepository;
                [Inject] private readonly ILogger<RepositoryService> _logger;
            }

            public interface IRepository<T> { }
            public class SampleUser { }
            public class SampleOrder { }
            public interface ILogger<T> { }

            [Cover<RepositoryService>]
            public partial class RepositoryServiceTests
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
    public void Cover_Attribute_With_Generic_Class_Service_Processes_Successfully()
    {
        // Arrange
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;

            [Scoped]
            public partial class GenericHandler<T>
            {
                [Inject] private readonly IRepository<T> _repository;
                [Inject] private readonly ILogger<GenericHandler<T>> _logger;
            }

            public interface IRepository<T> { }
            public interface ILogger<T> { }

            [Cover<GenericHandler<SampleUser>>]
            public partial class UserHandlerTests
            {
            }

            public class SampleUser { }
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
