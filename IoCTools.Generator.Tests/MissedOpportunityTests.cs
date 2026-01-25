namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

public class MissedOpportunityTests
{
    [Fact]
    public void ManualConstructor_WithInterfaces_SuggestsDependsOnAndLifetime()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }
public interface ILogger { }

public partial class ManualService
{
    private readonly IRepo _repo;
    private readonly ILogger _logger;

    public ManualService(IRepo repo, ILogger logger)
    {
        _repo = repo;
        _logger = logger;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
        suggestions[0].Severity.Should().Be(DiagnosticSeverity.Info);
        suggestions[0].GetMessage().Should().Contain("ManualService");
        suggestions[0].GetMessage().Should().Contain("IRepo");
        suggestions[0].GetMessage().Should().Contain("ILogger");
    }

    [Fact]
    public void ManualConstructor_WithValueTypes_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public partial class ValueTypeCtor
{
    public ValueTypeCtor(int count) { }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void ManualConstructor_WithLifetimeAttribute_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

[Scoped]
public partial class AlreadyRegistered
{
    private readonly IRepo _repo;

    public AlreadyRegistered(IRepo repo)
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void ManualConstructor_WithSkipRegistrationAttribute_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

[SkipRegistration]
public partial class SkippedService
{
    private readonly IRepo _repo;

    public SkippedService(IRepo repo)
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void ManualConstructor_WithExternalServiceAttribute_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

[ExternalService]
public partial class ExternallyRegistered
{
    private readonly IRepo _repo;

    public ExternallyRegistered(IRepo repo)
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void ManualConstructor_WithBaseCallWithArguments_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }
public interface ILogger { }

public abstract class BaseService
{
    protected BaseService(ILogger logger) { }
}

public partial class DerivedService : BaseService
{
    private readonly IRepo _repo;

    public DerivedService(IRepo repo, ILogger logger) : base(logger)
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should not suggest because constructor calls base(...) with arguments
        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void AbstractClass_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

public abstract partial class AbstractService
{
    protected AbstractService(IRepo repo) { }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void ClassWithDependsOnAttribute_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }
public interface ILogger { }

[DependsOn<IRepo>]
public partial class AlreadyUsingDependsOn
{
    private readonly ILogger _logger;

    public AlreadyUsingDependsOn(ILogger logger)
    {
        _logger = logger;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void ClassWithInjectField_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }
public interface ILogger { }

public partial class UsingInject
{
    [Inject] private readonly IRepo _repo;
    private readonly ILogger _logger;

    public UsingInject(ILogger logger)
    {
        _logger = logger;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void ParameterlessConstructor_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public partial class NoParamsCtor
{
    public NoParamsCtor() { }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void MixedParameters_ValueAndReference_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

public partial class MixedParamsCtor
{
    public MixedParamsCtor(IRepo repo, int count) { }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should not suggest because not all params are injectable services
        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void ClassWithStringParameter_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

public partial class StringParamCtor
{
    public StringParamCtor(IRepo repo, string name) { }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // String has SpecialType.System_String, so it should be excluded
        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void PrivateConstructor_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

public partial class PrivateCtor
{
    private PrivateCtor(IRepo repo) { }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void InternalConstructor_SuggestsDependsOn()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

public partial class InternalCtor
{
    internal InternalCtor(IRepo repo) { }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Internal constructors are valid for DI
        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
        suggestions[0].Severity.Should().Be(DiagnosticSeverity.Info);
        suggestions[0].GetMessage().Should().Contain("InternalCtor");
    }

    [Fact]
    public void ClassWithRegisterAsAttribute_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }
public interface IMyService { }

[RegisterAs<IMyService>]
public partial class UsingRegisterAs : IMyService
{
    private readonly IRepo _repo;

    public UsingRegisterAs(IRepo repo)
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void ClassWithRegisterAsAllAttribute_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }
public interface IMyService { }

[Scoped]
[RegisterAsAll]
public partial class UsingRegisterAsAll : IMyService
{
    private readonly IRepo _repo;

    public UsingRegisterAsAll(IRepo repo)
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void ClassWithConditionalServiceAttribute_DoesNotSuggest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

[Scoped]
[ConditionalService(Environment = ""Production"")]
public partial class ConditionalSvc
{
    private readonly IRepo _repo;

    public ConditionalSvc(IRepo repo)
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void MultipleConstructors_FirstWithParams_SuggestsDependsOn()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

public partial class MultiCtor
{
    private readonly IRepo? _repo;

    public MultiCtor() { }

    public MultiCtor(IRepo repo)
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should suggest because there's a constructor with injectable params
        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
        suggestions[0].Severity.Should().Be(DiagnosticSeverity.Info);
    }

    [Fact]
    public void NonPartialClass_StillSuggests()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

public class NonPartialService
{
    private readonly IRepo _repo;

    public NonPartialService(IRepo repo)
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Info-level suggestion even for non-partial since user can make it partial
        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
        suggestions[0].Severity.Should().Be(DiagnosticSeverity.Info);
    }
}
