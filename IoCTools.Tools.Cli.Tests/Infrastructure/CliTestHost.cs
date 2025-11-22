namespace IoCTools.Tools.Cli.Tests.Infrastructure;

using System.Text;

internal static class CliTestHost
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<CliInvocationResult> RunAsync(params string[] args)
    {
        await Gate.WaitAsync();
        try
        {
            var originalOut = Console.Out;
            var originalErr = Console.Error;
            var originalCwd = Environment.CurrentDirectory;
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            Console.SetOut(stdout);
            Console.SetError(stderr);
            Environment.CurrentDirectory = TestPaths.RepoRoot;

            try
            {
                var exitCode = await Program.Main(args);
                return new CliInvocationResult(exitCode, stdout.ToString(), stderr.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
                Environment.CurrentDirectory = originalCwd;
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}

internal sealed record CliInvocationResult(int ExitCode, string Stdout, string Stderr)
{
    public string FirstOutputLine =>
        Stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ??
        string.Empty;
}
