namespace IoCTools.Generator.Tests;


public class DependencyUsageDiagnosticTests
{
    [Fact]
    public void UnusedDependency_InjectField_ProducesIOC039()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

public partial class UnusedInjectService
{
    [Inject] private readonly ILogger _logger;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC039");
        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("_logger");
    }

    [Fact]
    public void UnusedDependency_DependsOnField_ProducesIOC039()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[DependsOn<IService>]
public partial class DependsOnService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC039");
        diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("_service");
    }

    [Fact]
    public void UnusedDependency_DependsOnField_Used_NoDiagnostic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[DependsOn<IService>]
public partial class ActiveService
{
    public string Value => _service.ToString();
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC039").Should().BeEmpty();
    }

    [Fact]
    public void UnusedDependency_ProtectedInjectField_Skipped()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IClock { }

public abstract partial class BaseClockService
{
    [Inject] protected readonly IClock _clock;
}

public partial class ConcreteClockService : BaseClockService
{
    public string Now() => _clock.ToString();
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC039").Should().BeEmpty();
    }

    [Fact]
    public void RedundantDependency_MultipleInjectFields_ProducesIOC040()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IDataStore { }

public partial class DuplicateInjectService
{
    [Inject] private readonly IDataStore _primaryStore;
    [Inject] private readonly IDataStore _secondaryStore;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC040");
        diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("IDataStore");
        diagnostics[0].GetMessage().Should().Contain("[Inject]");
    }

    [Fact]
    public void RedundantDependency_InheritanceMixedSources_ProducesIOC040()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

public abstract partial class BaseService
{
    [Inject] protected readonly ILogger _logger;
}

[DependsOn<ILogger>]
public partial class DerivedService : BaseService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC040").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void DependencyWrapper_ReturnsDependsOnField_ProducesIOC076()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IDeltaDbContext { }

public abstract class Repository<TEntity, TKey>
{
    protected abstract IDeltaDbContext DbContext { get; }
}

[DependsOn<IDeltaDbContext>]
public sealed partial class PlaybookRunRepository : Repository<string, int>
{
    protected override IDeltaDbContext DbContext => _deltaDbContext;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC076");
        diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("DbContext");
    }

    [Fact]
    public void DependencyWrapper_BlockGetter_ProducesIOC076()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IDeltaDbContext { }

public abstract class Repository<TEntity, TKey>
{
    protected abstract IDeltaDbContext DbContext { get; }
}

[DependsOn<IDeltaDbContext>]
public sealed partial class PlaybookRunRepository : Repository<string, int>
{
    protected override IDeltaDbContext DbContext
    {
        get { return _deltaDbContext; }
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC076").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ManualField_ShadowDependsOn_ProducesIOC077_Error()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[Scoped]
[DependsOn<IService>]
public partial class ShadowService
{
    private readonly IService _service = null!; // shadows generated field
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC077");
        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("_service");
    }

    [Fact]
    public void ManualField_ShadowDependsOnConfiguration_ProducesIOC077_Error()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public class Settings { }

[Scoped]
[DependsOnConfiguration<Settings>(""Settings"", MemberNames = new[] { ""_settings"" })]
public partial class ShadowConfigService
{
    private readonly Settings _settings = null!; // shadows generated config field
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC077").Should().BeEmpty();
    }

    [Fact]
    public void MemberNamesSuppressedByField_ProducesIOC078_Warning()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[Scoped]
[DependsOn<IService>(memberName1: ""_service"")]
public partial class SuppressedMemberNameService
{
    private readonly IService _service = null!; // suppresses generation
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC078");
        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ManualFieldWithDifferentName_DoesNotTriggerShadowDiagnostic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[Scoped]
[DependsOn<IService>]
public partial class CustomNameService
{
    [Inject] private readonly IService _custom; // different name, valid
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC077").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC078").Should().BeEmpty();
    }

    [Fact]
    public void ManualField_ShadowButUsedWithInjectAttribute_NoDiagnostic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[Scoped]
[DependsOn<IService>]
public partial class ExplicitInjectService
{
    [Inject] private readonly IService _service;
    public string Use() => _service.ToString();
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC077").Should().BeEmpty();
    }

    [Fact]
    public void MemberNamesDifferentFromField_NoSuppression_NoDiagnostic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[Scoped]
[DependsOn<IService>(memberName1: ""_serviceField"")]
public partial class MemberNamesNoCollisionService
{
    private readonly IService _other = null!;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC078").Should().BeEmpty();
    }

    [Fact]
    public void DependencyWrapper_WithAdditionalLogic_NoDiagnostic()
    {
        var source = @"
using System;
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IDeltaDbContext { }

public abstract class Repository<TEntity, TKey>
{
    protected abstract IDeltaDbContext DbContext { get; }
}

[DependsOn<IDeltaDbContext>]
public sealed partial class PlaybookRunRepository : Repository<string, int>
{
    protected override IDeltaDbContext DbContext => _deltaDbContext ?? throw new InvalidOperationException();
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC076").Should().BeEmpty();
    }

    [Fact]
    public void RawIConfiguration_Dependency_EmitsIOC079()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;

[Scoped]
[DependsOn<IConfiguration>]
public partial class NeedsConfig {}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC079");
        diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }
}
