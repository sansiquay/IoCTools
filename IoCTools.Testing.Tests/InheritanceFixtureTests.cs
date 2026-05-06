using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace IoCTools.Testing.Tests;

using System.Collections.Immutable;

/// <summary>
/// Tests for inheritance hierarchy fixture generation scenarios.
/// Verifies base and derived dependencies appear in generated members.
/// </summary>
public sealed class InheritanceFixtureTests
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
    public void Derived_Service_Gets_Base_And_Derived_Mock_Fields()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IBaseDependency { }
            public interface IDerivedDependency { }

            public abstract class BaseService
            {
                private readonly IBaseDependency _baseDep;

                public BaseService(IBaseDependency baseDep)
                {
                    _baseDep = baseDep;
                }
            }

            public sealed class DerivedService : BaseService
            {
                private readonly IDerivedDependency _derivedDep;

                public DerivedService(IBaseDependency baseDep, IDerivedDependency derivedDep)
                    : base(baseDep)
                {
                    _derivedDep = derivedDep;
                }
            }

            [Cover<DerivedService>]
            public partial class DerivedServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "DerivedServiceTests");

        // Assert - All dependencies present
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("_mockBaseDependency", "should have field for base dependency");
        fixtureSource.Should().Contain("_mockDerivedDependency", "should have field for derived dependency");
    }

    [Fact]
    public void Three_Level_Inheritance_All_Deps_Present()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IDep1 { }
            public interface IDep2 { }
            public interface IDep3 { }

            public abstract class Level1
            {
                private readonly IDep1 _dep1;
                public Level1(IDep1 dep1) { _dep1 = dep1; }
            }

            public abstract class Level2 : Level1
            {
                private readonly IDep2 _dep2;
                public Level2(IDep1 dep1, IDep2 dep2) : base(dep1) { _dep2 = dep2; }
            }

            public sealed class Level3 : Level2
            {
                private readonly IDep3 _dep3;
                public Level3(IDep1 dep1, IDep2 dep2, IDep3 dep3) : base(dep1, dep2) { _dep3 = dep3; }
            }

            [Cover<Level3>]
            public partial class Level3Tests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "Level3Tests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("_mockDep1", "Level1 dependency");
        fixtureSource.Should().Contain("_mockDep2", "Level2 dependency");
        fixtureSource.Should().Contain("_mockDep3", "Level3 dependency");
    }

    [Fact]
    public void Derived_Service_CreateSut_Includes_All_Parameters()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IBaseDependency { }
            public interface IDerivedDependency { }

            public abstract class BaseService
            {
                private readonly IBaseDependency _baseDep;
                public BaseService(IBaseDependency baseDep) { _baseDep = baseDep; }
            }

            public sealed class DerivedService : BaseService
            {
                private readonly IDerivedDependency _derivedDep;
                public DerivedService(IBaseDependency baseDep, IDerivedDependency derivedDep)
                    : base(baseDep) { _derivedDep = derivedDep; }
            }

            [Cover<DerivedService>]
            public partial class DerivedServiceTests { }
            """;

        // Act
        var result = GenerateFixture(source);
        var fixtureSource = GetFixtureSource(result.GeneratedTrees, "DerivedServiceTests");

        // Assert
        fixtureSource.Should().NotBeNull();
        fixtureSource.Should().Contain("_mockBaseDependency.Object", "CreateSut should include base dep");
        fixtureSource.Should().Contain("_mockDerivedDependency.Object", "CreateSut should include derived dep");
    }

    [Fact]
    public void Inheritance_Fixture_Has_No_Blocking_Errors()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IDep1 { }
            public interface IDep2 { }
            public interface IDep3 { }

            public class Level1 { public Level1(IDep1 dep1) { } }
            public class Level2 : Level1 { public Level2(IDep1 dep1, IDep2 dep2) : base(dep1) { } }
            public class Level3 : Level2 { public Level3(IDep1 dep1, IDep2 dep2, IDep3 dep3) : base(dep1, dep2) { } }

            [Cover<Level3>]
            public partial class Level3Tests { }
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
    public void InheritanceFixture_VerifyCompiles()
    {
        // Arrange
        var source = """
            using IoCTools.Testing.Annotations;

            public interface IDep1 { }
            public interface IDep2 { }
            public interface IDep3 { }

            public class Level1 { public Level1(IDep1 dep1) { } }
            public class Level2 : Level1 { public Level2(IDep1 dep1, IDep2 dep2) : base(dep1) { } }
            public class Level3 : Level2 { public Level3(IDep1 dep1, IDep2 dep2, IDep3 dep3) : base(dep1, dep2) { } }

            [Cover<Level3>]
            public partial class Level3Tests { }
            """;

        // Act
        var genResult = GenerateFixture(source);
        var compileResult = TestHelper.VerifyCompiles(source, genResult);

        // Assert
        var errors = compileResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty("generated inheritance fixture + original source should compile without errors");
    }
}
