namespace IoCTools.Abstractions.Tests;

using System;
using System.Linq;

using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using Xunit;

public sealed class AutoDepsApplyGlobAttributeTests
{
    private sealed class SampleProfile : IAutoDepsProfile { }

    [Fact]
    public void Constructor_throws_on_null_pattern()
    {
        Action act = () => new AutoDepsApplyGlobAttribute<SampleProfile>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_stores_pattern()
    {
        var attr = new AutoDepsApplyGlobAttribute<SampleProfile>("MyApp.Services.*");
        attr.Pattern.Should().Be("MyApp.Services.*");
    }

    [Fact]
    public void TProfile_is_constrained_to_IAutoDepsProfile()
    {
        var tProfile = typeof(AutoDepsApplyGlobAttribute<>).GetGenericArguments()[0];
        tProfile.GetGenericParameterConstraints()
            .Should().Contain(typeof(IAutoDepsProfile));
    }

    [Fact]
    public void Attribute_targets_assembly_only_and_allows_multiple()
    {
        var usage = typeof(AutoDepsApplyGlobAttribute<>)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().Be(AttributeTargets.Assembly);
        usage.AllowMultiple.Should().BeTrue();
    }

    [Fact]
    public void Default_scope_is_Assembly()
    {
        var attr = new AutoDepsApplyGlobAttribute<SampleProfile>("X.*");
        attr.Scope.Should().Be(AutoDepScope.Assembly);
    }
}
