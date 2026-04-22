namespace IoCTools.Generator.Shared.Tests;

using FluentAssertions;
using IoCTools.Generator.Shared;
using Xunit;

public sealed class ValueTypeEquatabilityTests
{
    [Fact]
    public void AutoDepAttribution_equal_values_are_equal()
    {
        var a = new AutoDepAttribution(
            AutoDepSourceKind.AutoUniversal, sourceName: null, assemblyName: null);
        var b = new AutoDepAttribution(
            AutoDepSourceKind.AutoUniversal, sourceName: null, assemblyName: null);
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void AutoDepAttribution_different_kinds_are_not_equal()
    {
        var a = new AutoDepAttribution(AutoDepSourceKind.AutoUniversal, null, null);
        var b = new AutoDepAttribution(AutoDepSourceKind.AutoBuiltinILogger, null, null);
        a.Should().NotBe(b);
    }

    [Fact]
    public void AutoDepAttribution_auto_profile_includes_source_name_in_equality()
    {
        var a = new AutoDepAttribution(AutoDepSourceKind.AutoProfile, "ControllerDefaults", null);
        var b = new AutoDepAttribution(AutoDepSourceKind.AutoProfile, "BackgroundDefaults", null);
        a.Should().NotBe(b);
    }
}
