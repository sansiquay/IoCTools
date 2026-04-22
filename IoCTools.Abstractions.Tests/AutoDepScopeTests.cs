namespace IoCTools.Abstractions.Tests;

using System;

using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using Xunit;

public sealed class AutoDepScopeTests
{
    [Fact]
    public void AutoDepScope_has_Assembly_as_default_underlying_value_zero()
    {
        ((int)AutoDepScope.Assembly).Should().Be(0);
        ((int)AutoDepScope.Transitive).Should().Be(1);
    }

    [Fact]
    public void Enum_values_have_expected_names()
    {
        Enum.GetNames(typeof(AutoDepScope))
            .Should().BeEquivalentTo("Assembly", "Transitive");
    }
}
