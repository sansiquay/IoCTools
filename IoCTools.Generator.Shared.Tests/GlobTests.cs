namespace IoCTools.Generator.Shared.Tests;

using FluentAssertions;
using IoCTools.Generator.Shared;
using Xunit;

public sealed class GlobTests
{
    [Fact]
    public void Star_matches_namespace_component()
    {
        AutoDepsResolver.GlobMatch("MyApp.Admin.Controllers.Foo", "*.Controllers.*", out var invalid)
            .Should().BeTrue();
        invalid.Should().BeFalse();
    }

    [Fact]
    public void Star_does_not_match_non_matching_namespace()
    {
        AutoDepsResolver.GlobMatch("MyApp.Production.Foo", "*.Test.*", out var invalid)
            .Should().BeFalse();
        invalid.Should().BeFalse();
    }

    [Fact]
    public void Empty_pattern_returns_false_with_invalid_flag()
    {
        AutoDepsResolver.GlobMatch("AnyInput", "", out var invalid).Should().BeFalse();
        invalid.Should().BeTrue();
    }

    [Fact]
    public void Null_pattern_returns_false_with_invalid_flag()
    {
        AutoDepsResolver.GlobMatch("AnyInput", null!, out var invalid).Should().BeFalse();
        invalid.Should().BeTrue();
    }

    [Fact]
    public void Question_mark_matches_single_character()
    {
        AutoDepsResolver.GlobMatch("abc", "a?c", out _).Should().BeTrue();
    }

    [Fact]
    public void Question_mark_does_not_match_empty()
    {
        AutoDepsResolver.GlobMatch("ac", "a?c", out _).Should().BeFalse();
    }

    [Fact]
    public void Dot_in_pattern_matches_literal_dot_only()
    {
        // '.' is NOT treated as regex-any; it's regex-escaped to '\.'
        AutoDepsResolver.GlobMatch("abc", "a.c", out _).Should().BeFalse();
        AutoDepsResolver.GlobMatch("a.c", "a.c", out _).Should().BeTrue();
    }

    [Fact]
    public void Star_matches_empty()
    {
        AutoDepsResolver.GlobMatch("ab", "a*b", out _).Should().BeTrue();
    }

    [Fact]
    public void Full_string_match_is_anchored()
    {
        // Pattern must match full input; "bc" should NOT match "abc"
        AutoDepsResolver.GlobMatch("abc", "bc", out _).Should().BeFalse();
    }

    [Fact]
    public void Malformed_pattern_reports_invalid()
    {
        // Unterminated character class triggers regex exception
        AutoDepsResolver.GlobMatch("anything", "[unterminated", out var invalid).Should().BeFalse();
        invalid.Should().BeTrue();
    }
}
