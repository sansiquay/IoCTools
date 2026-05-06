namespace IoCTools.Tools.Cli.Tests;

using System.Text.Json;

using CommandLine;
using FluentAssertions;

using Infrastructure;

using Xunit;

public sealed class TestScaffoldCommandTests
{
    private static string FieldsProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "FieldsProject", "FieldsProject.csproj");

    [Fact]
    public void ParseTestScaffold_RequiresProject()
    {
        var result = CommandLineParser.ParseTestScaffold(new[] { "--type", "MyApp.UserService" });
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("--project");
    }

    [Fact]
    public void ParseTestScaffold_RequiresType()
    {
        var result = CommandLineParser.ParseTestScaffold(new[] { "--project", "test.csproj" });
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("--type");
    }

    [Fact]
    public void ParseTestScaffold_Defaults()
    {
        var result = CommandLineParser.ParseTestScaffold(new[]
        {
            "--project", "/tmp/test.csproj",
            "--type", "MyApp.Services.UserService"
        });

        result.Success.Should().BeTrue();
        var opts = result.Value!;
        opts.ServiceType.Should().Be("MyApp.Services.UserService");
        opts.TestFramework.Should().Be("xunit");
        opts.Mocking.Should().Be("moq");
        opts.Assertions.Should().Be("none");
        opts.DryRun.Should().BeFalse();
        opts.Force.Should().BeFalse();
        opts.TestProjectPath.Should().BeNull();
        opts.OutputPath.Should().BeNull();
    }

    [Fact]
    public void ParseTestScaffold_ExplicitOptions()
    {
        var result = CommandLineParser.ParseTestScaffold(new[]
        {
            "--project", "/tmp/test.csproj",
            "--type", "MyApp.UserService",
            "--test-framework", "nunit",
            "--mocking", "moq",
            "--assertions", "fluentassertions",
            "--test-project", "/tmp/test/Tests.csproj",
            "--output", "/tmp/test/Services",
            "--dry-run",
            "--force",
            "--verbose"
        });

        result.Success.Should().BeTrue();
        var opts = result.Value!;
        opts.ServiceType.Should().Be("MyApp.UserService");
        opts.TestFramework.Should().Be("nunit");
        opts.Assertions.Should().Be("fluentassertions");
        opts.DryRun.Should().BeTrue();
        opts.Force.Should().BeTrue();
        opts.TestProjectPath.Should().Be("/tmp/test/Tests.csproj");
        opts.OutputPath.Should().Be("/tmp/test/Services");
        opts.Common.Verbose.Should().BeTrue();
    }

    [Fact]
    public void ParseTestScaffold_RejectsInvalidTestFramework()
    {
        var result = CommandLineParser.ParseTestScaffold(new[]
        {
            "--project", "/tmp/test.csproj",
            "--type", "MyApp.UserService",
            "--test-framework", "invalid"
        });

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("framework");
    }

    [Fact]
    public void ParseTestScaffold_RejectsInvalidAssertions()
    {
        var result = CommandLineParser.ParseTestScaffold(new[]
        {
            "--project", "/tmp/test.csproj",
            "--type", "MyApp.UserService",
            "--assertions", "nope"
        });

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("assertions");
    }

    [Theory]
    [InlineData("--test-framework", "xunit")]
    [InlineData("--test-framework", "nunit")]
    [InlineData("--test-framework", "mstest")]
    [InlineData("--framework", "xunit")]
    [InlineData("--framework", "nunit")]
    [InlineData("--framework", "mstest")]
    public void ParseTestScaffold_AcceptFrameworkAliases(string flagName, string value)
    {
        var result = CommandLineParser.ParseTestScaffold(new[]
        {
            "--project", "/tmp/test.csproj",
            "--type", "MyApp.UserService",
            flagName, value
        });

        result.Success.Should().BeTrue($"flag {flagName} with value {value} should parse");
        result.Value!.TestFramework.Should().Be(value, $"{flagName} {value} should map to TestFramework");
    }

    [Fact]
    public void ParseTestScaffold_FrameworkTfmOverride_DoesNotConflict()
    {
        // When --framework is passed with a TFM-like value (not a test framework name),
        // it should be treated as a TFM override and the test framework should default to xunit.
        var result = CommandLineParser.ParseTestScaffold(new[]
        {
            "--project", "/tmp/test.csproj",
            "--type", "MyApp.UserService",
            "--framework", "net8.0"
        });

        result.Success.Should().BeTrue();
        result.Value!.TestFramework.Should().Be("xunit", "TFM override value should not override test framework");
        result.Value!.Common.Framework.Should().Be("net8.0", "--framework net8.0 should be TFM override");
    }

    [Fact]
    public void ParseTestScaffold_FrameworkMstest_DoesNotFlowIntoCommon()
    {
        // When --framework mstest is used, it should be consumed as test framework and
        // NOT flow into CommonOptions.Framework (which is for TFM overrides only).
        var result = CommandLineParser.ParseTestScaffold(new[]
        {
            "--project", "/tmp/test.csproj",
            "--type", "MyApp.UserService",
            "--framework", "mstest"
        });

        result.Success.Should().BeTrue();
        result.Value!.TestFramework.Should().Be("mstest");
        result.Value!.Common.Framework.Should().BeNull("--framework mstest is a test framework alias, not a TFM override");
    }

    [Fact]
    public void ParseTestScaffold_FrameworkVsTestFramework_YieldsSameTestFramework()
    {
        // --framework mstest and --test-framework mstest should both set TestFramework=mstest,
        // and Common.Framework should only be set when --test-framework is used (non-aliased).
        var viaFramework = CommandLineParser.ParseTestScaffold(new[]
        {
            "--project", "/tmp/test.csproj",
            "--type", "MyApp.UserService",
            "--framework", "mstest"
        });

        var viaTestFramework = CommandLineParser.ParseTestScaffold(new[]
        {
            "--project", "/tmp/test.csproj",
            "--type", "MyApp.UserService",
            "--test-framework", "mstest"
        });

        viaFramework.Success.Should().BeTrue();
        viaTestFramework.Success.Should().BeTrue();
        viaFramework.Value!.TestFramework.Should().Be(viaTestFramework.Value!.TestFramework);
        // --framework mstest should NOT pollute Common.Framework
        viaFramework.Value!.Common.Framework.Should().BeNull();
    }

    [Fact]
    public void ParseTestScaffold_OutputDirectory_AppendsFileName()
    {
        // When --output is a directory path (ends with separator or has no extension),
        // the scaffold runner should append the test class name.
        var result = CommandLineParser.ParseTestScaffold(new[]
        {
            "--project", "/tmp/test.csproj",
            "--type", "MyApp.UserService",
            "--output", "/tmp/tests/"
        });

        result.Success.Should().BeTrue();
        result.Value!.OutputPath.Should().Be(Path.GetFullPath("/tmp/tests/"));
    }

    [Fact]
    public void ParseTestScaffold_OutputFilePath_StaysAsIs()
    {
        // When --output is a file path with .cs extension, it should be used as-is.
        var result = CommandLineParser.ParseTestScaffold(new[]
        {
            "--project", "/tmp/test.csproj",
            "--type", "MyApp.UserService",
            "--output", "/tmp/tests/UserServiceTests.cs"
        });

        result.Success.Should().BeTrue();
        result.Value!.OutputPath.Should().Be(Path.GetFullPath("/tmp/tests/UserServiceTests.cs"));
    }

    [Fact]
    public void GenerateScaffold_Xunit_DefaultAssertions()
    {
        var source = TestScaffoldRunner.GenerateTestClassSource(
            "UserServiceTests", "MyApp.Services.Tests", "MyApp.Services.UserService",
            new List<ScaffoldDependency>(), "xunit", "moq", "none");

        source.Should().Contain("using IoCTools.Testing.Annotations;");
        source.Should().Contain("using Xunit;");
        source.Should().Contain("namespace MyApp.Services.Tests;");
        source.Should().Contain("[Cover<MyApp.Services.UserService>(Logger = FixtureLoggerProfile.NullLogger)]");
        source.Should().Contain("public partial class UserServiceTests");
        source.Should().Contain("[Fact]");
        source.Should().Contain("public void Sut_ShouldConstruct()");
        source.Should().Contain("Assert.NotNull(Sut);");
    }

    [Theory]
    [InlineData("xunit")]
    [InlineData("nunit")]
    [InlineData("mstest")]
    public void GenerateScaffold_RespectsFrameworkTemplate(string framework)
    {
        var source = TestScaffoldRunner.GenerateTestClassSource(
            "ServiceTests", "App.Tests", "App.Service",
            new List<ScaffoldDependency>(), framework, "moq", "none");

        var expectedUsing = framework switch
        {
            "xunit" => "using Xunit;",
            "nunit" => "using NUnit.Framework;",
            "mstest" => "using Microsoft.VisualStudio.TestTools.UnitTesting;",
            _ => throw new ArgumentOutOfRangeException(nameof(framework))
        };
        var expectedAttribute = framework switch
        {
            "xunit" => "[Fact]",
            "nunit" => "[Test]",
            "mstest" => "[TestMethod]",
            _ => throw new ArgumentOutOfRangeException(nameof(framework))
        };

        source.Should().Contain(expectedUsing, $"{framework} should use correct using");
        source.Should().Contain(expectedAttribute, $"{framework} should use correct test attribute");
        var expectedAssert = framework == "mstest" ? "Assert.IsNotNull(Sut);" : "Assert.NotNull(Sut);";
        source.Should().Contain(expectedAssert, $"{framework} with 'none' assertions should use {expectedAssert}");
        source.Should().Contain("namespace App.Tests;", "should use provided namespace");
    }

    [Fact]
    public void GenerateScaffold_Mstest_NoneAssertion_IsValidCSharp()
    {
        // MSTest with 'none' assertion should produce Assert.IsNotNull (MSTest-native API),
        // not xUnit's Assert.NotNull.
        var source = TestScaffoldRunner.GenerateTestClassSource(
            "ServiceTests", "App.Tests", "App.Service",
            new List<ScaffoldDependency>(), "mstest", "moq", "none");

        source.Should().Contain("[TestMethod]");
        source.Should().Contain("Assert.IsNotNull(Sut);");
        source.Should().NotContain("Assert.NotNull(Sut)");
        source.Should().Contain("public void Sut_ShouldConstruct()");
        source.Should().NotContain("[Fact]");
        source.Should().NotContain("[Test]");
    }

    [Fact]
    public void GenerateScaffold_Nunit_FrameworkTemplate()
    {
        var source = TestScaffoldRunner.GenerateTestClassSource(
            "ServiceTests", "App.Tests", "App.Service",
            new List<ScaffoldDependency>(), "nunit", "moq", "fluentassertions");

        source.Should().Contain("using NUnit.Framework;");
        source.Should().Contain("[Test]");
        source.Should().Contain("using FluentAssertions;");
        source.Should().Contain("Sut.Should().NotBeNull()");
    }

    [Theory]
    [InlineData("fluentassertions", "Sut.Should().NotBeNull()")]
    [InlineData("shouldly", "Sut.ShouldNotBeNull()")]
    [InlineData("none", "Assert.NotNull(Sut)")]
    public void GenerateScaffold_AssertionStyle(string assertions, string expectedAssert)
    {
        var source = TestScaffoldRunner.GenerateTestClassSource(
            "TestServiceTests", "App.Tests", "App.TestService",
            new List<ScaffoldDependency>(), "xunit", "moq", assertions);
        source.Should().Contain(expectedAssert);
    }

    [Fact]
    public void GenerateScaffold_Mstest_AssertionStyle_UsesIsNotNull()
    {
        // MSTest uses Assert.IsNotNull instead of Assert.NotNull for 'none' assertions
        var source = TestScaffoldRunner.GenerateTestClassSource(
            "TestServiceTests", "App.Tests", "App.TestService",
            new List<ScaffoldDependency>(), "mstest", "moq", "none");
        source.Should().Contain("Assert.IsNotNull(Sut);");
        source.Should().NotContain("Assert.NotNull(Sut)");
    }

    [Fact]
    public async Task TestScaffold_Json_NonDryRun_EmitsPureJson()
    {
        var outputFile = Path.Combine(TestPaths.CreateTempDirectory(), "TelemetryReporterTests.cs");

        var result = await CliTestHost.RunAsync(
            "test",
            "scaffold",
            "--project", FieldsProjectPath,
            "--type", "FieldsProject.Services.TelemetryReporter",
            "--output", outputFile,
            "--json");

        result.ExitCode.Should().Be(0, $"stderr: {result.Stderr}");
        result.Stdout.Should().NotContain("Generated test scaffold");

        using var payload = JsonDocument.Parse(result.Stdout);
        payload.RootElement.GetProperty("OutputPath").GetString().Should().Be(Path.GetFullPath(outputFile));
        payload.RootElement.GetProperty("TestClassName").GetString().Should().Be("TelemetryReporterTests");
        File.Exists(outputFile).Should().BeTrue();
    }

    [Fact]
    public async Task TestScaffold_Json_ParseError_LeavesStdoutEmpty()
    {
        var result = await CliTestHost.RunAsync(
            "test",
            "scaffold",
            "--project", FieldsProjectPath,
            "--json");

        result.ExitCode.Should().Be(1);
        result.Stdout.Should().BeEmpty();
        result.Stderr.Should().Contain("--type");
    }

    [Fact]
    public async Task TestScaffold_Json_ServiceNotFound_LeavesStdoutEmpty()
    {
        var result = await CliTestHost.RunAsync(
            "test",
            "scaffold",
            "--project", FieldsProjectPath,
            "--type", "FieldsProject.Services.DoesNotExist",
            "--json",
            "--dry-run");

        result.ExitCode.Should().Be(1);
        result.Stdout.Should().BeEmpty();
        result.Stderr.Should().Contain("not found");
    }

    [Fact]
    public async Task TestScaffold_Json_AmbiguousType_LeavesStdoutEmpty()
    {
        var result = await CliTestHost.RunAsync(
            "test",
            "scaffold",
            "--project", FieldsProjectPath,
            "--type", "DuplicateService",
            "--json",
            "--dry-run");

        result.ExitCode.Should().Be(1);
        result.Stdout.Should().BeEmpty();
        result.Stderr.Should().Contain("Ambiguous type name");
    }

    [Fact]
    public async Task TestScaffold_Json_OutputExists_LeavesStdoutEmpty()
    {
        var outputFile = Path.Combine(TestPaths.CreateTempDirectory(), "TelemetryReporterTests.cs");
        await File.WriteAllTextAsync(outputFile, "existing");

        var result = await CliTestHost.RunAsync(
            "test",
            "scaffold",
            "--project", FieldsProjectPath,
            "--type", "FieldsProject.Services.TelemetryReporter",
            "--output", outputFile,
            "--json");

        result.ExitCode.Should().Be(1);
        result.Stdout.Should().BeEmpty();
        result.Stderr.Should().Contain("already exists");
    }
}
