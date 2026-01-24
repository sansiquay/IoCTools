namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     Tests for IOC080: Service class must be partial
///     Validates that classes with code-generating attributes are properly marked as partial.
/// </summary>
public class MissingPartialKeywordTests
{
    #region Happy Path Tests - Partial classes should not trigger IOC080

    [Fact]
    public void PartialClassWithInject_ShouldNotProduceDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

[Scoped]
public partial class MyService
{
    [Inject] private readonly ILogger _logger;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC080");
        diagnostics.Should().BeEmpty("partial class with [Inject] should not trigger IOC080");
    }

    [Fact]
    public void PartialClassWithDependsOn_ShouldNotProduceDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

[Scoped]
[DependsOn<ILogger>]
public partial class MyService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC080");
        diagnostics.Should().BeEmpty("partial class with [DependsOn] should not trigger IOC080");
    }

    [Fact]
    public void PartialClassWithInjectConfiguration_ShouldNotProduceDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

[Scoped]
public partial class MyService
{
    [InjectConfiguration(""AppSettings:ConnectionString"")] private readonly string _connectionString;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC080");
        diagnostics.Should().BeEmpty("partial class with [InjectConfiguration] should not trigger IOC080");
    }

    [Fact]
    public void PartialClassWithDependsOnConfiguration_ShouldNotProduceDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

[Scoped]
[DependsOnConfiguration<string>(""AppSettings:ApiKey"")]
public partial class MyService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC080");
        diagnostics.Should().BeEmpty("partial class with [DependsOnConfiguration] should not trigger IOC080");
    }

    [Fact]
    public void ClassWithOnlyLifetimeAttribute_ShouldNotProduceDiagnostic()
    {
        // Arrange - a class with only [Scoped] doesn't need code generation if it has no dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IMyService { }

[Scoped]
public class MyService : IMyService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC080");
        diagnostics.Should().BeEmpty("class with only lifetime attribute should not trigger IOC080");
    }

    #endregion

    #region Sad Path Tests - Non-partial classes should trigger IOC080

    [Fact]
    public void NonPartialClassWithInject_ShouldProduceIOC080()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

[Scoped]
public class MyService
{
    [Inject] private readonly ILogger _logger;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC080");
        diagnostics.Should().ContainSingle("non-partial class with [Inject] should trigger IOC080");

        var diagnostic = diagnostics[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("MyService");
        diagnostic.GetMessage().Should().Contain("[Inject]");
        diagnostic.GetMessage().Should().Contain("partial");
    }

    [Fact]
    public void NonPartialClassWithDependsOn_ShouldProduceIOC080()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

[Scoped]
[DependsOn<ILogger>]
public class MyService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC080");
        diagnostics.Should().ContainSingle("non-partial class with [DependsOn] should trigger IOC080");

        var diagnostic = diagnostics[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("MyService");
        diagnostic.GetMessage().Should().Contain("[DependsOn]");
        diagnostic.GetMessage().Should().Contain("partial");
    }

    [Fact]
    public void NonPartialClassWithInjectConfiguration_ShouldProduceIOC080()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

[Scoped]
public class MyService
{
    [InjectConfiguration(""AppSettings:ConnectionString"")] private readonly string _connectionString;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC080");
        diagnostics.Should().ContainSingle("non-partial class with [InjectConfiguration] should trigger IOC080");

        var diagnostic = diagnostics[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("MyService");
        diagnostic.GetMessage().Should().Contain("[InjectConfiguration]");
        diagnostic.GetMessage().Should().Contain("partial");
    }

    [Fact]
    public void NonPartialClassWithDependsOnConfiguration_ShouldProduceIOC080()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

[Scoped]
[DependsOnConfiguration<string>(""AppSettings:ApiKey"")]
public class MyService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC080");
        diagnostics.Should().ContainSingle("non-partial class with [DependsOnConfiguration] should trigger IOC080");

        var diagnostic = diagnostics[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("MyService");
        diagnostic.GetMessage().Should().Contain("[DependsOnConfiguration]");
        diagnostic.GetMessage().Should().Contain("partial");
    }

    [Fact]
    public void NonPartialClassWithMultipleCodeGeneratingAttributes_ShouldListAllInMessage()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

[Scoped]
[DependsOn<ILogger>]
public class MyService
{
    [Inject] private readonly ILogger _additionalLogger;
    [InjectConfiguration(""AppSettings:Timeout"")] private readonly int _timeout;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC080");
        diagnostics.Should().ContainSingle("non-partial class with multiple attributes should trigger single IOC080");

        var diagnostic = diagnostics[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("MyService");
        diagnostic.GetMessage().Should().Contain("[Inject]");
        diagnostic.GetMessage().Should().Contain("[DependsOn]");
        diagnostic.GetMessage().Should().Contain("[InjectConfiguration]");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void InterfaceWithDependsOn_ShouldNotProduceDiagnostic()
    {
        // Arrange - interfaces can't be partial, but we don't report IOC080 for them
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

[DependsOn<ILogger>]
public interface IMyService { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC080");
        diagnostics.Should().BeEmpty("interfaces should not trigger IOC080");
    }

    [Fact]
    public void NonPartialNestedClassWithInject_ShouldProduceIOC080()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

public partial class OuterClass
{
    [Scoped]
    public class InnerService
    {
        [Inject] private readonly ILogger _logger;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC080");
        diagnostics.Should().ContainSingle("non-partial nested class should trigger IOC080");

        var diagnostic = diagnostics[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("InnerService");
    }

    #endregion
}
