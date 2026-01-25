using IoCTools.Generator.Utilities;
using Xunit;

namespace IoCTools.Generator.Tests;

public class AttributeParserTests
{
    public class GenerateFieldName
    {
        [Fact]
        public void DefaultParameters_InterfaceType_GeneratesCamelCaseWithUnderscore()
        {
            var result = AttributeParser.GenerateFieldName("IService", "CamelCase", true, "_");
            Assert.Equal("_service", result);
        }

        [Fact]
        public void DefaultParameters_NonInterfaceType_GeneratesCamelCaseWithUnderscore()
        {
            var result = AttributeParser.GenerateFieldName("MyService", "CamelCase", true, "_");
            Assert.Equal("_myService", result);
        }

        [Fact]
        public void StripIFalse_InterfaceType_StillStripsIForSemanticNaming()
        {
            // The stripI parameter no longer affects semantic naming - interfaces always strip 'I'
            var result = AttributeParser.GenerateFieldName("IService", "CamelCase", false, "_");
            Assert.Equal("_service", result);
        }

        [Fact]
        public void SingleLetterInterface_DoesNotStripI()
        {
            // "I" alone doesn't have uppercase second character, so it's not treated as interface
            var result = AttributeParser.GenerateFieldName("I", "CamelCase", true, "_");
            Assert.Equal("_i", result);
        }

        [Fact]
        public void SingleCharacterTypeName_HandlesCorrectly()
        {
            var result = AttributeParser.GenerateFieldName("X", "CamelCase", true, "_");
            Assert.Equal("_x", result);
        }
    }

    public class NamingConventionTests
    {
        [Fact]
        public void CamelCase_ConvertsFirstCharacterToLower()
        {
            var result = AttributeParser.GenerateFieldName("MyService", "CamelCase", true, "_");
            Assert.Equal("_myService", result);
        }

        [Fact]
        public void PascalCase_ConvertsFirstCharacterToUpper()
        {
            var result = AttributeParser.GenerateFieldName("MyService", "PascalCase", true, "_");
            Assert.Equal("_MyService", result);
        }

        [Fact]
        public void SnakeCase_ConvertsToSnakeCase()
        {
            var result = AttributeParser.GenerateFieldName("MyService", "SnakeCase", true, "_");
            Assert.Equal("_my_service", result);
        }

        [Fact]
        public void SnakeCase_Acronym_HandlesMultipleUppercase()
        {
            var result = AttributeParser.GenerateFieldName("XMLParser", "SnakeCase", true, "_");
            Assert.Equal("_x_m_l_parser", result);
        }

        [Fact]
        public void SnakeCase_SingleWord_Lowercases()
        {
            var result = AttributeParser.GenerateFieldName("Service", "SnakeCase", true, "_");
            Assert.Equal("_service", result);
        }

        [Fact]
        public void UnknownNamingConvention_LeavesNameUnchanged()
        {
            var result = AttributeParser.GenerateFieldName("MyService", "UnknownConvention", true, "_");
            Assert.Equal("_MyService", result);
        }

        [Fact]
        public void SnakeCase_WithNumbers_PreservesNumbers()
        {
            var result = AttributeParser.GenerateFieldName("My2Service3", "SnakeCase", true, "_");
            Assert.Equal("_my2_service3", result);
        }
    }

    public class PrefixTests
    {
        [Fact]
        public void EmptyPrefix_NoUnderscoreAdded()
        {
            var result = AttributeParser.GenerateFieldName("MyService", "CamelCase", true, "");
            Assert.Equal("myService", result);
        }

        [Fact]
        public void DefaultUnderscorePrefix_AddsUnderscoreBeforeName()
        {
            var result = AttributeParser.GenerateFieldName("MyService", "CamelCase", true, "_");
            Assert.Equal("_myService", result);
        }

        [Fact]
        public void CustomPrefixWithUnderscore_PrefixesAsIs()
        {
            var result = AttributeParser.GenerateFieldName("MyService", "CamelCase", true, "m_");
            Assert.Equal("m_myService", result);
        }

        [Fact]
        public void CustomPrefixWithUnderscore_LongerPrefix()
        {
            var result = AttributeParser.GenerateFieldName("MyService", "CamelCase", true, "dep_");
            Assert.Equal("dep_myService", result);
        }

        [Fact]
        public void CustomPrefixWithoutUnderscore_AddsUnderscoreAfterPrefix()
        {
            var result = AttributeParser.GenerateFieldName("MyService", "CamelCase", true, "m");
            Assert.Equal("_mMyService", result);
        }

