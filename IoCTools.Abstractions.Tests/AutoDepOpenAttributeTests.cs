namespace IoCTools.Abstractions.Tests;

using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using Xunit;

public sealed class AutoDepOpenAttributeTests
{
    [Fact]
    public void Constructor_throws_on_null_unbound_generic_type()
    {
        Action act = () => new AutoDepOpenAttribute(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_stores_unbound_generic_type()
    {
        var attr = new AutoDepOpenAttribute(typeof(IEnumerable<>));
        attr.UnboundGenericType.Should().Be(typeof(IEnumerable<>));
    }

    [Fact]
    public void Default_scope_is_Assembly()
    {
        var attr = new AutoDepOpenAttribute(typeof(IEnumerable<>));
        attr.Scope.Should().Be(AutoDepScope.Assembly);
    }

    [Fact]
    public void Attribute_targets_assembly_only_and_allows_multiple()
    {
        var usage = typeof(AutoDepOpenAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().Be(AttributeTargets.Assembly);
        usage.AllowMultiple.Should().BeTrue();
    }
}
