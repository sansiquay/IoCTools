using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;
using Xunit;

namespace IoCTools.Testing.Tests;

public class GenericServiceFixtureTests
{
    [Fact]
    public void Cover_Attribute_Handles_Generic_Service_Dependencies()
    {
        // Arrange
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;

            [Scoped]
            public partial class RepositoryService
            {
                [Inject] private readonly IRepository<User> _userRepository;
                [Inject] private readonly IRepository<Order> _orderRepository;
                [Inject] private readonly ILogger<RepositoryService> _logger;
            }

            public interface IRepository<T> { }
            public class User { }
            public class Order { }
            public interface ILogger<T> { }

            [Cover<RepositoryService>]
            public partial class RepositoryServiceTests
            {
            }
            """;

        // Act
        var result = TestHelper.Generate(source);
        var generatedCode = result.GeneratedTrees.FirstOrDefault()?.ToString() ?? "";

        // Assert
        // Generic types should produce unique mock names
        generatedCode.Should().Contain("_mock");
        generatedCode.Should().Contain("IRepository");
        generatedCode.Should().Contain("ILogger");
    }

    [Fact]
    public void Cover_Attribute_Handles_GenericClass_Service()
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

            [Cover<GenericHandler<User>>]
            public partial class UserHandlerTests
            {
            }

            public class User { }
            """;

        // Act
        var result = TestHelper.Generate(source);
        var generatedCode = result.GeneratedTrees.FirstOrDefault()?.ToString() ?? "";

        // Assert
        generatedCode.Should().Contain("CreateSut");
        generatedCode.Should().Contain("GenericHandler<User>");
    }
}
