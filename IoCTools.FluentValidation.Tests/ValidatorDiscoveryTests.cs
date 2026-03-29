namespace IoCTools.FluentValidation.Tests;

using System.Linq;

using FluentAssertions;

/// <summary>
/// Tests for validator discovery pipeline — verifying which classes are discovered
/// as FluentValidation validators requiring DI registration.
/// </summary>
public sealed class ValidatorDiscoveryTests
{
    #region Discovered Validators

    [Fact]
    public void ScopedValidator_WithAbstractValidatorBase_IsDiscovered()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class OrderCommand { }

[Scoped]
public partial class OrderCommandValidator : AbstractValidator<OrderCommand>
{
    public OrderCommandValidator() { }
}
";

        // Act
        var result = TestHelper.Generate(source);

        // Assert
        var fvGenerated = result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains("FluentValidation"));
        fvGenerated.Should().NotBeNull("Scoped validator should be discovered by FV generator");
        var code = fvGenerated!.GetText().ToString();
        code.Should().Contain("OrderCommandValidator");
    }

    [Fact]
    public void SingletonValidator_WithAbstractValidatorBase_IsDiscovered()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class CreateUserCommand { }

[Singleton]
public partial class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator() { }
}
";

        // Act
        var result = TestHelper.Generate(source);

        // Assert
        var fvGenerated = result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains("FluentValidation"));
        fvGenerated.Should().NotBeNull("Singleton validator should be discovered");
        var code = fvGenerated!.GetText().ToString();
        code.Should().Contain("CreateUserCommandValidator");
        code.Should().Contain("AddSingleton");
    }

    [Fact]
    public void TransientValidator_WithAbstractValidatorBase_IsDiscovered()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class DeleteCommand { }

[Transient]
public partial class DeleteCommandValidator : AbstractValidator<DeleteCommand>
{
    public DeleteCommandValidator() { }
}
";

        // Act
        var result = TestHelper.Generate(source);

        // Assert
        var fvGenerated = result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains("FluentValidation"));
        fvGenerated.Should().NotBeNull("Transient validator should be discovered");
        var code = fvGenerated!.GetText().ToString();
        code.Should().Contain("DeleteCommandValidator");
        code.Should().Contain("AddTransient");
    }

    #endregion

    #region Not Discovered

    [Fact]
    public void ClassWithLifetimeAttribute_NotInheritingAbstractValidator_IsNotDiscovered()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace TestApp;

public interface IMyService { }

[Scoped]
public partial class MyService : IMyService { }
";

        // Act
        var result = TestHelper.Generate(source);

        // Assert — no FV-generated file should reference MyService
        var fvGenerated = result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains("FluentValidation"));
        if (fvGenerated != null)
        {
            var code = fvGenerated.GetText().ToString();
            code.Should().NotContain("MyService");
        }
    }

    [Fact]
    public void ValidatorWithoutLifetimeAttribute_IsNotDiscovered()
    {
        // Arrange
        var source = @"
using FluentValidation;

namespace TestApp;

public class LoginCommand { }

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator() { }
}
";

        // Act
        var result = TestHelper.Generate(source);

        // Assert — no FV-generated output should exist (no validators with lifetime attributes)
        var fvGenerated = result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains("FluentValidation"));
        fvGenerated.Should().BeNull("validator without lifetime attribute should not be discovered");
    }

    [Fact]
    public void AbstractValidatorClass_WithLifetimeAttribute_IsNotDiscovered()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class BaseCommand { }

[Scoped]
public abstract class BaseCommandValidator : AbstractValidator<BaseCommand>
{
    protected BaseCommandValidator() { }
}
";

        // Act
        var result = TestHelper.Generate(source);

        // Assert
        var fvGenerated = result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains("FluentValidation"));
        fvGenerated.Should().BeNull("abstract validator should not be discovered");
    }

    #endregion
}
