namespace IoCTools.Abstractions.Tests;

using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using Xunit;

public sealed class IAutoDepsProfileTests
{
    private sealed class SampleProfile : IAutoDepsProfile { }

    [Fact]
    public void Interface_is_assignable_from_implementing_class()
    {
        var profile = (IAutoDepsProfile)new SampleProfile();
        profile.Should().NotBeNull();
    }

    [Fact]
    public void Interface_has_no_members()
    {
        typeof(IAutoDepsProfile).GetMembers(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.DeclaredOnly)
            .Should().BeEmpty();
    }
}
