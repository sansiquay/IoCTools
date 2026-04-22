namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using IoCTools.Tools.Cli.CommandLine;

using Xunit;

public sealed class CommonAutoDepsOptionsTests
{
    [Fact]
    public void Parse_with_hide_flag_returns_Hide_true()
    {
        var args = new[] { "--hide-auto-deps" };
        var result = CommonAutoDepsOptions.TryExtract(args, out var remaining, out var error);
        result.HideAutoDeps.Should().BeTrue();
        result.OnlyAutoDeps.Should().BeFalse();
        error.Should().BeNull();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public void Parse_with_only_flag_returns_Only_true()
    {
        var args = new[] { "--only-auto-deps" };
        var result = CommonAutoDepsOptions.TryExtract(args, out var remaining, out var error);
        result.OnlyAutoDeps.Should().BeTrue();
        result.HideAutoDeps.Should().BeFalse();
        error.Should().BeNull();
    }

    [Fact]
    public void Parse_with_both_flags_returns_error()
    {
        var args = new[] { "--hide-auto-deps", "--only-auto-deps" };
        _ = CommonAutoDepsOptions.TryExtract(args, out _, out var error);
        error.Should().NotBeNull();
        error!.Should().Contain("mutually exclusive");
    }

    [Fact]
    public void Parse_strips_recognized_flags_from_remaining()
    {
        var args = new[] { "graph", "--hide-auto-deps", "Foo" };
        var result = CommonAutoDepsOptions.TryExtract(args, out var remaining, out _);
        remaining.Should().Equal("graph", "Foo");
        result.HideAutoDeps.Should().BeTrue();
    }

    [Fact]
    public void Parse_ignores_unrelated_args()
    {
        var args = new[] { "why", "Svc", "IFoo", "--format", "json" };
        var result = CommonAutoDepsOptions.TryExtract(args, out var remaining, out _);
        result.HideAutoDeps.Should().BeFalse();
        result.OnlyAutoDeps.Should().BeFalse();
        remaining.Should().Equal("why", "Svc", "IFoo", "--format", "json");
    }

    [Fact]
    public void Empty_singleton_has_both_flags_false()
    {
        CommonAutoDepsOptions.Empty.HideAutoDeps.Should().BeFalse();
        CommonAutoDepsOptions.Empty.OnlyAutoDeps.Should().BeFalse();
    }

    [Fact]
    public void Parse_with_both_flags_still_produces_remaining_without_flags()
    {
        var args = new[] { "--project", "foo.csproj", "--hide-auto-deps", "--only-auto-deps" };
        _ = CommonAutoDepsOptions.TryExtract(args, out var remaining, out var error);
        error.Should().NotBeNull();
        remaining.Should().Equal("--project", "foo.csproj");
    }
}