        [Fact]
        public void CustomPrefixWithoutUnderscore_SnakeCase_AppliesToCombinedName()
        {
            // Prefix "dep" without underscore: combines with type, then snake_case applies to combined name
            var result = AttributeParser.GenerateFieldName("MyService", "SnakeCase", true, "dep");
            Assert.Equal("_dep_my_service", result);
        }

        [Fact]
        public void EmptyPrefix_WithPascalCase_HasNoPrefix()
        {
            var result = AttributeParser.GenerateFieldName("MyService", "PascalCase", true, "");
            Assert.Equal("MyService", result);
        }

        [Fact]
        public void EmptyPrefix_WithSnakeCase_HasNoPrefix()
        {
            var result = AttributeParser.GenerateFieldName("MyService", "SnakeCase", true, "");
            Assert.Equal("my_service", result);
        }

        [Fact]
        public void MultipleCharacterPrefixWithoutUnderscore()
        {
            var result = AttributeParser.GenerateFieldName("Service", "CamelCase", true, "svc");
            Assert.Equal("_svcService", result);
        }

        [Fact]
        public void AllUnderscoresPrefix_PreservesUnderscores()
        {
            var result = AttributeParser.GenerateFieldName("Service", "CamelCase", true, "__");
            Assert.Equal("__service", result);
        }
    }

    public class ReservedKeywordTests
    {
        [Fact]
        public void ReservedKeyword_int_WithUnderscorePrefix_NotEscaped()
        {
            // With underscore prefix, "_int" is not a reserved keyword
            var result = AttributeParser.GenerateFieldName("int", "CamelCase", true, "_");
            Assert.Equal("_int", result);
        }

        [Fact]
        public void ReservedKeyword_string_WithUnderscorePrefix_NotEscaped()
        {
            var result = AttributeParser.GenerateFieldName("string", "CamelCase", true, "_");
            Assert.Equal("_string", result);
        }

        [Fact]
        public void ReservedKeyword_bool_WithUnderscorePrefix_NotEscaped()
        {
            var result = AttributeParser.GenerateFieldName("bool", "CamelCase", true, "_");
            Assert.Equal("_bool", result);
        }

        [Fact]
        public void ReservedKeyword_if_WithUnderscorePrefix_NotEscaped()
        {
            var result = AttributeParser.GenerateFieldName("if", "CamelCase", true, "_");
            Assert.Equal("_if", result);
        }

        [Fact]
        public void ReservedKeyword_class_WithUnderscorePrefix_NotEscaped()
        {
            var result = AttributeParser.GenerateFieldName("class", "CamelCase", true, "_");
            Assert.Equal("_class", result);
        }

        [Fact]
        public void ReservedKeyword_void_WithUnderscorePrefix_NotEscaped()
        {
            var result = AttributeParser.GenerateFieldName("void", "CamelCase", true, "_");
            Assert.Equal("_void", result);
        }

        [Fact]
        public void ReservedKeyword_return_WithUnderscorePrefix_NotEscaped()
        {
            var result = AttributeParser.GenerateFieldName("return", "CamelCase", true, "_");
            Assert.Equal("_return", result);
        }

        [Fact]
        public void NonReservedKeyword_DoesNotAppendValue()
        {
            var result = AttributeParser.GenerateFieldName("myService", "CamelCase", true, "_");
            Assert.Equal("_myService", result);
        }

        [Fact]
        public void ReservedKeyword_WithEmptyPrefix_AppendsValue()
        {
            // With empty prefix, the field name matches the reserved keyword
            var result = AttributeParser.GenerateFieldName("int", "CamelCase", true, "");
            Assert.Equal("intValue", result);
        }

        [Fact]
        public void ReservedKeyword_WithCustomPrefix_NotEscaped()
        {
            // Custom prefix makes it not match the reserved keyword
            var result = AttributeParser.GenerateFieldName("int", "CamelCase", true, "m_");
            Assert.Equal("m_int", result);
        }

