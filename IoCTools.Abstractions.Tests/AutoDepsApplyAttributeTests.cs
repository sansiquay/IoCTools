namespace IoCTools.Abstractions.Tests;

using System;
using System.Linq;

using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using Xunit;

public sealed class AutoDepsApplyAttributeTests
{
    private sealed class SampleProfile : IAutoDepsProfile { }
    private abstract class SampleBase { }

    [Fact]
    public void Attribute_has_generic_arity_of_two()
    {
        typeof(AutoDepsApplyAttribute<,>).GetGenericArguments().Length.Should().Be(2);
    }

    [Fact]
    public void TProfile_is_constrained_to_IAutoDepsProfile()
    {
        var tProfile = typeof(AutoDepsApplyAttribute<,>).GetGenericArguments()[0];
        tProfile.GetGenericParameterConstraints()
            .Should().Contain(typeof(IAutoDepsProfile));
    }

    [Fact]
    public void TBase_has_no_constraints()
    {
        var tBase = typeof(AutoDepsApplyAttribute<,>).GetGenericArguments()[1];
        tBase.GetGenericParameterConstraints().Should().BeEmpty();
    }

    [Fact]
    public void Attribute_targets_assembly_only_and_allows_multiple()
    {
        var usage = typeof(AutoDepsApplyAttribute<,>)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().Be(AttributeTargets.Assembly);
        usage.AllowMultiple.Should().BeTrue();
    }

    [Fact]
    public void Default_scope_is_Assembly()
    {
        var attr = new AutoDepsApplyAttribute<SampleProfile, SampleBase>();
        attr.Scope.Should().Be(AutoDepScope.Assembly);
    }

    [Fact]
    public void Scope_can_be_set_to_Transitive()
    {
        var attr = new AutoDepsApplyAttribute<SampleProfile, SampleBase> { Scope = AutoDepScope.Transitive };
        attr.Scope.Should().Be(AutoDepScope.Transitive);
    }
}
