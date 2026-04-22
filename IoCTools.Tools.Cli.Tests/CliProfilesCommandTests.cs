namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using Xunit;

/// <summary>
/// Phase 7 Task 7.8 — the new <c>profiles</c> (plural) subcommand introspects auto-deps
/// profiles: list mode shows every <see cref="IoCTools.Abstractions.Annotations.IAutoDepsProfile"/>
/// with its contributed deps; <c>--matches</c> adds attached services; a positional
/// <c>ProfileName</c> drills into a single profile. Ambiguous simple names must exit
/// non-zero and list fully-qualified candidates.
/// </summary>
[Collection("CLI Execution")]
public sealed class CliProfilesCommandTests
{
    private static string ProfilesProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "ProfilesProject",
            "ProfilesProject.csproj");

    [Fact]
    public async Task Profiles_list_mode_shows_all_profiles_with_deps()
    {
        var result = await CliTestHost.RunAsync(
            "profiles",
            "--project", ProfilesProjectPath);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("ControllerDefaults");
        result.Stdout.Should().Contain("WorkerDefaults");
        // Contributed deps from [assembly: AutoDepIn<...>] should be listed per profile.
        result.Stdout.Should().Contain("IMediator");
        result.Stdout.Should().Contain("IMapper");
        result.Stdout.Should().Contain("IMetrics");
    }

    [Fact]
    public async Task Profiles_with_matches_flag_includes_service_attachment_list()
    {
        var result = await CliTestHost.RunAsync(
            "profiles",
            "--project", ProfilesProjectPath,
            "--matches");

        result.ExitCode.Should().Be(0);
        // ControllerDefaults is attached via [assembly: AutoDepsApply<ControllerDefaults, ControllerBase>]
        // so every service deriving from ControllerBase should appear under it.
        result.Stdout.Should().Contain("OrderController");
        result.Stdout.Should().Contain("UserController");
        // WorkerDefaults is attached via AutoDepsApplyGlob("ProfilesProject.Services.Background*").
        result.Stdout.Should().Contain("BackgroundWorker");
    }

    [Fact]
    public async Task Profiles_drill_by_simple_name_shows_profile_detail()
    {
        var result = await CliTestHost.RunAsync(
            "profiles",
            "ControllerDefaults",
            "--project", ProfilesProjectPath);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("ControllerDefaults");
        result.Stdout.Should().Contain("IMediator");
        result.Stdout.Should().Contain("IMapper");
        result.Stdout.Should().Contain("OrderController");
        result.Stdout.Should().Contain("UserController");
        // Drill mode surfaces the attachment source line.
        result.Stdout.Should().Contain("AutoDepsApply");
    }

    [Fact]
    public async Task Profiles_drill_by_fully_qualified_name_shows_profile_detail()
    {
        var result = await CliTestHost.RunAsync(
            "profiles",
            "ProfilesProject.Services.ControllerDefaults",
            "--project", ProfilesProjectPath);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("ControllerDefaults");
        result.Stdout.Should().Contain("IMediator");
    }

    [Fact]
    public async Task Profiles_ambiguous_simple_name_exits_nonzero_with_candidates()
    {
        var result = await CliTestHost.RunAsync(
            "profiles",
            "Defaults",
            "--project", ProfilesProjectPath);

        result.ExitCode.Should().NotBe(0);
        result.Stdout.Should().Contain("Ambiguous");
        result.Stdout.Should().Contain("ProfilesProject.Services.Defaults");
        result.Stdout.Should().Contain("ProfilesProject.Services.Alt.Defaults");
    }
}