        // Sample of C# reserved keywords with empty prefix
        [Theory]
        [InlineData("abstract")]
        [InlineData("as")]
        [InlineData("base")]
        [InlineData("bool")]
        [InlineData("break")]
        [InlineData("byte")]
        [InlineData("case")]
        [InlineData("catch")]
        [InlineData("char")]
        [InlineData("checked")]
        [InlineData("class")]
        [InlineData("const")]
        [InlineData("continue")]
        [InlineData("decimal")]
        [InlineData("default")]
        [InlineData("delegate")]
        [InlineData("do")]
        [InlineData("double")]
        [InlineData("else")]
        [InlineData("enum")]
        [InlineData("event")]
        [InlineData("explicit")]
        [InlineData("extern")]
        [InlineData("false")]
        [InlineData("finally")]
        [InlineData("fixed")]
        [InlineData("float")]
        [InlineData("for")]
        [InlineData("foreach")]
        [InlineData("goto")]
        [InlineData("if")]
        [InlineData("implicit")]
        [InlineData("in")]
        [InlineData("int")]
        [InlineData("interface")]
        [InlineData("internal")]
        [InlineData("is")]
        [InlineData("lock")]
        [InlineData("long")]
        [InlineData("namespace")]
        [InlineData("new")]
        [InlineData("null")]
        [InlineData("object")]
        [InlineData("operator")]
        [InlineData("out")]
        [InlineData("override")]
        [InlineData("params")]
        [InlineData("private")]
        [InlineData("protected")]
        [InlineData("public")]
        [InlineData("readonly")]
        [InlineData("ref")]
        [InlineData("return")]
        [InlineData("sbyte")]
        [InlineData("sealed")]
        [InlineData("short")]
        [InlineData("sizeof")]
        [InlineData("stackalloc")]
        [InlineData("static")]
        [InlineData("string")]
        [InlineData("struct")]
        [InlineData("switch")]
        [InlineData("this")]
        [InlineData("throw")]
        [InlineData("true")]
        [InlineData("try")]
        [InlineData("typeof")]
        [InlineData("uint")]
        [InlineData("ulong")]
        [InlineData("unchecked")]
        [InlineData("unsafe")]
        [InlineData("ushort")]
        [InlineData("using")]
        [InlineData("virtual")]
        [InlineData("void")]
        [InlineData("volatile")]
        [InlineData("while")]
        public void AllCSharpReservedKeywords_WithEmptyPrefix_AppendValue(string keyword)
        {
            var result = AttributeParser.GenerateFieldName(keyword, "CamelCase", true, "");
            Assert.Equal($"{keyword}Value", result);
        }

        [Fact]
        public void ReservedKeywordCaseSensitive_AfterCamelCaseBecomesKeyword()
        {
            // "String" with capital S is not a reserved keyword initially
            // But after camelCase conversion, it becomes "string" which IS a reserved keyword
            var result = AttributeParser.GenerateFieldName("String", "CamelCase", true, "");
            Assert.Equal("stringValue", result); // camelCase makes it "string", then it gets escaped
        }

        [Fact]
        public void InterfaceTypeThatMatchesReservedKeyword_HandlesCorrectly()
        {
            // "Iint" -> strips 'I' -> "int" -> with underscore prefix, not a reserved keyword
            var result = AttributeParser.GenerateFieldName("Iint", "CamelCase", true, "_");
            Assert.Equal("_iint", result);
        }
    }

    public class EdgeCases
    {
        [Fact]
        public void TypeNameStartingWithI_LowercaseSecondChar_NotAnInterface()
        {
            // "I" followed by lowercase is not an interface pattern
            var result = AttributeParser.GenerateFieldName("Iphone", "CamelCase", true, "_");
            Assert.Equal("_iphone", result);
        }

        [Fact]
        public void TypeNameStartingWithI_DigitSecondChar_NotAnInterface()
        {
            // "I" followed by digit is not an interface pattern
            var result = AttributeParser.GenerateFieldName("I123", "CamelCase", true, "_");
            Assert.Equal("_i123", result);
        }

        [Fact]
        public void TypeNameWithMultipleLeadingI_OnlyStripsOne()
        {
            var result = AttributeParser.GenerateFieldName("IIService", "CamelCase", true, "_");
            Assert.Equal("_iService", result);
        }

        [Fact]
        public void TypeNameWithUnderscores_PreservesUnderscores()
        {
            var result = AttributeParser.GenerateFieldName("My_Service", "CamelCase", true, "_");
            Assert.Equal("_my_Service", result);
        }

        [Fact]
        public void TypeNameWithNumbers_PreservesNumbers()
        {
            var result = AttributeParser.GenerateFieldName("My2Service", "CamelCase", true, "_");
            Assert.Equal("_my2Service", result);
        }
    }

