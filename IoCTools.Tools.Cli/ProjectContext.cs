using System.Xml.Linq;

namespace IoCTools.Tools.Cli;

using System.Diagnostics;

using CommandLine;

using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;

internal sealed class ProjectContext : IAsyncDisposable
{
    private static bool _msbuildRegistered;
    private static string? _resolvedDotNetHostPath;
    private readonly MSBuildWorkspace _workspace;

    private ProjectContext(MSBuildWorkspace workspace,
        Project project,
        CSharpCompilation compilation)
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

    public ValueTask DisposeAsync()
    {
        _workspace.Dispose();
        return ValueTask.CompletedTask;
    }

    public static async Task<ProjectContext> CreateAsync(CommonOptions options,
        CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(options.ProjectPath);

        // Check for multi-targeting before proceeding
        CheckMultiTargeting(projectPath, options.Framework);

        if (NeedsRestore(projectPath))
            await RestoreProjectAsync(projectPath, cancellationToken);

        RegisterMsBuild(projectPath);

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Configuration", options.Configuration }
        };

        if (!string.IsNullOrWhiteSpace(options.Framework))
            properties["TargetFramework"] = options.Framework!;

        var workspace = MSBuildWorkspace.Create(properties);
        workspace.WorkspaceFailed += (_,
            args) =>
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

    private static void CheckMultiTargeting(string projectPath, string? framework)
    {
        if (!File.Exists(projectPath))
            return;

        try
        {
            var projectContent = File.ReadAllText(projectPath);
            var doc = XDocument.Parse(projectContent);
            var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");

            // Check for <TargetFrameworks> (plural) which indicates multi-targeting
            // Try with namespace first (old-style projects), then without (SDK-style)
            var targetFrameworksElement = doc.Descendants(ns + "TargetFrameworks").FirstOrDefault()
                ?? doc.Descendants("TargetFrameworks").FirstOrDefault();

            if (targetFrameworksElement != null && targetFrameworksElement.Value != null)
            {
                var tfms = targetFrameworksElement.Value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => t.Length > 0)
                    .ToArray();

                if (tfms.Length > 1 && string.IsNullOrWhiteSpace(framework))
                {
                    var tfmList = string.Join(", ", tfms);
                    throw new InvalidOperationException(
                        $"Project '{Path.GetFileName(projectPath)}' targets multiple frameworks: {tfmList}.\n" +
                        $"Use the --framework <tfm> option to specify which framework to analyze.\n" +
                        $"Example: --framework {tfms[0]}");
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException)
        {
            throw; // Re-throw our validation error
        }
        catch
        {
            // If we can't parse the project file, continue - MSBuild will handle it later
        }
    }

    private static void RegisterMsBuild(string projectPath)
    {
        if (_msbuildRegistered) return;

        var dotNetHostPath = ResolveDotNetHostPath();
        var dotNetRoot = Path.GetDirectoryName(dotNetHostPath)
                         ?? throw new InvalidOperationException($"Unable to determine DOTNET_ROOT from '{dotNetHostPath}'.");
        var sdkPath = ResolveSdkPath(dotNetHostPath, projectPath);

        Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", dotNetHostPath);
        Environment.SetEnvironmentVariable("DOTNET_ROOT", dotNetRoot);
        Environment.SetEnvironmentVariable("MSBuildSDKsPath", Path.Combine(sdkPath, "Sdks"));

        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathEntries = currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (!pathEntries.Contains(dotNetRoot, StringComparer.Ordinal))
            Environment.SetEnvironmentVariable("PATH", $"{dotNetRoot}{Path.PathSeparator}{currentPath}");

        MSBuildLocator.RegisterMSBuildPath(sdkPath);
        _msbuildRegistered = true;
    }

    private static string ResolveDotNetHostPath()
    {
        if (!string.IsNullOrWhiteSpace(_resolvedDotNetHostPath) && File.Exists(_resolvedDotNetHostPath))
            return _resolvedDotNetHostPath;

        var candidates = new List<string?>();
        candidates.Add(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH"));
        candidates.Add(Environment.ProcessPath);

        var dotNetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotNetRoot))
            candidates.Add(Path.Combine(dotNetRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate)) continue;
            if (!string.Equals(Path.GetFileNameWithoutExtension(candidate), "dotnet", StringComparison.OrdinalIgnoreCase))
                continue;

            _resolvedDotNetHostPath = candidate;
            return candidate;
        }

        _resolvedDotNetHostPath = "dotnet";
        return _resolvedDotNetHostPath;
    }

    private static string ResolveSdkPath(string dotNetHostPath,
        string projectPath)
    {
        var workingDirectory = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory();
        var sdkVersion = RunDotNet(dotNetHostPath, "--version", workingDirectory).Trim();
        if (sdkVersion.Length == 0)
            throw new InvalidOperationException($"Unable to determine .NET SDK version using '{dotNetHostPath} --version'.");

        var dotNetRoot = Path.GetDirectoryName(dotNetHostPath)
                         ?? throw new InvalidOperationException($"Unable to determine DOTNET_ROOT from '{dotNetHostPath}'.");
        var sdkPath = Path.Combine(dotNetRoot, "sdk", sdkVersion);
        if (!Directory.Exists(sdkPath))
            throw new InvalidOperationException($"Resolved SDK path '{sdkPath}' does not exist.");

        return sdkPath;
    }

    private static string RunDotNet(string dotNetHostPath,
        string arguments,
        string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = dotNetHostPath,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        var dotNetRoot = Path.GetDirectoryName(dotNetHostPath);
        if (!string.IsNullOrWhiteSpace(dotNetRoot))
        {
            psi.Environment["DOTNET_HOST_PATH"] = dotNetHostPath;
            psi.Environment["DOTNET_ROOT"] = dotNetRoot;

            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            psi.Environment["PATH"] = currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Contains(dotNetRoot, StringComparer.Ordinal)
                ? currentPath
                : $"{dotNetRoot}{Path.PathSeparator}{currentPath}";
        }

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException($"Unable to start '{dotNetHostPath} {arguments}'.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var details = string.Join(" ", new[] { stdout.Trim(), stderr.Trim() }.Where(text => text.Length > 0));
            throw new InvalidOperationException(
                $"'{dotNetHostPath} {arguments}' failed with exit code {process.ExitCode}. {details}");
        }

        return stdout;
    }

    private static async Task RestoreProjectAsync(string projectPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            throw new ArgumentException("Project path is required for restore.", nameof(projectPath));

        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var dotNetHostPath = ResolveDotNetHostPath();
        var dotNetRoot = Path.GetDirectoryName(dotNetHostPath);
        var psi = new ProcessStartInfo
        {
            FileName = dotNetHostPath,
            Arguments = $"restore \"{projectPath}\" --nologo",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = projectDirectory
        };

        if (!string.IsNullOrWhiteSpace(dotNetRoot))
        {
            psi.Environment["DOTNET_HOST_PATH"] = dotNetHostPath;
            psi.Environment["DOTNET_ROOT"] = dotNetRoot;

            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            psi.Environment["PATH"] = currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Contains(dotNetRoot, StringComparer.Ordinal)
                ? currentPath
                : $"{dotNetRoot}{Path.PathSeparator}{currentPath}";
        }

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
