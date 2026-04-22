namespace IoCTools.Abstractions.Tests;

using System;
using System.Linq;

using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using Xunit;

public sealed class AutoDepInAttributeTests
{
    private sealed class SampleProfile : IAutoDepsProfile { }
    private interface IExample { }

    [Fact]
    public void Attribute_has_generic_arity_of_two()
    {
        typeof(AutoDepInAttribute<,>).GetGenericArguments().Length.Should().Be(2);
    }

    [Fact]
    public void TProfile_is_constrained_to_IAutoDepsProfile()
    {
        var tProfile = typeof(AutoDepInAttribute<,>).GetGenericArguments()[0];
        tProfile.GetGenericParameterConstraints()
            .Should().Contain(typeof(IAutoDepsProfile));
    }

    [Fact]
    public void Attribute_targets_assembly_only_and_allows_multiple()
    {
        var usage = typeof(AutoDepInAttribute<,>)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().Be(AttributeTargets.Assembly);
        usage.AllowMultiple.Should().BeTrue();
    }

    [Fact]
    public void Default_scope_is_Assembly()
    {
        var attr = new AutoDepInAttribute<SampleProfile, IExample>();
        attr.Scope.Should().Be(AutoDepScope.Assembly);
    }

    [Fact]
    public void Scope_can_be_set_to_Transitive()
    {
        var attr = new AutoDepInAttribute<SampleProfile, IExample> { Scope = AutoDepScope.Transitive };
        attr.Scope.Should().Be(AutoDepScope.Transitive);
    }
}