    public class StripConfigurationSuffixes
    {
        [Fact]
        public void SettingsSuffix_RemovesOnce()
        {
            var result = AttributeParser.StripConfigurationSuffixes("MySettings");
            Assert.Equal("My", result);
        }

        [Fact]
        public void ConfigurationSuffix_RemovesOnce()
        {
            var result = AttributeParser.StripConfigurationSuffixes("MyConfiguration");
            Assert.Equal("My", result);
        }

        [Fact]
        public void OptionsSuffix_RemovesOnce()
        {
            var result = AttributeParser.StripConfigurationSuffixes("MyOptions");
            Assert.Equal("My", result);
        }

        [Fact]
        public void DuplicateSettingsSettings_RemovesToOne()
        {
            var result = AttributeParser.StripConfigurationSuffixes("MySettingsSettings");
            Assert.Equal("MySettings", result);
        }

        [Fact]
        public void TripleSuffix_RemovesOnlyOne()
        {
            // Only removes ONE suffix, not multiple
            var result = AttributeParser.StripConfigurationSuffixes("MyOptionsOptionsOptions");
            Assert.Equal("MyOptionsOptions", result);
        }

        [Fact]
        public void MixedSuffixes_RemovesActualSuffix()
        {
            var result = AttributeParser.StripConfigurationSuffixes("MySettingsOptions");
            // The string ends with "Options", so that's what gets removed
            // The suffixes array is checked in order, but EndsWith determines actual match
            Assert.Equal("MySettings", result);
        }

        [Fact]
        public void EmptyString_ReturnsEmpty()
        {
            var result = AttributeParser.StripConfigurationSuffixes("");
            Assert.Equal("", result);
        }

        [Fact]
        public void WhitespaceOnly_ReturnsOriginal()
        {
            var result = AttributeParser.StripConfigurationSuffixes("   ");
            Assert.Equal("   ", result);
        }

        [Fact]
        public void NoSuffix_ReturnsOriginal()
        {
            var result = AttributeParser.StripConfigurationSuffixes("MyService");
            Assert.Equal("MyService", result);
        }

        [Fact]
        public void OnlySuffix_ReturnsOriginal()
        {
            // If removing the suffix would drop everything, bail out
            var result = AttributeParser.StripConfigurationSuffixes("Settings");
            Assert.Equal("Settings", result);
        }

        [Fact]
        public void SuffixInMiddle_NotRemoved()
        {
            var result = AttributeParser.StripConfigurationSuffixes("MySettingsService");
            Assert.Equal("MySettingsService", result); // Only checks end of string
        }

        [Fact]
        public void CaseSensitive_NotRemoved()
        {
            var result = AttributeParser.StripConfigurationSuffixes("Mysettings");
            Assert.Equal("Mysettings", result);
        }

        [Fact]
        public void ComplexName_WithConfiguration_Removes()
        {
            var result = AttributeParser.StripConfigurationSuffixes("JitterConfiguration");
            Assert.Equal("Jitter", result);
        }
    }

    public class GenerateConfigurationFieldName
    {
        [Fact]
        public void StripSettingsTrue_RemovesSettingsSuffix()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("MySettings", "CamelCase", true, "_", true);
            Assert.Equal("_my", result);
        }

