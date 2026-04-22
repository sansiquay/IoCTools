namespace IoCTools.Tools.Cli.Tests;

using FluentAssertions;

using Infrastructure;

using Xunit;

/// <summary>
///     Phase 7 Task 7.9 -- headless <c>migrate-inject</c> subcommand. Sanity-checks the
///     CLI against two fixture projects: the main <c>MigrateInjectProject</c> (IoCTools
///     1.6-dev via in-tree source linking) and <c>MigrateInjectPre16Project</c> (no
///     <c>IoCTools.Abstractions</c> reference, standing in for &lt; 1.6 consumers).
/// </summary>
/// <remarks>
///     Tests that mutate files operate on a freshly-copied sandbox under the system temp
///     directory so the repository fixtures stay pristine for the next run.
/// </remarks>
[Collection("CLI Execution")]
public sealed class CliMigrateInjectCommandTests
{
    private static string MigrateInjectProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "MigrateInjectProject",
            "MigrateInjectProject.csproj");

    private static string Pre16ProjectPath =>
        TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "MigrateInjectPre16Project",
            "MigrateInjectPre16Project.csproj");

    [Fact]
    public async Task MigrateInject_dry_run_prints_diff_without_modifying_files()
    {
        var notificationPath = TestPaths.ResolveRepoPath(
            "IoCTools.Tools.Cli.Tests", "TestProjects", "MigrateInjectProject", "Services", "NotificationService.cs");
        var originalContent = await File.ReadAllTextAsync(notificationPath);

        var result = await CliTestHost.RunAsync(
            "migrate-inject",
            "--path", MigrateInjectProjectPath,
            "--dry-run");

        result.ExitCode.Should().Be(0);
        // The diff output must reference the file and show removed [Inject] lines.
        result.Stdout.Should().Contain("NotificationService.cs");
        result.Stdout.Should().Contain("[Inject]");
        result.Stdout.Should().Contain("DependsOn");
        result.Stdout.Should().Contain("dry-run");
        // File untouched.
        (await File.ReadAllTextAsync(notificationPath)).Should().Be(originalContent);
    }

    [Fact]
    public async Task MigrateInject_writes_migrated_files_without_dry_run()
    {
        var servicePath = TestPaths.ResolveRepoPath(
            "IoCTools.Tools.Cli.Tests", "TestProjects", "MigrateInjectProject", "Services", "NotificationService.cs");
        await using var sandbox = await FixtureSandbox.CaptureAsync(servicePath,
            TestPaths.ResolveRepoPath("IoCTools.Tools.Cli.Tests", "TestProjects", "MigrateInjectProject",
                "Services", "AuditService.cs"));

        var result = await CliTestHost.RunAsync("migrate-inject", "--path", MigrateInjectProjectPath);

        result.ExitCode.Should().Be(0);
        var migrated = await File.ReadAllTextAsync(servicePath);
        // Check for actual [Inject] attributes on fields, not the word inside a doc comment.
        migrated.Should().NotContain("[Inject] private");
        migrated.Should().Contain("DependsOn");
        result.Stdout.Should().Contain("Fields converted:");
    }

    [Fact]
    public async Task MigrateInject_covered_by_auto_dep_deletes_field()
    {
        var notification = TestPaths.ResolveRepoPath(
            "IoCTools.Tools.Cli.Tests", "TestProjects", "MigrateInjectProject", "Services", "NotificationService.cs");
        var auditPath = TestPaths.ResolveRepoPath(
            "IoCTools.Tools.Cli.Tests", "TestProjects", "MigrateInjectProject", "Services", "AuditService.cs");
        await using var sandbox = await FixtureSandbox.CaptureAsync(notification, auditPath);

        var result = await CliTestHost.RunAsync("migrate-inject", "--path", MigrateInjectProjectPath);

        result.ExitCode.Should().Be(0);
        var migrated = await File.ReadAllTextAsync(auditPath);
        migrated.Should().NotContain("[Inject] private readonly ILogger<AuditService>");
        // Builtin-ILogger auto-dep covers the field, so no [DependsOn<ILogger<...>>] attribute
        // should appear either (distinguishes Branch A "delete" from Branch B/C "convert" --
        // double-adding would be a regression).
        migrated.Should().NotContain("DependsOn<ILogger<AuditService>>");
    }

    [Fact]
    public async Task MigrateInject_handles_ExternalService_as_external_true()
    {
        var notification = TestPaths.ResolveRepoPath(
            "IoCTools.Tools.Cli.Tests", "TestProjects", "MigrateInjectProject", "Services", "NotificationService.cs");
        var auditPath = TestPaths.ResolveRepoPath(
            "IoCTools.Tools.Cli.Tests", "TestProjects", "MigrateInjectProject", "Services", "AuditService.cs");
        await using var sandbox = await FixtureSandbox.CaptureAsync(notification, auditPath);

        var result = await CliTestHost.RunAsync("migrate-inject", "--path", MigrateInjectProjectPath);

        result.ExitCode.Should().Be(0);
        var migrated = await File.ReadAllTextAsync(auditPath);
        migrated.Should().Contain("DependsOn");
        migrated.Should().Contain("IAuditSink");
        migrated.Should().Contain("external: true");
    }

    [Fact]
    public async Task MigrateInject_emits_notice_for_pre_1_6_Abstractions_reference()
    {
        var servicePath = TestPaths.ResolveRepoPath(
            "IoCTools.Tools.Cli.Tests", "TestProjects", "MigrateInjectPre16Project", "Services", "LegacyService.cs");
        await using var sandbox = await FixtureSandbox.CaptureAsync(servicePath);

        var result = await CliTestHost.RunAsync("migrate-inject", "--path", Pre16ProjectPath);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("Delete entirely");
        var migrated = await File.ReadAllTextAsync(servicePath);
        migrated.Should().NotContain("[Inject] private");
        migrated.Should().Contain("DependsOn");
    }

    /// <summary>
    ///     Snapshots the given fixture files on entry and restores them on dispose. The
    ///     migrate-inject CLI needs a real project to load (MSBuildWorkspace resolves relative
    ///     ProjectReferences), so we mutate the in-tree fixture in-place and roll back at the
    ///     end of the test. DisposeAsync runs even on assertion failures.
    /// </summary>
    private sealed class FixtureSandbox : IAsyncDisposable
    {
        private readonly Dictionary<string, string> _snapshot = new();

        public static async Task<FixtureSandbox> CaptureAsync(params string[] paths)
        {
            var sb = new FixtureSandbox();
            foreach (var p in paths)
            {
                sb._snapshot[p] = await File.ReadAllTextAsync(p);
            }
            return sb;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var kvp in _snapshot)
            {
                await File.WriteAllTextAsync(kvp.Key, kvp.Value);
            }
        }
    }
}
