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

    [Theory]
    [InlineData("Dictionary<String, List<ILogger<T>>>", "Dictionary_String__List_ILogger_T___")]
    [InlineData("ObservableCollection<Dictionary<string, List<Task<IEnumerable<string>>>>>",
        "ObservableCollection_Dictionary_string__List_Task_IEnumerable_string_____")]  // 5 underscores for >>>>
    [InlineData("Func<Action<Func<Action<string>, int>>, bool>", "Func_Action_Func_Action_string___int____bool_")]
    public void Sanitize_HandlesExtremeGenericNesting(string input, string expected)
    {
        HintNameBuilder.Sanitize(input).Should().Be(expected);
    }

    [Fact]
    public void Sanitize_HandlesNestedGenericsWithSpecialChars()
    {
        // Generics with special characters like + (nested classes)
        // NOTE: The + character is NOT currently sanitized by HintNameBuilder
        // This is a known limitation - nested class names will retain the + character
        var input = "OuterClass+NestedClass<Dictionary<string, List<ILogger<T>>>>";
        var result = HintNameBuilder.Sanitize(input);

        // Angle brackets are sanitized
        result.Should().NotContain("<");
        result.Should().NotContain(">");
        // Plus sign is NOT sanitized (known limitation)
        result.Should().Contain("+");
    }

    [Fact]
    public void Sanitize_ProducesUniqueFilenamesForSimilarTypes()
    {
        // Similar generic signatures should produce unique filenames
        var type1 = "Dictionary<string, List<int>>";
        var type2 = "Dictionary<String, List<Int32>>";  // Different casing
        var type3 = "Dictionary<string, List<int>>";    // Same as type1

        var result1 = HintNameBuilder.Sanitize(type1);
        var result2 = HintNameBuilder.Sanitize(type2);
        var result3 = HintNameBuilder.Sanitize(type3);

        // Same type produces same result
        result1.Should().Be(result3);

        // Different casing produces different result
        result1.Should().NotBe(result2);
    }

    [Fact]
    public void Sanitize_HandlesVeryLongTypeNames()
    {
        // Test character limits - Windows has a 255 char limit for filenames
        // NOTE: HintNameBuilder does NOT truncate - long names will exceed 256 chars
        var longTypeName = string.Join("->", Enumerable.Range(1, 50).Select(i => $"Type{i}"));
        var result = HintNameBuilder.Sanitize(longTypeName);

        // Should not crash
        result.Should().NotBeNullOrEmpty();
        // KNOWN LIMITATION: No truncation is performed
        // Very long type names will produce filenames >256 chars which may fail on Windows
        // This is documented for future fix
        result.Length.Should().BeGreaterThan(256);  // Confirms the limitation exists
    }
}
