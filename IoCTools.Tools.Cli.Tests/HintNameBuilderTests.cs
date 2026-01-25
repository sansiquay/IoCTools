namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;
using Xunit;

public class HintNameBuilderTests
{
    [Theory]
    [InlineData("Simple", "Simple")]
    [InlineData("With Spaces", "With_Spaces")]
    [InlineData("With.Dots", "With_Dots")]
    [InlineData("With<Generics>", "With_Generics_")]
    [InlineData("With,Comma", "With_Comma")]
    public void Sanitize_ReplacesBasicChars(string input, string expected)
    {
        HintNameBuilder.Sanitize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("  TrimMe  ", "TrimMe")]
    [InlineData("Trim.Me.", "Trim_Me")]
    [InlineData(" . ", "_")] // Trim becomes empty -> fallback
    public void Sanitize_TrimsAndHandlesEmpty(string input, string expected)
    {
        HintNameBuilder.Sanitize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("CON", "_CON")]
    [InlineData("prn", "_prn")]
    [InlineData("Aux", "_Aux")]
    [InlineData("NUL", "_NUL")]
    [InlineData("Com1", "_Com1")]
    [InlineData("Lpt9", "_Lpt9")]
    public void Sanitize_HandlesReservedNames(string input, string expected)
    {
        HintNameBuilder.Sanitize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Col:on", "Col_on")]
    [InlineData("Pi|pe", "Pi_pe")]
    [InlineData("Que?stion", "Que_stion")]
    [InlineData("Ast*erisk", "Ast_erisk")]
    [InlineData("Qu\"ote", "Qu_ote")]
    [InlineData("Back\\slash", "Back_slash")]
    [InlineData("For/ward", "For_ward")]
    [InlineData("<Less", "_Less")]
    [InlineData(">Greater", "_Greater")]
    public void Sanitize_ReplacesInvalidChars(string input, string expected)
    {
        HintNameBuilder.Sanitize(input).Should().Be(expected);
    }
    
    [Fact]
    public void Sanitize_ReplacesControlChars()
    {
        var input = "Control\u001fChar";
        var expected = "Control_Char";
        HintNameBuilder.Sanitize(input).Should().Be(expected);
    }
}
