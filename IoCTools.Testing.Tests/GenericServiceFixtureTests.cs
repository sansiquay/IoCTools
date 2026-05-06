using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace IoCTools.Testing.Tests;

using System.Collections.Immutable;

/// <summary>
/// Tests for generic service fixture generation scenarios.
/// Verifies generic mock field names, helpers, and CreateSut() are valid.
/// </summary>
public sealed class GenericServiceFixtureTests
{
    #region Helpers

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

    private static string? GetFixtureSource(ImmutableArray<SyntaxTree> trees, string hintName)
    {
        return trees
            .Select(t => t.ToString())
            .FirstOrDefault(s => s.Contains(hintName));
    }

    #endregion

    [Fact]
    public void Generic_Dependency_Gets_Valid_Mock_Field_Name()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using System.Collections.Generic;

            public interface IRepository<T> { }

            public partial class DataService
            {
                private readonly IRepository<string> _repository;

                public DataService(IRepository<string> repository)
                {
                    _repository = repository;
                }
            }

            [Cover<DataService>]
            public partial class DataServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "DataServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("Mock<IRepository<string>>", "should include generic type args in field type");
    }

    [Fact]
    public void Generic_Service_CreateSut_Has_Valid_Constructor_Args()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IRepository<T> { }

            public sealed class Customer { }

            public partial class CustomerService
            {
                private readonly IRepository<Customer> _repository;

                public CustomerService(IRepository<Customer> repository)
                {
                    _repository = repository;
                }
            }

            [Cover<CustomerService>]
            public partial class CustomerServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "CustomerServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        // Generic type appends type arg to field name for disambiguation
        fixtureSource.Should().Contain("_mockRepositoryCustomer.Object", "CreateSut should use mock.Object with generic-aware naming");
    }

    [Fact]
    public void Open_Generic_Service_Generates_Valid_Source()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface ILogger<T> { }

            public partial class GenericService<T>
            {
                private readonly ILogger<T> _logger;

                public GenericService(ILogger<T> logger)
                {
                    _logger = logger;
                }
            }

            [Cover<GenericService<string>>]
            public partial class GenericServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "GenericServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("Mock<ILogger<string>>", "closed generic should resolve type arg");
    }

    [Fact]
    public void Open_Generic_Service_CreateSut_Is_Valid()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface ILogger<T> { }

            public partial class GenericService<T>
            {
                private readonly ILogger<T> _logger;

                public GenericService(ILogger<T> logger)
                {
                    _logger = logger;
                }
            }

            [Cover<GenericService<string>>]
            public partial class GenericServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "GenericServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("GenericService<string> CreateSut()", "CreateSut return type should include closed generic");
    }

    [Fact]
    public void NullLogger_Profile_Preserves_Generic_Category_Type()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using Microsoft.Extensions.Logging;

            public partial class GenericService<T>
            {
                private readonly ILogger<GenericService<T>> _logger;

                public GenericService(ILogger<GenericService<T>> logger)
                {
                    _logger = logger;
                }
            }

            [Cover<GenericService<string>>(Logger = FixtureLoggerProfile.NullLogger)]
            public partial class GenericServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "GenericServiceTests");
        var compileResult = TestHelper.VerifyCompiles(source, result);

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("NullLogger<GenericService<string>>.Instance", "NullLogger category should keep closed generic shape");
        var errors = compileResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty("NullLogger profile should compile for generic service categories");
    }

    [Fact]
    public void Generic_Fixture_Has_No_Blocking_Errors()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using System.Collections.Generic;

            public interface IRepository<T> { }

            public partial class MultiRepoService
            {
                private readonly IRepository<string> _stringRepo;
                private readonly IRepository<int> _intRepo;

                public MultiRepoService(IRepository<string> stringRepo, IRepository<int> intRepo)
                {
                    _stringRepo = stringRepo;
                    _intRepo = intRepo;
                }
            }

            [Cover<MultiRepoService>]
            public partial class MultiRepoServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);

        // Assert
        var blockingErrors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        blockingErrors.Should().BeEmpty();
    }

    [Fact]
    public void GenericFixture_VerifyCompiles()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;
            using System.Collections.Generic;

            public interface IRepository<T> { }

            public partial class MultiRepoService
            {
                private readonly IRepository<string> _stringRepo;
                private readonly IRepository<int> _intRepo;

                public MultiRepoService(IRepository<string> stringRepo, IRepository<int> intRepo)
                {
                    _stringRepo = stringRepo;
                    _intRepo = intRepo;
                }
            }

            [Cover<MultiRepoService>]
            public partial class MultiRepoServiceTests { }
            """;

        // Act
        var genResult = GenerateFixture(source);
        var compileResult = TestHelper.VerifyCompiles(source, genResult);

        // Assert
        var errors = compileResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty("generated generic fixture + original source should compile without errors");
    }
}
