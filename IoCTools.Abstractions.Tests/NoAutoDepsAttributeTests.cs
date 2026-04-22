namespace IoCTools.Abstractions.Tests;

using System;
using System.Linq;

using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using Xunit;

public sealed class NoAutoDepsAttributeTests
{
    [Fact]
    public void Attribute_targets_class_only_and_does_not_allow_multiple()
    {
        var usage = typeof(NoAutoDepsAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().Be(AttributeTargets.Class);
        usage.AllowMultiple.Should().BeFalse();
    }

    [Fact]
    public void Attribute_can_be_instantiated()
    {
        var attr = new NoAutoDepsAttribute();
        attr.Should().NotBeNull();
    }
}
