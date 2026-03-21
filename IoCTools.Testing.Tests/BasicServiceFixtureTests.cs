using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using IoCTools.Testing.Annotations;
using Microsoft.CodeAnalysis;
using Xunit;

namespace IoCTools.Testing.Tests;

/// <summary>
/// Tests for basic service fixture generation scenarios.
/// Note: Full fixture code generation requires the main IoCTools.Generator
/// to first generate constructors for services. These tests verify the
/// test fixture generator can be instantiated and runs without errors.
/// </summary>
public class BasicServiceFixtureTests
{
    [Fact]
    public void TestHelper_Can_Run_Generators_Successfully()
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
        // Verify that both generators ran
        result.GeneratedTrees.Should().NotBeEmpty("generators should produce output");

        // Check for any blocking errors (Info/Warning diagnostics and expected IOC001 for missing interfaces are OK)
        var blockingErrors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error && !d.Id.StartsWith("TDIAG") && d.Id != "IOC001")
            .ToList();

        blockingErrors.Should().BeEmpty("no blocking errors should be generated");
    }

    [Fact]
    public void Cover_Attribute_With_Valid_Service_Processes_Successfully()
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
        result.GeneratedTrees.Should().NotBeEmpty();
    }

    [Fact]
    public void Multiple_Cover_Attributes_Can_Be_Processed()
    {
        // Arrange
        var source = """
            using IoCTools.Abstractions.Annotations;
            using IoCTools.Testing.Annotations;

            [Scoped]
            public partial class ServiceA
            {
                [Inject] private readonly ILogger<ServiceA> _logger;
            }

            [Scoped]
            public partial class ServiceB
            {
                [Inject] private readonly ILogger<ServiceB> _logger;
            }

            public interface ILogger<T> { }

            [Cover<ServiceA>]
            public partial class ServiceATests { }

            [Cover<ServiceB>]
            public partial class ServiceBTests { }
            """;

        // Act
        var result = TestHelper.Generate(source);

        // Assert
        result.GeneratedTrees.Should().NotBeEmpty();
    }
}
