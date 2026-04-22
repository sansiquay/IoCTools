namespace IoCTools.Abstractions.Tests;

using System;
using System.Linq;

using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using Xunit;

public sealed class NoAutoDepAttributeTests
{
    private interface IExample { }

    [Fact]
    public void Attribute_has_generic_arity_of_one()
    {
        typeof(NoAutoDepAttribute<>).GetGenericArguments().Length.Should().Be(1);
    }

    [Fact]
    public void Attribute_targets_class_only_and_allows_multiple()
    {
        var usage = typeof(NoAutoDepAttribute<>)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().Be(AttributeTargets.Class);
        usage.AllowMultiple.Should().BeTrue();
    }

    [Fact]
    public void Attribute_can_be_instantiated()
    {
        var attr = new NoAutoDepAttribute<IExample>();
        attr.Should().NotBeNull();
    }
}
