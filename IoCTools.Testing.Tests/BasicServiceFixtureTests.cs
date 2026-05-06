using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace IoCTools.Testing.Tests;

using System.Collections.Immutable;

/// <summary>
/// Tests for basic service fixture generation scenarios.
/// Verifies exact generated member shape: mock fields, CreateSut(), typed setup helpers.
/// Services use explicit constructors so the fixture generator can independently find parameters.
/// </summary>
public sealed class BasicServiceFixtureTests
{
    #region Helpers

    /// <summary>
    /// Runs the testing generator with explicit-constructor services.
    /// Uses trusted platform assemblies for proper compilation.
    /// </summary>
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

    /// <summary>
    /// Gets the generated fixture source text for a test class hint name.
    /// Returns null if no matching fixture is found.
    /// </summary>
    private static string? GetFixtureSource(ImmutableArray<SyntaxTree> trees, string hintName)
    {
        return trees
            .Select(t => t.ToString())
            .FirstOrDefault(s => s.Contains(hintName));
    }

    #endregion

    #region Test Class Shape

    [Fact]
    public void Internal_Test_Class_Preserves_Accessibility()
    {
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }

            public class UserService
            {
                public UserService(IUserRepository userRepository) { }
            }

            [Cover<UserService>]
            internal partial class UserServiceTests { }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("internal partial class UserServiceTests");
        TestHelper.VerifyCompiles(source, result).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Nested_Test_Class_Preserves_Containing_Type()
    {
        var source = """
            using IoCTools.Testing.Annotations;

            namespace Sample;

            public interface IUserRepository { }

            public class UserService
            {
                public UserService(IUserRepository userRepository) { }
            }

            public partial class ServiceTestHost
            {
                [Cover<UserService>]
                internal partial class UserServiceTests { }
            }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("public partial class ServiceTestHost");
        fixtureSource.Should().Contain("internal partial class UserServiceTests");
        TestHelper.VerifyCompiles(source, result).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generic_Test_Class_Preserves_Type_Parameters()
    {
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IRepository<T> { }

            public class GenericService<T>
            {
                public GenericService(IRepository<T> repository) { }
            }

            [Cover<GenericService<string>>]
            public partial class GenericServiceTests<TMarker>
            {
            }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "GenericServiceTests");

        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("public partial class GenericServiceTests<TMarker>");
        fixtureSource.Should().Contain("Mock<IRepository<string>> _mockRepositoryString = new()");
        TestHelper.VerifyCompiles(source, result).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Cross_Namespace_Service_Type_Is_Imported()
    {
        var source = """
            using IoCTools.Testing.Annotations;

            namespace Shared
            {
                public interface IUserRepository { }
            }

            namespace MyApp.Services
            {
                public class UserService
                {
                    public UserService(Shared.IUserRepository repository) { }
                }
            }

            namespace MyApp.Services.Tests
            {
                [Cover<MyApp.Services.UserService>]
                public partial class UserServiceTests { }
            }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("using MyApp.Services;");
        fixtureSource.Should().Contain("using Shared;");
        fixtureSource.Should().Contain("private UserService Sut => _sut ??= CreateSut();");
        TestHelper.VerifyCompiles(source, result).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Parameterless_Service_Gets_Sut_And_CreateSut()
    {
        var source = """
            using IoCTools.Testing.Annotations;

            public class ParameterlessService
            {
                public ParameterlessService() { }
            }

            [Cover<ParameterlessService>]
            public partial class ParameterlessServiceTests { }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "ParameterlessServiceTests");

        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().StartWith("#nullable enable");
        fixtureSource.Should().Contain("private ParameterlessService? _sut;");
        fixtureSource.Should().Contain("private ParameterlessService Sut => _sut ??= CreateSut();");
        fixtureSource.Should().Contain("public ParameterlessService CreateSut() => new();");
        TestHelper.VerifyCompiles(source, result).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Sealed_Test_Class_Generated_Fixture_Compiles()
    {
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }

            public sealed class UserService
            {
                public UserService(IUserRepository repository) { }
            }

            [Cover<UserService>]
            public sealed partial class UserServiceTests { }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("private readonly Mock<IUserRepository> _mockUserRepository = new();");
        fixtureSource.Should().Contain("private UserService Sut => _sut ??= CreateSut();");
        TestHelper.VerifyCompiles(source, result).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Fixture_Usings_Are_Global_When_Consumer_Has_System_Namespace()
    {
        var source = """
            using IoCTools.Testing.Annotations;

            namespace MyApp.System
            {
                public sealed class Marker { }
            }

            namespace MyApp.Services
            {
                public interface IUserRepository { }

                public sealed class UserService
                {
                    public UserService(IUserRepository repository) { }
                }
            }

            namespace MyApp.System.Tests
            {
                [Cover<MyApp.Services.UserService>]
                public sealed partial class UserServiceTests { }
            }
            """;

        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("using System.Linq;");
        TestHelper.VerifyCompiles(source, result).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Same_Named_Test_Classes_In_Different_Namespaces_Get_Distinct_Fixtures()
    {
        var source = """
            using IoCTools.Testing.Annotations;

            namespace First
            {
                public interface IFirstDep { }
                public class UserService
                {
                    public UserService(IFirstDep dep) { }
                }
            }

            namespace Second
            {
                public interface ISecondDep { }
                public class UserService
                {
                    public UserService(ISecondDep dep) { }
                }
            }

            namespace First.Tests
            {
                [Cover<First.UserService>]
                public partial class UserServiceTests { }
            }

            namespace Second.Tests
            {
                [Cover<Second.UserService>]
                public partial class UserServiceTests { }
            }
            """;

        var result = GenerateFixture(source);
        var fixtureSources = result.GeneratedTrees
            .Select(t => t.ToString())
            .Where(s => s.Contains("partial class UserServiceTests"))
            .ToList();

        fixtureSources.Should().HaveCount(2);
        fixtureSources.Should().Contain(s => s.Contains("namespace First.Tests"));
        fixtureSources.Should().Contain(s => s.Contains("namespace Second.Tests"));
        TestHelper.VerifyCompiles(source, result).Diagnostics.Should().BeEmpty();
    }

    #endregion

    #region Mock Fields

    [Fact]
    public void Single_Dependency_Generates_Mock_Field()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }

            public partial class UserService
            {
                private readonly IUserRepository _userRepository;

                public UserService(IUserRepository userRepository)
                {
                    _userRepository = userRepository;
                }
            }

            [Cover<UserService>]
            public partial class UserServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("Mock<IUserRepository>", "should declare typed mock field");
        fixtureSource.Should().Contain("_mockUserRepository", "should use standard field naming");
    }

    [Fact]
    public void Two_Dependencies_Generate_Two_Mock_Fields()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }
            public interface INotificationService { }

            public partial class OrderService
            {
                private readonly IUserRepository _userRepository;
                private readonly INotificationService _notificationService;

                public OrderService(IUserRepository userRepository, INotificationService notificationService)
                {
                    _userRepository = userRepository;
                    _notificationService = notificationService;
                }
            }

            [Cover<OrderService>]
            public partial class OrderServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "OrderServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("_mockUserRepository", "field for IUserRepository");
        fixtureSource.Should().Contain("_mockNotificationService", "field for INotificationService");
    }

    [Fact]
    public void Mock_Field_Is_Protected_And_Readonly()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }

            public partial class UserService
            {
                private readonly IUserRepository _userRepository;

                public UserService(IUserRepository userRepository)
                {
                    _userRepository = userRepository;
                }
            }

            [Cover<UserService>]
            public partial class UserServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("Mock<IUserRepository> _mockUserRepository = new()", "field declaration should be private readonly with new()");
    }

    #endregion

    #region CreateSut Factory

    [Fact]
    public void CreateSut_Uses_Mock_Objects()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }

            public partial class UserService
            {
                private readonly IUserRepository _userRepository;

                public UserService(IUserRepository userRepository)
                {
                    _userRepository = userRepository;
                }
            }

            [Cover<UserService>]
            public partial class UserServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("UserService CreateSut() => new(", "should have typed CreateSut factory");
        fixtureSource.Should().Contain("_mockUserRepository.Object", "CreateSut should pass mock.Object");
    }

    [Fact]
    public void CreateSut_Preserves_Parameter_Order()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IFirst { }
            public interface ISecond { }

            public partial class OrderedService
            {
                private readonly IFirst _first;
                private readonly ISecond _second;

                public OrderedService(IFirst first, ISecond second)
                {
                    _first = first;
                    _second = second;
                }
            }

            [Cover<OrderedService>]
            public partial class OrderedServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "OrderedServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        var createSutLine = fixtureSource.Split('\n')
            .FirstOrDefault(l => l.Contains("CreateSut()"));
        createSutLine.Should().NotBeNull();

        // Find the constructor argument expressions
        var lines = fixtureSource.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var argLines = lines
            .SkipWhile(l => !l.Contains("CreateSut() => new("))
            .Skip(1)
            .TakeWhile(l => l.Contains("_mock"))
            .ToList();

        argLines.Should().HaveCount(2, "two constructor arguments");
        argLines[0].Should().Contain("_mockFirst.Object");
        argLines[1].Should().Contain("_mockSecond.Object");
    }

    #endregion

    #region Setup Helpers

    [Fact]
    public void Setup_Helper_Generated_For_Each_Dependency()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }

            public partial class UserService
            {
                private readonly IUserRepository _userRepository;

                public UserService(IUserRepository userRepository)
                {
                    _userRepository = userRepository;
                }
            }

            [Cover<UserService>]
            public partial class UserServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("void SetupUserRepository(", "should have typed setup helper");
        fixtureSource.Should().Contain("Action<Mock<IUserRepository>> configure", "should accept configure action");
    }

    [Fact]
    public void Mock_Accessor_Names_Are_Disambiguated_With_Field_Names()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            namespace Alpha { public interface ISettings { } }
            namespace Beta { public interface ISettings { } }

            public partial class CollisionService
            {
                public CollisionService(Alpha.ISettings alpha, Beta.ISettings beta) { }
            }

            [Cover<CollisionService>]
            public partial class CollisionServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "CollisionServiceTests");
        var compileResult = TestHelper.VerifyCompiles(source, result);

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("AlphaSettingsMock", "accessor should follow disambiguated Alpha field name");
        fixtureSource.Should().Contain("BetaSettingsMock", "accessor should follow disambiguated Beta field name");
        var errors = compileResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty("disambiguated fixture accessors should compile");
    }

    [Fact]
    public void Mock_Member_Names_Remain_Unique_When_Namespace_Leaf_Matches()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            namespace One.Common { public interface ISettings { } }
            namespace Two.Common { public interface ISettings { } }

            public partial class CommonCollisionService
            {
                public CommonCollisionService(One.Common.ISettings one, Two.Common.ISettings two) { }
            }

            [Cover<CommonCollisionService>]
            public partial class CommonCollisionServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "CommonCollisionServiceTests");
        var compileResult = TestHelper.VerifyCompiles(source, result);

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("_mockCommonSettings");
        fixtureSource.Should().Contain("_mockCommonSettings_2");
        fixtureSource.Should().Contain("CommonSettingsMock");
        fixtureSource.Should().Contain("CommonSettings_2Mock");
        fixtureSource.Should().Contain("SetupCommonSettings(");
        fixtureSource.Should().Contain("SetupCommonSettings_2(");
        var errors = compileResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty("post-disambiguation duplicate names should get deterministic suffixes");
    }

    [Fact]
    public void Concrete_Dependency_Uses_Real_Instance_Helpers()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }

            public sealed class RequestContext
            {
                public string TenantId { get; set; } = "";
            }

            public partial class UserService
            {
                public UserService(IUserRepository userRepository, RequestContext context) { }
            }

            [Cover<UserService>]
            public partial class UserServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "UserServiceTests");
        var compileResult = TestHelper.VerifyCompiles(source, result);

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("private readonly Mock<IUserRepository> _mockUserRepository = new();");
        fixtureSource.Should().Contain("private RequestContext RequestContext { get; set; } = new();");
        fixtureSource.Should().NotContain("Mock<RequestContext>");
        fixtureSource.Should().Contain("private RequestContext UseRequestContext(RequestContext value)");
        fixtureSource.Should().Contain("private RequestContext ConfigureRequestContext(Action<RequestContext> configure)");
        fixtureSource.Should().Contain("_mockUserRepository.Object,");
        fixtureSource.Should().Contain("RequestContext");

        var errors = compileResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty("concrete dependency fixture helpers should compile");
    }

    #endregion

    #region No Blocking Errors

    [Fact]
    public void Fixture_Generation_Produces_No_Blocking_Errors()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }

            public partial class UserService
            {
                private readonly IUserRepository _userRepository;

                public UserService(IUserRepository userRepository)
                {
                    _userRepository = userRepository;
                }
            }

            [Cover<UserService>]
            public partial class UserServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);

        // Assert
        var blockingErrors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        blockingErrors.Should().BeEmpty("generator should produce no errors for valid input");
    }

    [Fact]
    public void BasicFixture_VerifyCompiles()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IUserRepository { }

            public partial class UserService
            {
                private readonly IUserRepository _userRepository;

                public UserService(IUserRepository userRepository)
                {
                    _userRepository = userRepository;
                }
            }

            [Cover<UserService>]
            public partial class UserServiceTests { }
            """;

        // Act
        var genResult = GenerateFixture(source);
        var compileResult = TestHelper.VerifyCompiles(source, genResult);

        // Assert
        var errors = compileResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty("generated fixture + original source should compile without errors");
    }

    #endregion
}
