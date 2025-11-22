namespace IoCTools.Tools.Cli;

internal static class UsagePrinter
{
    public static void Write()
    {
        Console.WriteLine("IoCTools CLI");
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet ioc-tools fields --project <csproj> --file <cs-file> [--type Namespace.Service]");
        Console.WriteLine(
            "  dotnet ioc-tools fields-path --project <csproj> --file <cs-file> --type Namespace.Service [--output <dir>]");
        Console.WriteLine("  dotnet ioc-tools services --project <csproj> [--output <dir>]");
        Console.WriteLine("  dotnet ioc-tools services-path --project <csproj> [--output <dir>]");
        Console.WriteLine("  dotnet ioc-tools explain --project <csproj> --type Namespace.Service");
        Console.WriteLine(
            "  dotnet ioc-tools graph --project <csproj> [--type Namespace.Service] [--format json|puml|mermaid] [--output <dir>]");
        Console.WriteLine(
            "  dotnet ioc-tools why --project <csproj> --type Namespace.Service --dependency Fully.Qualified.Type");
        Console.WriteLine("  dotnet ioc-tools doctor --project <csproj> [--fixable-only]");
        Console.WriteLine("  dotnet ioc-tools compare --project <csproj> --output <dir> [--baseline <dir>]");
        Console.WriteLine("  dotnet ioc-tools profile --project <csproj> [--type Namespace.Service]");
        Console.WriteLine("  dotnet ioc-tools config-audit --project <csproj> [--settings appsettings.json]");
        Console.WriteLine();
        Console.WriteLine("Common switches:");
        Console.WriteLine("  --configuration <Debug|Release>    Build configuration (default Debug)");
        Console.WriteLine("  --framework <tfm>                   Target framework override if multi-targeting");
    }

    public static int ExitWithUsage()
    {
        Write();
        return 0;
    }

    public static int ExitUnknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        Console.Error.WriteLine();
        Write();
        return 1;
    }

    public static int ExitWithError(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message)) Console.Error.WriteLine(message);
        Console.Error.WriteLine();
        Write();
        return 1;
    }
}
