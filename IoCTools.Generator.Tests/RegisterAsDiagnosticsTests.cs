namespace IoCTools.Generator.Tests;

/// <summary>
///     Diagnostic validation tests for RegisterAs<T1, T2, T3> selective interface registration
/// </summary>
public class RegisterAsDiagnosticsTests
{
    #region Error Cases and Diagnostics

    [Fact]
    public void RegisterAs_WithLifetimeInference_WorksCorrectly()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestApp
{
    public interface IService { }

    [Scoped]
    [RegisterAs<IService>]
    public partial class SmartService : IService
    {
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // With intelligent inference, this should now work without requiring explicit Service attribute
        result.Diagnostics.Any(d => d.Id == "IOC028").Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();
        // Should register both concrete class and interface
        registrationContent.Should()
            .Contain("services.AddScoped<global::TestApp.SmartService, global::TestApp.SmartService>");
        registrationContent.Should()
            .Contain("services.AddScoped<global::TestApp.IService, global::TestApp.SmartService>");
    }

    [Fact]
    public void RegisterAsInterfaceNotImplemented_GeneratesIOC029Diagnostic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestApp
{
    public interface IImplementedService { }
    public interface INotImplementedService { }

    [Scoped]
    [RegisterAs<IImplementedService, INotImplementedService>]
    public partial class PartialImplementationService : IImplementedService
    {
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should report IOC029 diagnostic for not implemented interface
        result.Diagnostics.Any(d => d.Id == "IOC029").Should().BeTrue();
    }

    [Fact]
    public void RegisterAsDuplicateInterface_GeneratesIOC030Diagnostic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestApp
{
    public interface IService { }

    [Scoped]
    [RegisterAs<IService, IService>]
    public partial class DuplicateService : IService
    {
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should report IOC030 diagnostic for duplicate interface
        result.Diagnostics.Any(d => d.Id == "IOC030").Should().BeTrue();
    }

    [Fact]
    public void RegisterAsNonInterfaceType_GeneratesIOC031Diagnostic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestApp
{
    public interface IService { }
    public class ConcreteClass { }

    [Scoped]
    [RegisterAs<IService, ConcreteClass>]
    public partial class BadTypeService : IService
    {
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should report IOC031 diagnostic for non-interface type
        result.Diagnostics.Any(d => d.Id == "IOC031").Should().BeTrue();
    }

    #endregion
}