        [Fact]
        public void StripSettingsTrue_RemovesConfigurationSuffix()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("MyConfiguration", "CamelCase", true, "_", true);
            Assert.Equal("_my", result);
        }

        [Fact]
        public void StripSettingsTrue_RemovesOptionsSuffix()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("MyOptions", "CamelCase", true, "_", true);
            Assert.Equal("_my", result);
        }

        [Fact]
        public void StripSettingsFalse_KeepsSuffix()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("MySettings", "CamelCase", true, "_", false);
            Assert.Equal("_mySettings", result);
        }

        [Fact]
        public void StripSettingsTrue_InterfaceConfiguration_HandlesCorrectly()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("IAppSettings", "CamelCase", true, "_", true);
            Assert.Equal("_app", result); // Strips 'I' from interface, then removes "Settings"
        }

        [Fact]
        public void OnlySuffix_DoesNotStrip()
        {
            // If stripping would leave nothing, use the original name
            var result = AttributeParser.GenerateConfigurationFieldName("Settings", "CamelCase", true, "_", true);
            Assert.Equal("_settings", result);
        }

        [Fact]
        public void WhitespaceAfterStrip_UsesOriginalWithWhitespace()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("Settings   ", "CamelCase", true, "_", true);
            // After stripping "Settings", we get "   " (whitespace)
            // IsNullOrWhitespace returns true, so it uses the original "Settings   "
            // Which then gets camelCased to "settings   "
            Assert.Equal("_settings   ", result);
        }

        [Fact]
        public void WithCustomPrefix_StripsSuffixThenAppliesPrefix()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("MySettings", "CamelCase", true, "m_", true);
            Assert.Equal("m_my", result);
        }

        [Fact]
        public void WithPascalCase_StripsSuffixThenAppliesConvention()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("MyOptions", "PascalCase", true, "_", true);
            Assert.Equal("_My", result);
        }

        [Fact]
        public void WithSnakeCase_StripsSuffixThenAppliesConvention()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("MyConfiguration", "SnakeCase", true, "_", true);
            Assert.Equal("_my", result);
        }

        [Fact]
        public void DuplicateSuffixes_CollapsesToOne()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("MyOptionsOptions", "CamelCase", true, "_", true);
            Assert.Equal("_myOptions", result);
        }

        [Fact]
        public void NoSuffix_ReturnsNormalFieldName()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("MyConfig", "CamelCase", true, "_", true);
            Assert.Equal("_myConfig", result);
        }

        [Fact]
        public void ReservedKeywordWithSuffix_WithUnderscoreNotEscaped()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("intSettings", "CamelCase", true, "_", true);
            Assert.Equal("_int", result);
        }
    }

    public class ComplexScenarios
    {
        [Fact]
        public void GenericTypeName_IgnoresGenericPart()
        {
            // The method only deals with the type name, not generic args
            var result = AttributeParser.GenerateFieldName("IList", "CamelCase", true, "_");
            Assert.Equal("_list", result);
        }

        [Fact]
        public void LongInterfaceName_StripsIAndAppliesConvention()
        {
            var result = AttributeParser.GenerateFieldName("IUserRepositoryService", "SnakeCase", true, "_");
            Assert.Equal("_user_repository_service", result);
        }

        [Fact]
        public void ConfigurationWithMultipleSuffixes_StripsThenStripsI()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("IAppSettingsOptions", "SnakeCase", true, "m_", true);
            // StripConfigurationSuffixes("IAppSettingsOptions") -> removes "Options" -> "IAppSettings"
            // ExtractSemanticFieldName("IAppSettings") -> strips 'I' -> "AppSettings"
            // SnakeCase -> "app_settings"
            Assert.Equal("m_app_settings", result);
        }

        [Fact]
        public void NestedAcronyms_SnakeCaseHandles()
        {
            var result = AttributeParser.GenerateFieldName("XMLHttpRequest", "SnakeCase", true, "_");
            Assert.Equal("_x_m_l_http_request", result);
        }

        [Fact]
        public void EmptyPrefixWithPascalCase_NoUnderscore()
        {
            var result = AttributeParser.GenerateFieldName("IService", "PascalCase", true, "");
            Assert.Equal("Service", result);
        }

        [Fact]
        public void ReservedKeywordWithEmptyPrefix_EscapesCorrectly()
        {
            var result = AttributeParser.GenerateFieldName("string", "CamelCase", true, "");
            Assert.Equal("stringValue", result);
        }

        [Fact]
        public void InterfaceWithReservedKeyword_WithUnderscoreNotEscaped()
        {
            var result = AttributeParser.GenerateFieldName("Iint", "CamelCase", true, "_");
            Assert.Equal("_iint", result); // Strips 'I', but "_iint" is not a reserved keyword
        }

        [Fact]
        public void ConfigurationNameWithConfiguration_HandlesCorrectly()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("DatabaseConnectionSettingsConfiguration", "CamelCase", true, "_", true);
            // Removes "Configuration" suffix first
            Assert.Equal("_databaseConnectionSettings", result);
        }

        [Fact]
        public void RealWorldDatabaseSettings()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("DatabaseSettings", "CamelCase", true, "_", true);
            Assert.Equal("_database", result);
        }

        [Fact]
        public void RealWorldAppOptions()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("AppOptions", "CamelCase", true, "_", true);
            Assert.Equal("_app", result);
        }

        [Fact]
        public void RealWorldServiceConfiguration()
        {
            var result = AttributeParser.GenerateConfigurationFieldName("EmailServiceConfiguration", "SnakeCase", true, "m_", true);
            Assert.Equal("m_email_service", result);
        }
    }
}
