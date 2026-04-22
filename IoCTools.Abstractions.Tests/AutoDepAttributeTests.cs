namespace IoCTools.Abstractions.Tests;

using System;
using System.Linq;

using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using Xunit;

public sealed class AutoDepAttributeTests
{
    private interface IExample { }

    [Fact]
    public void Attribute_targets_assembly_only_and_allows_multiple()
    {
        var usage = typeof(AutoDepAttribute<>)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().Be(AttributeTargets.Assembly);
        usage.AllowMultiple.Should().BeTrue();
    }

    [Fact]
    public void Default_scope_is_Assembly()
    {
        var attr = new AutoDepAttribute<IExample>();
        attr.Scope.Should().Be(AutoDepScope.Assembly);
    }

    [Fact]
    public void Scope_can_be_set_to_Transitive()
    {
        var attr = new AutoDepAttribute<IExample> { Scope = AutoDepScope.Transitive };
        attr.Scope.Should().Be(AutoDepScope.Transitive);
    }
}
