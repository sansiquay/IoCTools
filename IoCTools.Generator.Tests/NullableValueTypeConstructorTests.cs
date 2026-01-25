namespace IoCTools.Generator.Tests;

/// <summary>
///     Tests for nullable value type constructor parameter generation.
///     Verifies that Nullable<T> unwrapping works correctly for configuration validation
///     and constructor generation with nullable value types.
/// </summary>
public class NullableValueTypeConstructorTests
{
    [Fact]
    public void Constructor_NullableInt_GeneratesCorrectParameter()
    {
        // Arrange - Test nullable int (int?) as constructor parameter
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService { }

[Scoped]
public partial class NullableIntService : IService
{
    [Inject] private readonly int? _optionalInt;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("NullableIntService");
        constructorSource.Content.Should().Contain("int? optionalInt");
        constructorSource.Content.Should().Contain("this._optionalInt = optionalInt");
    }

    [Fact]
    public void Constructor_NullableDateTime_GeneratesCorrectParameter()
    {
        // Arrange - Test nullable DateTime as constructor parameter
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;

namespace Test;

public interface IService { }

[Scoped]
public partial class NullableDateTimeService : IService
{
    [Inject] private readonly DateTime? _optionalTimestamp;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("NullableDateTimeService");

        // The generator uses DateTime? without global:: prefix for System types
        constructorSource.Content.Should().Contain("DateTime? optionalTimestamp");
        constructorSource.Content.Should().Contain("this._optionalTimestamp = optionalTimestamp");
    }

    [Fact]
    public void Constructor_NullableGuid_GeneratesCorrectParameter()
    {
        // Arrange - Test nullable Guid as constructor parameter
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;

namespace Test;

public interface IService { }

[Scoped]
public partial class NullableGuidService : IService
{
    [Inject] private readonly Guid? _optionalId;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("NullableGuidService");

        // The generator may use Guid without global:: prefix for System types
        constructorSource.Content.Should().Contain("Guid? optionalId");
        constructorSource.Content.Should().Contain("this._optionalId = optionalId");
    }

    [Fact]
    public void Constructor_NullableBool_GeneratesCorrectParameter()
    {
        // Arrange - Test nullable bool as constructor parameter
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService { }

[Scoped]
public partial class NullableBoolService : IService
{
    [Inject] private readonly bool? _optionalFlag;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("NullableBoolService");
        constructorSource.Content.Should().Contain("bool? optionalFlag");
        constructorSource.Content.Should().Contain("this._optionalFlag = optionalFlag");
    }

    [Fact]
    public void Constructor_NullableDouble_GeneratesCorrectParameter()
    {
        // Arrange - Test nullable double as constructor parameter
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService { }

[Scoped]
public partial class NullableDoubleService : IService
{
    [Inject] private readonly double? _optionalRate;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("NullableDoubleService");
        constructorSource.Content.Should().Contain("double? optionalRate");
        constructorSource.Content.Should().Contain("this._optionalRate = optionalRate");
    }

    [Fact]
    public void Constructor_MixedNullableAndNonNullable_GeneratesCorrectly()
    {
        // Arrange - Mix of nullable and non-nullable value types
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;

namespace Test;

public interface IService { }

[Scoped]
public partial class MixedNullableService : IService
{
    [Inject] private readonly int _requiredInt;
    [Inject] private readonly int? _optionalInt;
    [Inject] private readonly DateTime? _optionalDate;
    [Inject] private readonly bool _requiredFlag;
    [Inject] private readonly Guid? _optionalId;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("MixedNullableService");

        // All parameters should be present
        constructorSource.Content.Should().Contain("int requiredInt");
        constructorSource.Content.Should().Contain("int? optionalInt");
        constructorSource.Content.Should().Contain("DateTime? optionalDate");
        constructorSource.Content.Should().Contain("bool requiredFlag");
        constructorSource.Content.Should().Contain("Guid? optionalId");
    }

    [Fact]
    public void Constructor_NullableCustomStruct_GeneratesCorrectParameter()
    {
        // Arrange - Test nullable custom struct
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService { }

public struct CustomStruct
{
    public int Value { get; set; }
}

[Scoped]
public partial class NullableStructService : IService
{
    [Inject] private readonly CustomStruct? _optionalStruct;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("NullableStructService");
        constructorSource.Content.Should().Contain("CustomStruct? optionalStruct");
        constructorSource.Content.Should().Contain("this._optionalStruct = optionalStruct");
    }

    [Fact]
    public void Constructor_NullableTimeSpan_GeneratesCorrectParameter()
    {
        // Arrange - Test nullable TimeSpan
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;

namespace Test;

public interface IService { }

[Scoped]
public partial class NullableTimeSpanService : IService
{
    [Inject] private readonly TimeSpan? _optionalTimeout;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("NullableTimeSpanService");
        constructorSource.Content.Should().Contain("TimeSpan? optionalTimeout");
        constructorSource.Content.Should().Contain("this._optionalTimeout = optionalTimeout");
    }
}
