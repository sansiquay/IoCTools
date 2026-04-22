namespace IoCTools.Abstractions.Tests;

using System;
using System.Linq;

using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using Xunit;

public sealed class AutoDepsAttributeTests
{
    [Fact]
    public void Attribute_targets_class_only_and_allows_multiple()
    {
        var usage = typeof(AutoDepsAttribute<>)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().Be(AttributeTargets.Class);
        usage.AllowMultiple.Should().BeTrue();
    }

    [Fact]
    public void TProfile_is_constrained_to_IAutoDepsProfile()
    {
        var tProfile = typeof(AutoDepsAttribute<>).GetGenericArguments()[0];
        tProfile.GetGenericParameterConstraints()
            .Should().Contain(typeof(IAutoDepsProfile));
    }
}
