using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace IoCTools.Testing.Tests;

using System.Collections.Immutable;
using System.Threading.Tasks;

/// <summary>
/// Tests for the 1.9.0 "Cover&lt;T&gt; sharpening" bundle:
/// - P0: ConcreteHandling.ForceMock opt-out for auto-concrete promotion
/// - P1: TestFixturePipeline exact-namespace match (no substring false-positive)
/// - P1: FixtureEmitter static-mutable state removed (parallel-safe)
/// </summary>
public sealed class CoverSharpeningTests
{
    private static TestHelper.GenerationResult GenerateFixture(string source)
    {
        var iocToolsAssembly = typeof(Abstractions.Annotations.ScopedAttribute).Assembly;
        var iocTestingAssembly = typeof(IoCTools.Testing.Annotations.CoverAttribute<>).Assembly;

        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => MetadataReference.CreateFromFile(p))
            .Cast<MetadataReference>()
            .ToList();

        var metadataRefs = new List<MetadataReference>(trustedAssemblies)
        {
            MetadataReference.CreateFromFile(iocToolsAssembly.Location),
            MetadataReference.CreateFromFile(iocTestingAssembly.Location),
        };

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

        var compilation = CSharpCompilation.Create(
            "Test",
            new[] { syntaxTree },
            metadataRefs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable));

        var testingGenerator = new IoCTools.Testing.IoCToolsTestingGenerator();
        var driver = CSharpGeneratorDriver.Create(new[]
            {
                testingGenerator.AsSourceGenerator()
            },
            Array.Empty<AdditionalText>(),
            new CSharpParseOptions(LanguageVersion.Preview));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees
            .Skip(1)
            .ToImmutableArray();

        return new TestHelper.GenerationResult(generatedTrees, diagnostics.ToImmutableArray());
    }

    private static string? GetFixtureSource(ImmutableArray<SyntaxTree> trees, string testClassName)
    {
        var tree = trees.FirstOrDefault(t => t.FilePath.Contains(testClassName) && t.FilePath.EndsWith(".Fixture.g.cs"));
        return tree?.ToString();
    }

    // =========================================================================
    // P0 — ConcreteHandling.ForceMock opt-out
    // =========================================================================

    [Fact]
    public void ConcreteHandling_Auto_Default_Still_Promotes_To_Concrete_Instance()
    {
        // Sanity check: the default behavior is unchanged from 1.8.x.
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }
            public sealed class RequestContext { public string TenantId { get; set; } = ""; }

            public partial class UserService
            {
                public UserService(IUserRepository userRepository, RequestContext context) { }
            }

            [Cover<UserService>]
            public partial class UserServiceTests { }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("private RequestContext RequestContext { get; set; } = new();",
            "Auto (default) should still promote RequestContext to a concrete instance");
        fixtureSource.Should().NotContain("Mock<RequestContext>",
            "Auto mode preserves the prior auto-concrete behavior");
    }

    [Fact]
    public void ConcreteHandling_ForceMock_Substitutes_Mock_For_Concrete_Dependency()
    {
        // P0 finding: opt out of auto-concrete promotion so concrete-class deps mock cleanly.
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }
            public class RequestContext // open class, not sealed — Moq must be able to subclass it
            {
                public virtual string TenantId { get; set; } = "";
            }

            public partial class UserService
            {
                public UserService(IUserRepository userRepository, RequestContext context) { }
            }

            [Cover<UserService>(ConcreteHandling = ConcreteHandling.ForceMock)]
            public partial class UserServiceTests { }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        fixtureSource.Should().NotBeNull("ForceMock fixture should still be emitted");
        fixtureSource.Should().Contain("Mock<RequestContext>",
            "ForceMock should emit RequestContext as a Mock<T> rather than a real instance");
        fixtureSource.Should().Contain("_mockRequestContext",
            "the standard mock field naming convention should apply");
        fixtureSource.Should().NotContain("private RequestContext RequestContext { get; set; } = new();",
            "ForceMock should suppress the auto-concrete promotion path entirely");
        fixtureSource.Should().NotContain("ConfigureRequestContext(Action<RequestContext>",
            "the Configure<T> helper is only emitted for ConcreteInstance role");
    }

    [Fact]
    public void ConcreteHandling_ForceMock_Still_Mocks_Interface_Deps_Normally()
    {
        // ForceMock only changes concrete-class handling; interface deps already mock.
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }
            public class RequestContext { public virtual string TenantId { get; set; } = ""; }

            public partial class UserService
            {
                public UserService(IUserRepository userRepository, RequestContext context) { }
            }

            [Cover<UserService>(ConcreteHandling = ConcreteHandling.ForceMock)]
            public partial class UserServiceTests { }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        fixtureSource.Should().Contain("Mock<IUserRepository>",
            "interface deps still go through Mock<T>");
        fixtureSource.Should().Contain("_mockUserRepository.Object",
            "interface mock objects are passed via .Object to the constructor");
    }

    // =========================================================================
    // P1 — TestFixturePipeline exact-namespace match
    // =========================================================================

    [Fact]
    public void CoverAttribute_In_Consumer_Namespace_Containing_IoCTools_Testing_Is_Not_Picked_Up()
    {
        // P1 finding: previously `Contains("IoCTools.Testing")` false-positived on
        // consumer namespaces that contain that substring. After the fix the pipeline
        // only matches IoCTools.Testing.Annotations.CoverAttribute<T>.
        var source = """
            using System;

            namespace MyApp.IoCTools.Testing.Stuff
            {
                [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
                public sealed class CoverAttribute<TService> : Attribute where TService : class { }
            }

            namespace MyApp.Tests
            {
                using MyApp.IoCTools.Testing.Stuff;

                public interface IUserRepository { }

                public partial class UserService
                {
                    public UserService(IUserRepository userRepository) { }
                }

                [Cover<UserService>]
                public partial class UserServiceTests { }
            }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        fixtureSource.Should().BeNull(
            "a consumer namespace containing the substring 'IoCTools.Testing' must not be matched as the real attribute");
    }

    [Fact]
    public void Real_IoCTools_Testing_Annotations_CoverAttribute_Is_Still_Picked_Up()
    {
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }

            public partial class UserService
            {
                public UserService(IUserRepository userRepository) { }
            }

            [Cover<UserService>]
            public partial class UserServiceTests { }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        fixtureSource.Should().NotBeNull("the real IoCTools.Testing.Annotations.CoverAttribute<T> must still be matched");
        fixtureSource.Should().Contain("Mock<IUserRepository>");
    }

    // =========================================================================
    // P1 — Static-mutable state removed: parallel-safe
    // =========================================================================

    [Fact]
    public async Task Parallel_Compilations_With_Different_Inputs_Do_Not_Bleed_State()
    {
        // P1 finding: FixtureEmitter.CurrentOptionsProfile mutable static is gone;
        // running multiple compilations concurrently produces deterministic outputs.
        var sourceA = """
            using IoCTools.Testing.Annotations;
            public interface IRepoA { }
            public partial class ServiceA { public ServiceA(IRepoA r) { } }
            [Cover<ServiceA>] public partial class ServiceATests { }
            """;
        var sourceB = """
            using IoCTools.Testing.Annotations;
            public interface IRepoB { }
            public class CtxB { public virtual int V { get; set; } }
            public partial class ServiceB { public ServiceB(IRepoB r, CtxB c) { } }
            [Cover<ServiceB>(ConcreteHandling = ConcreteHandling.ForceMock)] public partial class ServiceBTests { }
            """;

        var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
        {
            var src = (i % 2 == 0) ? sourceA : sourceB;
            var className = (i % 2 == 0) ? "ServiceATests" : "ServiceBTests";
            var result = GenerateFixture(src);
            return (className, source: GetFixtureSource(result.GeneratedTrees, className));
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var (className, source) in results)
        {
            source.Should().NotBeNull($"each parallel compilation must independently produce a fixture for {className}");
            if (className == "ServiceATests")
            {
                source.Should().Contain("Mock<IRepoA>");
                source.Should().NotContain("CtxB");
            }
            else
            {
                source.Should().Contain("Mock<IRepoB>");
                source.Should().Contain("Mock<CtxB>",
                    "ForceMock from compilation B must not leak into / be leaked over by A");
            }
        }
    }
}
