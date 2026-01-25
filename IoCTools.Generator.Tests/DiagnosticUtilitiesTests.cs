using IoCTools.Generator.Diagnostics;
using Xunit;

namespace IoCTools.Generator.Tests;

public class DiagnosticUtilitiesTests
{
    public class CompileIgnoredTypePatterns
    {
        [Fact]
        public void EmptyString_ReturnsDefaultPatterns()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns("");

            Assert.NotNull(result);
            Assert.Equal(4, result.Length);
        }

        [Fact]
        public void WhitespaceOnly_ReturnsDefaultPatterns()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns("   ");

            Assert.NotNull(result);
            Assert.Equal(4, result.Length);
        }

        [Fact]
        public void NullInput_ReturnsDefaultPatterns()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns(null!);

            Assert.NotNull(result);
            Assert.Equal(4, result.Length);
        }

        [Fact]
        public void SinglePattern_ReturnsCompiledRegex()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns("*.Test.*");

            Assert.NotNull(result);
            Assert.Single(result);
            // Pattern becomes ^.*\.Test\..*$ - requires dot after Test
            Assert.Matches(result[0], "MyNamespace.Test.IService");
            Assert.Matches(result[0], "MyNamespace.Test.SomeClass");
            Assert.DoesNotMatch(result[0], "Test"); // no dot before or after
            Assert.DoesNotMatch(result[0], "Namespace.Test"); // no dot after Test
        }

        [Fact]
        public void MultiplePatterns_SemicolonSeparated_ReturnsCompiledRegexes()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns("*.Abstractions.*;*.Contracts.*");

            Assert.NotNull(result);
            Assert.Equal(2, result.Length);
            Assert.Matches(result[0], "My.Abstractions.IService");
            Assert.Matches(result[1], "My.Contracts.IData");
        }

        [Fact]
        public void PatternWithWildcards_MatchesCorrectly()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns("*.Services.*");

            Assert.NotNull(result);
            var pattern = result[0];
            // Pattern becomes ^.*\.Services\..*$ - requires dot after Services
            Assert.True(pattern.IsMatch("MyCompany.Services.IUserService"));
            Assert.True(pattern.IsMatch("MyCompany.Services.SomeClass"));
            Assert.False(pattern.IsMatch("Services.MyClass")); // no dot after Services
            Assert.False(pattern.IsMatch("MyCompany.Models.MyClass"));
        }

        [Fact]
        public void PatternWithGenericNotation_CreatesValidRegex()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns("*.ILoggerService<*>");

            Assert.NotNull(result);
            Assert.Single(result);

            var pattern = result[0];
            // The pattern is created; let's just verify it matches the expected format
            Assert.Contains("ILoggerService", pattern.ToString());

            // Verify it matches one expected case
            Assert.True(pattern.IsMatch("MyNamespace.ILoggerService<MyClass>"));
        }

        [Fact]
        public void DefaultILoggerServicePattern_MatchesCorrectly()
        {
            var result = DiagnosticUtilities.GetDefaultIgnoredPatterns();
            var loggerPattern = result[3]; // Fourth pattern is ILoggerService

            // Default pattern is ^.*\.ILoggerService<.*>$ - requires dot before ILoggerService
            Assert.True(loggerPattern.IsMatch("MyNamespace.ILoggerService<string>"));
            Assert.True(loggerPattern.IsMatch("App.Logging.ILoggerService<int>"));
            Assert.False(loggerPattern.IsMatch("ILoggerService<MyClass>")); // no dot before
            Assert.False(loggerPattern.IsMatch("MyNamespace.ILoggerService")); // no generic
        }

        [Fact]
        public void EmptyPatternsInList_Skipped_ReturnsDefaultIfAllEmpty()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns("  ;  ; ");

            Assert.NotNull(result);
            // All patterns empty, should return defaults
            Assert.Equal(4, result.Length);
        }

        [Fact]
        public void MixedEmptyAndValidPatterns_UsesOnlyValid()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns("*.Valid.*;  ; *.AlsoValid.*");

            Assert.NotNull(result);
            Assert.Equal(2, result.Length);
        }
    }

    public class GetDefaultIgnoredPatterns
    {
        [Fact]
        public void ReturnsFourDefaultPatterns()
        {
            var result = DiagnosticUtilities.GetDefaultIgnoredPatterns();

            Assert.NotNull(result);
            Assert.Equal(4, result.Length);
        }

        [Fact]
        public void DefaultPatterns_AreInCorrectOrder()
        {
            var result = DiagnosticUtilities.GetDefaultIgnoredPatterns();

            // Order: Abstractions, Contracts, Interfaces, ILoggerService
            Assert.Contains("Abstractions", result[0].ToString());
            Assert.Contains("Contracts", result[1].ToString());
            Assert.Contains("Interfaces", result[2].ToString());
            Assert.Contains("ILoggerService", result[3].ToString());
        }

        [Fact]
        public void AbstractionsPattern_MatchesCorrectly()
        {
            var result = DiagnosticUtilities.GetDefaultIgnoredPatterns();
            var abstractionsPattern = result[0]; // First pattern is Abstractions

            // Pattern is ^.*\.Abstractions\..*$ - requires a dot after Abstractions
            Assert.True(abstractionsPattern.IsMatch("MyCompany.Abstractions.IService"));
            Assert.True(abstractionsPattern.IsMatch("MyCompany.Abstractions.SomeClass"));
            Assert.False(abstractionsPattern.IsMatch("Abstractions.MyClass")); // no dot after Abstractions
            Assert.False(abstractionsPattern.IsMatch("MyCompany.Models.MyClass"));
        }

        [Fact]
        public void ContractsPattern_MatchesCorrectly()
        {
            var result = DiagnosticUtilities.GetDefaultIgnoredPatterns();
            var contractsPattern = result[1]; // Second pattern is Contracts

            // Pattern is ^.*\.Contracts\..*$
            Assert.True(contractsPattern.IsMatch("MyCompany.Contracts.IData"));
            Assert.True(contractsPattern.IsMatch("MyCompany.Contracts.SomeClass"));
            Assert.False(contractsPattern.IsMatch("Contracts.MyClass")); // no dot after Contracts
            Assert.False(contractsPattern.IsMatch("MyCompany.Models.MyClass"));
        }

        [Fact]
        public void InterfacesPattern_MatchesCorrectly()
        {
            var result = DiagnosticUtilities.GetDefaultIgnoredPatterns();
            var interfacesPattern = result[2]; // Third pattern is Interfaces

            // Pattern is ^.*\.Interfaces\..*$
            Assert.True(interfacesPattern.IsMatch("MyCompany.Interfaces.IService"));
            Assert.True(interfacesPattern.IsMatch("MyCompany.Interfaces.SomeClass"));
            Assert.False(interfacesPattern.IsMatch("Interfaces.MyClass")); // no dot after Interfaces
            Assert.False(interfacesPattern.IsMatch("MyCompany.Models.MyClass"));
        }

        [Fact]
        public void ILoggerServicePattern_MatchesGenericOnly()
        {
            var result = DiagnosticUtilities.GetDefaultIgnoredPatterns();
            var loggerPattern = result[3]; // Fourth pattern is ILoggerService

            // Default pattern is ^.*\.ILoggerService<.*>$ - requires dot before ILoggerService
            Assert.True(loggerPattern.IsMatch("MyNamespace.ILoggerService<string>"));
            Assert.True(loggerPattern.IsMatch("MyNamespace.ILoggerService<int>"));
            Assert.False(loggerPattern.IsMatch("ILoggerService<MyClass>")); // no dot before
            Assert.False(loggerPattern.IsMatch("MyNamespace.ILoggerService"));
            Assert.False(loggerPattern.IsMatch("ILoggerService"));
        }
    }

    public class PatternMatchingBehavior
    {
        [Theory]
        [InlineData("*.Abstractions.*", "My.Abstractions.IService", true)]
        [InlineData("*.Abstractions.*", "My.Models.IService", false)]
        [InlineData("*.Contracts.*", "Vendor.Contracts.Data", true)]
        [InlineData("*.Contracts.*", "Vendor.Models.Data", false)]
        [InlineData("*.Interfaces.*", "Core.Interfaces.IRepository", true)]
        [InlineData("*.Interfaces.*", "Core.Models.Repository", false)]
        [InlineData("*.ILoggerService<*>", "App.ILoggerService<User>", true)]
        [InlineData("*.ILoggerService<*>", "App.ILoggerService", false)]
        [InlineData("MyNamespace.*", "MyNamespace.Models.MyClass", true)]
        [InlineData("MyNamespace.*", "OtherNamespace.Models.MyClass", false)]
        public void PatternMatchesCorrectly(string pattern, string input, bool expectedMatch)
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns(pattern);
            var matches = result[0].IsMatch(input);

            Assert.Equal(expectedMatch, matches);
        }
    }

    public class EdgeCases
    {
        [Fact]
        public void PatternWithMultipleWildcards_MatchesCorrectly()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns("*.*.*");

            Assert.NotNull(result);
            var pattern = result[0];
            Assert.True(pattern.IsMatch("A.B.C"));
            Assert.True(pattern.IsMatch("X.Y.Z.W"));
        }

        [Fact]
        public void PatternWithNoWildcards_MatchesExact()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns("Exact.Namespace.Name");

            Assert.NotNull(result);
            var pattern = result[0];
            Assert.True(pattern.IsMatch("Exact.Namespace.Name"));
            Assert.False(pattern.IsMatch("Exact.Namespace.Name.Extra"));
            Assert.False(pattern.IsMatch("Other.Namespace.Name"));
        }

        [Fact]
        public void PatternStartingWithWildcard_MatchesAnyPrefix()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns("*.Common");

            Assert.NotNull(result);
            var pattern = result[0];
            // *.Common -> Regex.Escape -> \*\.Common
            // .Replace("\\*", ".*") -> .*.Common
            // Final pattern: ^.*\.Common$
            // This matches anything ending with .Common (including .Common itself since * can be empty)

            Assert.Matches(pattern, "My.Common");
            Assert.Matches(pattern, "A.Common");
            Assert.Matches(pattern, "X.Common");
            Assert.Matches(pattern, "AB.Common"); // * can match multiple chars
            Assert.Matches(pattern, ".Common"); // * matches empty string before the dot
            Assert.DoesNotMatch(pattern, "Common"); // missing the dot
            Assert.DoesNotMatch(pattern, "Uncommon"); // doesn't end with .Common
            Assert.Matches(pattern, "My.Nested.Common"); // * matches "My.Nested" before final .Common
        }

        [Fact]
        public void PatternEndingWithWildcard_MatchesAnySuffix()
        {
            var result = DiagnosticUtilities.CompileIgnoredTypePatterns("My.*");

            Assert.NotNull(result);
            var pattern = result[0];
            // Pattern becomes ^My.*$ - matches My followed by anything (including nothing)
            Assert.True(pattern.IsMatch("My.Class"));
            Assert.True(pattern.IsMatch("My.Deep.Nested.Class"));
            Assert.True(pattern.IsMatch("My.Class"));
            Assert.False(pattern.IsMatch("NotMy.Class"));
        }
    }
}
