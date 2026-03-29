namespace IoCTools.FluentValidation.Tests;

using System.Linq;

using FluentAssertions;

/// <summary>
/// Tests for validator registration code generation — verifying the correct
/// DI registration lines are generated for discovered validators.
/// </summary>
public sealed class RegistrationTests
{
    #region Registration Format

    [Fact]
    public void ScopedValidator_GeneratesIValidatorAndConcreteRegistration()
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
            .First(t => t.FilePath.Contains("FluentValidation"));
        var code = fvGenerated.GetText().ToString();
        code.Should().Contain("AddScoped<global::FluentValidation.IValidator<");
        code.Should().Contain("OrderCommandValidator");
        code.Should().Contain("AddScoped<global::TestApp.OrderCommandValidator>();");
    }

    [Fact]
    public void GeneratedCode_DoesNotContainNonGenericIValidator()
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
            .First(t => t.FilePath.Contains("FluentValidation"));
        var code = fvGenerated.GetText().ToString();
        // Should contain IValidator<T> but NOT standalone IValidator without generic
        code.Should().Contain("IValidator<");
        // Lines with just "IValidator," or "IValidator>" without "<" before it are non-generic
        var lines = code.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("IValidator") && !line.Contains("IValidator<"))
            {
                // Allow the using statement for FluentValidation namespace
                if (!line.TrimStart().StartsWith("using"))
                {
                    line.Should().NotContain("IValidator", "non-generic IValidator should not be registered");
                }
            }
        }
    }

    [Fact]
    public void GeneratedCode_DoesNotContainIEnumerableIValidationRule()
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
            .First(t => t.FilePath.Contains("FluentValidation"));
        var code = fvGenerated.GetText().ToString();
        code.Should().NotContain("IEnumerable<IValidationRule>");
    }

    #endregion

    #region Multiple Validators

    [Fact]
    public void TwoValidators_BothAppearInSamePartialMethodBody()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using FluentValidation;

namespace TestApp;

public class OrderCommand { }
public class UserCommand { }

[Scoped]
public partial class OrderCommandValidator : AbstractValidator<OrderCommand>
{
    public OrderCommandValidator() { }
}

[Singleton]
public partial class UserCommandValidator : AbstractValidator<UserCommand>
{
    public UserCommandValidator() { }
}
";

        // Act
        var result = TestHelper.Generate(source);

        // Assert
        var fvGenerated = result.GeneratedTrees
            .First(t => t.FilePath.Contains("FluentValidation"));
        var code = fvGenerated.GetText().ToString();
        code.Should().Contain("OrderCommandValidator");
        code.Should().Contain("UserCommandValidator");
        code.Should().Contain("static partial void Add");
    }

    #endregion

    #region Namespace Derivation

    [Fact]
    public void GeneratedPartialMethod_UsesCorrectNamespaceDerivedFromAssemblyName()
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

        // Assert — assembly name is "Test" (set in TestHelper), so namespace is Test.Extensions.Generated
        var fvGenerated = result.GeneratedTrees
            .First(t => t.FilePath.Contains("FluentValidation"));
        var code = fvGenerated.GetText().ToString();
        code.Should().Contain("namespace Test.Extensions.Generated;");
        code.Should().Contain("GeneratedServiceCollectionExtensions");
    }

    #endregion
}
