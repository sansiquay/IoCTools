namespace IoCTools.Tools.Cli;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using IoCTools.Tools.Cli.CommandLine;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;

internal sealed class ProjectContext : IAsyncDisposable
{
    private readonly MSBuildWorkspace _workspace;
    private static bool _msbuildRegistered;

    private ProjectContext(MSBuildWorkspace workspace, Project project, CSharpCompilation compilation)
    {
        _workspace = workspace;
        Project = project;
        Compilation = compilation;
        ProjectDirectory = project.FilePath != null
            ? Path.GetDirectoryName(project.FilePath) ?? Directory.GetCurrentDirectory()
            : Directory.GetCurrentDirectory();
    }

    public Project Project { get; }
    public CSharpCompilation Compilation { get; }
    public string ProjectDirectory { get; }

    public static async Task<ProjectContext> CreateAsync(CommonOptions options, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(options.ProjectPath);
        if (NeedsRestore(projectPath))
            await RestoreProjectAsync(projectPath, cancellationToken);

        RegisterMsBuild();

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Configuration", options.Configuration }
        };

        if (!string.IsNullOrWhiteSpace(options.Framework))
            properties["TargetFramework"] = options.Framework!;

        var workspace = MSBuildWorkspace.Create(properties);
        workspace.WorkspaceFailed += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Diagnostic.Message))
                Console.Error.WriteLine($"[msbuild] {args.Diagnostic.Message}");
        };

        var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
        var compilation = await project.GetCompilationAsync(cancellationToken) as CSharpCompilation;
        if (compilation == null)
            throw new InvalidOperationException("Unable to compile project for analysis.");

        return new ProjectContext(workspace, project, compilation);
    }

    private static void RegisterMsBuild()
    {
        if (_msbuildRegistered) return;
        MSBuildLocator.RegisterDefaults();
        _msbuildRegistered = true;
    }

    public ValueTask DisposeAsync()
    {
        _workspace.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async Task RestoreProjectAsync(string projectPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            throw new ArgumentException("Project path is required for restore.", nameof(projectPath));

        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"restore \"{projectPath}\" --nologo",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = projectDirectory
        };

        using var process = Process.Start(psi)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stdout = (await stdoutTask).Trim();
            var stderr = (await stderrTask).Trim();
            var details = string.Join(" ", new[] { stdout, stderr }.Where(s => !string.IsNullOrEmpty(s)));
            throw new InvalidOperationException(
                $"dotnet restore failed for '{projectPath}' (exit {process.ExitCode}). {details}");
        }
    }

    private static bool NeedsRestore(string projectPath)
    {
        var assetsPath = Path.Combine(Path.GetDirectoryName(projectPath) ?? string.Empty, "obj", "project.assets.json");
        return !File.Exists(assetsPath);
    }
}
