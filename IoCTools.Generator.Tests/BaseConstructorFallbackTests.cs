namespace IoCTools.Generator.Tests;

using IoCTools.Generator.Tests;
using Microsoft.CodeAnalysis;
using FluentAssertions;
using Xunit;
using System.Linq;

public class BaseConstructorFallbackTests
{
    [Fact]
    public void Constructor_BaseClassUnmanaged_WithParameters_DoesNotGenerateBadBaseCall()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public class UnmanagedBase
{
    public UnmanagedBase(string connectionString)
    {
    }
}

public interface IService { }

public partial class DerivedService : UnmanagedBase
{
    [Inject] private readonly IService _service;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Generator should skip constructor generation when base class requires
        // parameters we cannot provide (this is the fix for ioc-chq)
        var constructorSource = result.GetConstructorSource("DerivedService");
        constructorSource.Should().BeNull(
            "Generator should skip constructor generation when non-IoC base class requires parameters");

        // Verify we never generate the old invalid base(\"default\") pattern in any generated source
        foreach (var generatedSource in result.GeneratedSources)
        {
            generatedSource.Content.Should().NotContain("base(\"default\")",
                "Generator should never generate invalid base(\"default\") placeholder");
        }

        // Compilation will fail because the user didn't provide a manual constructor
        // that properly calls the base constructor with required parameters
        result.HasErrors.Should().BeTrue("Compilation should fail because base constructor args are missing");
    }
}
