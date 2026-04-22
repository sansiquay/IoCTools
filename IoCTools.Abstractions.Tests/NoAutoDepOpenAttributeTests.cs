namespace IoCTools.Abstractions.Tests;

using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using Xunit;

public sealed class NoAutoDepOpenAttributeTests
{
    [Fact]
    public void Attribute_targets_class_only_and_allows_multiple()
    {
        var usage = typeof(NoAutoDepOpenAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().Be(AttributeTargets.Class);
        usage.AllowMultiple.Should().BeTrue();
    }

    [Fact]
    public void Constructor_throws_on_null_unbound_generic_type()
    {
        Action act = () => new NoAutoDepOpenAttribute(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_stores_unbound_generic_type()
    {
        var attr = new NoAutoDepOpenAttribute(typeof(IEnumerable<>));
        attr.UnboundGenericType.Should().Be(typeof(IEnumerable<>));
    }
}
