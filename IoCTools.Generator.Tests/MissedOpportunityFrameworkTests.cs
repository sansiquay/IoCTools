namespace IoCTools.Generator.Tests;

/// <summary>
///     Tests that the MissedOpportunityValidator correctly excludes framework base types
///     from IOC068 suggestions. These classes have their own registration mechanisms
///     and should not be suggested for IoCTools attributes.
/// </summary>
public class MissedOpportunityFrameworkTests
{
    [Fact]
    public void BackgroundService_DoesNotSuggest()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public interface IRepo { }

public class MyWorker : BackgroundService
{
    private readonly IRepo _repo;

    public MyWorker(IRepo repo)
    {
        _repo = repo;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // BackgroundService is a framework type, should not suggest
        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void IHostedService_DoesNotSuggest()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public interface IRepo { }

public class MyHostedService : IHostedService
{
    private readonly IRepo _repo;

    public MyHostedService(IRepo repo)
    {
        _repo = repo;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // IHostedService is a framework interface, should not suggest
        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }

    [Fact]
    public void ClassInheritingFromFrameworkType_WithBaseCall_DoesNotSuggest()
    {
        // This tests the `: base(...)` detection for custom framework inheritance patterns
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }
public interface ILogger { }

// Simulate a framework base type with required constructor parameters
public abstract class FrameworkBase
{
    protected FrameworkBase(ILogger logger) { }
}

public class MyDerivedClass : FrameworkBase
{
    private readonly IRepo _repo;

    public MyDerivedClass(IRepo repo, ILogger logger) : base(logger)
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
    public void ClassWithParameterlessBaseCall_SuggestsDependsOn()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

// Base class with parameterless constructor
public abstract class SimpleBase
{
    protected SimpleBase() { }
}

public class MyDerivedClass : SimpleBase
{
    private readonly IRepo _repo;

    public MyDerivedClass(IRepo repo) : base()
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should suggest because base() is called with no arguments - this is not a framework integration pattern
        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
    }

    [Fact]
    public void ClassWithImplicitBaseCall_SuggestsDependsOn()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

// Base class with parameterless constructor (implicit)
public abstract class SimpleBase { }

public class MyDerivedClass : SimpleBase
{
    private readonly IRepo _repo;

    public MyDerivedClass(IRepo repo)
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should suggest because there's no explicit base() call with arguments
        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
    }

    [Fact]
    public void ClassWithThisConstructorCall_SuggestsDependsOn()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }

public class MultiCtorClass
{
    private readonly IRepo _repo;

    public MultiCtorClass() : this(null!) { }

    public MultiCtorClass(IRepo repo)
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should suggest - `: this(...)` is not a framework integration pattern
        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
    }

    [Fact]
    public void RegularServiceClass_SuggestsDependsOn()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepo { }
public interface ILogger { }

public class RegularService
{
    private readonly IRepo _repo;
    private readonly ILogger _logger;

    public RegularService(IRepo repo, ILogger logger)
    {
        _repo = repo;
        _logger = logger;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should suggest because this is a regular service class
        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
        suggestions[0].GetMessage().Should().Contain("RegularService");
    }

    [Fact]
    public void FallbackServicePattern_WithThisCall_StillSuggests()
    {
        // Mimics FallbackMcpContextPinStore pattern - has a parameterless ctor that calls this()
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IClock { }
public interface IMcpContextPinStore { }

public sealed class FallbackMcpContextPinStore : IMcpContextPinStore
{
    private readonly IClock _clock;

    public FallbackMcpContextPinStore() : this(null!) { }

    public FallbackMcpContextPinStore(IClock clock) => _clock = clock;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should suggest because this has a constructor with injectable params
        // The `: this(...)` call on the parameterless constructor doesn't exclude it
        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
        suggestions[0].GetMessage().Should().Contain("FallbackMcpContextPinStore");
        suggestions[0].GetMessage().Should().Contain("IClock");
    }

    [Fact]
    public void FileWithMixedClasses_DetectsFallbackStore()
    {
        // Mimics McpContextPinStore.cs which has both DefaultMcpContextPinStore (uses IoCTools)
        // and FallbackMcpContextPinStore (does not)
        const string source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IClock { }
public interface ILogger { }
public interface IMcpContextPinStore { }
public interface IMcpContextPinService { }

// This class USES IoCTools - should NOT trigger IOC068
[DependsOn<IMcpContextPinService, IClock, ILogger>]
public sealed partial class DefaultMcpContextPinStore : IMcpContextPinStore
{
    // Implementation using generated fields
}

// This class does NOT use IoCTools - SHOULD trigger IOC068
public sealed class FallbackMcpContextPinStore : IMcpContextPinStore
{
    private readonly IClock _clock;

    public FallbackMcpContextPinStore() : this(null!) { }

    public FallbackMcpContextPinStore(IClock clock) => _clock = clock;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should only suggest for FallbackMcpContextPinStore, not DefaultMcpContextPinStore
        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
        suggestions[0].GetMessage().Should().Contain("FallbackMcpContextPinStore");
        suggestions[0].GetMessage().Should().NotContain("DefaultMcpContextPinStore");
    }

    [Fact]
    public void ExactDeltaScenario_WithExpressionBody_Detects()
    {
        // Exact copy of delta's FallbackMcpContextPinStore pattern
        const string source = @"
using System;
using System.Collections.Concurrent;
using IoCTools.Abstractions.Annotations;

namespace Delta.Api.Mcp;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IMcpContextPinStore { }

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class FallbackMcpContextPinStore : IMcpContextPinStore
{
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<Guid, object> _pins = new();

    public FallbackMcpContextPinStore()
        : this(new SystemClock())
    {
    }

    public FallbackMcpContextPinStore(IClock clock) => _clock = clock;
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should suggest for FallbackMcpContextPinStore
        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
        suggestions[0].GetMessage().Should().Contain("FallbackMcpContextPinStore");
        suggestions[0].GetMessage().Should().Contain("IClock");
    }

    [Fact]
    public void MultipleServicesInFile_OnlySuggestsForEligible()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public interface IRepo { }
public interface ILogger { }

// Should NOT suggest - BackgroundService
public class MyWorker : BackgroundService
{
    private readonly IRepo _repo;
    public MyWorker(IRepo repo) { _repo = repo; }
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

// Should NOT suggest - already has [Scoped]
[Scoped]
public partial class ScopedService
{
    private readonly IRepo _repo;
    public ScopedService(IRepo repo) { _repo = repo; }
}

// SHOULD suggest - regular service
public class RegularService
{
    private readonly IRepo _repo;
    public RegularService(IRepo repo) { _repo = repo; }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var suggestions = result.GetDiagnosticsByCode("IOC068");
        suggestions.Should().ContainSingle();
        suggestions[0].GetMessage().Should().Contain("RegularService");
    }

    [Fact]
    public void DeepInheritanceChain_WithFrameworkBase_DoesNotSuggest()
    {
        const string source = @"
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public interface IRepo { }

// Intermediate class in the inheritance chain
public abstract class IntermediateWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

// Final class that should NOT be suggested
public class MyWorker : IntermediateWorker
{
    private readonly IRepo _repo;

    public MyWorker(IRepo repo)
    {
        _repo = repo;
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should not suggest - inherits from BackgroundService via IntermediateWorker
        result.GetDiagnosticsByCode("IOC068").Should().BeEmpty();
    }
}
