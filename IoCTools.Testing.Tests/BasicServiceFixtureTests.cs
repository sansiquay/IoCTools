using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;
using Microsoft.CodeAnalysis;
using Xunit;

namespace IoCTools.Testing.Tests;

public class BasicServiceFixtureTests
{
    [Fact]
    public void Cover_Attribute_Generates_Mock_Fields()
    {
        // Arrange
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;

            [Scoped]
            public partial class UserService
            {
                [Inject] private readonly IUserRepository _userRepository;
                [Inject] private readonly ILogger<UserService> _logger;
            }

            public interface IUserRepository { }
            public interface ILogger<T> { }

            [Cover<UserService>]
            public partial class UserServiceTests
            {
            }
            """;

        // Act
        var result = TestHelper.Generate(source);

        // Assert
        var generated = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Fixture.g.cs") || t.FilePath.Contains("IoCToolsTestingGenerator"));
        generated.Should().NotBeNull("generator should produce fixture output");

        var generatedCode = generated!.ToString();
        generatedCode.Should().Contain("_mockUserRepository");
        generatedCode.Should().Contain("_mockLogger");
        generatedCode.Should().Contain("Mock<IUserRepository>");
        generatedCode.Should().Contain("Mock<ILogger<UserService>>");
    }

    [Fact]
    public void Cover_Attribute_Generates_CreateSut_Method()
    {
        // Arrange
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;

            [Scoped]
            public partial class UserService
            {
                [Inject] private readonly IUserRepository _userRepository;
            }

            public interface IUserRepository { }

            [Cover<UserService>]
            public partial class UserServiceTests
            {
            }
            """;

        // Act
        var result = TestHelper.Generate(source);

        // Assert
        var generatedCode = result.GeneratedTrees.FirstOrDefault()?.ToString() ?? "";
        generatedCode.Should().Contain("CreateSut");
        generatedCode.Should().Contain("_mockUserRepository.Object");
    }

    [Fact]
    public void Cover_Attribute_Generates_Setup_Helper_Methods()
    {
        // Arrange
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;

            [Scoped]
            public partial class OrderService
            {
                [Inject] private readonly IOrderRepository _orderRepository;
                [Inject] private readonly INotificationService _notificationService;
            }

            public interface IOrderRepository { }
            public interface INotificationService { }

            [Cover<OrderService>]
            public partial class OrderServiceTests
            {
            }
            """;

        // Act
        var result = TestHelper.Generate(source);

        // Assert
        var generatedCode = result.GeneratedTrees.FirstOrDefault()?.ToString() ?? "";
        generatedCode.Should().Contain("Setup");
    }
}
