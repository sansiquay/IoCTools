namespace IoCTools.Tools.Cli;

using IoCTools.Tools.Cli.CommandLine;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        if (args.Length == 0 || IsHelp(args[0]))
        {
            UsagePrinter.Write();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var remaining = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "fields" => await RunFieldsAsync(remaining, cts.Token),
                "fields-path" => await RunFieldsPathAsync(remaining, cts.Token),
                "services" => await RunServicesAsync(remaining, cts.Token),
                "services-path" => await RunServicesPathAsync(remaining, cts.Token),
                "help" => UsagePrinter.ExitWithUsage(),
                _ => UsagePrinter.ExitUnknown(command)
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Command cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled error: {ex.Message}");
#if DEBUG
            Console.Error.WriteLine(ex);
#endif
            return 1;
        }
    }

    private static bool IsHelp(string value) =>
        string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "help", StringComparison.OrdinalIgnoreCase);

    private static async Task<int> RunFieldsAsync(string[] args, CancellationToken token)
    {
        var parse = CommandLineParser.ParseFields(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        var inspector = new ServiceFieldInspector(context.Project);
        var reports = await inspector.GetFieldReportsAsync(options.FilePath, options.TypeFilters, token);

        if (reports.Count == 0)
        {
            Console.WriteLine("No IoCTools-enabled services found in file.");
            return 0;
        }

        foreach (var report in reports)
        {
            Console.WriteLine($"Service: {report.TypeName}");
            Console.WriteLine($"  Declared In: {report.FilePath}");

            if (report.DependencyFields.Count > 0)
            {
                Console.WriteLine("  Generated Dependencies:");
                foreach (var field in report.DependencyFields)
                    Console.WriteLine($"    - {field.TypeName} => {field.FieldName}{(field.IsExternal ? " (external)" : string.Empty)}");
            }
            else
            {
                Console.WriteLine("  Generated Dependencies: (none)");
            }

            if (report.ConfigurationFields.Count > 0)
            {
                Console.WriteLine("  Generated Config Fields:");
                foreach (var field in report.ConfigurationFields)
                {
                    var requirement = field.Required == true ? "required" : "optional";
                    var reload = field.SupportsReloading == true ? ", reload" : string.Empty;
                    var configKey = string.IsNullOrWhiteSpace(field.ConfigurationKey)
                        ? "inferred section"
                        : field.ConfigurationKey;
                    Console.WriteLine($"    - {field.TypeName} => {field.FieldName} (key: {configKey}, {requirement}{reload})");
                }
            }
            else
            {
                Console.WriteLine("  Generated Config Fields: (none)");
            }

            Console.WriteLine();
        }

        return 0;
    }

    private static async Task<int> RunFieldsPathAsync(string[] args, CancellationToken token)
    {
        var parse = CommandLineParser.ParseFieldsPath(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        var inspector = new ServiceFieldInspector(context.Project);
        var symbol = await inspector.FindServiceSymbolAsync(options.FilePath, options.TypeName, token);
        if (symbol == null)
            return UsagePrinter.ExitWithError(
                $"Type '{options.TypeName}' was not found in '{options.FilePath}' or it is not an IoCTools service.");

        var artifacts = await GeneratorArtifactWriter.CreateAsync(context, options.OutputDirectory, token);
        var hintName = HintNameBuilder.GetConstructorHint(symbol);
        if (!artifacts.TryGetFile(hintName, out var path))
        {
            Console.WriteLine("Generator skipped constructor output for this type.");
            return 0;
        }

        Console.WriteLine(path);
        return 0;
    }

    private static async Task<int> RunServicesAsync(string[] args, CancellationToken token)
    {
        var parse = CommandLineParser.ParseServices(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        var artifacts = await GeneratorArtifactWriter.CreateAsync(context, options.OutputDirectory, token);
        var hintName = HintNameBuilder.GetExtensionHint(context.Project);
        if (!artifacts.TryGetFile(hintName, out var path))
        {
            Console.WriteLine("No generated registration extension was produced for this project.");
            return 0;
        }

        var summary = RegistrationSummaryBuilder.Build(path!);
        RegistrationPrinter.Write(summary);
        return 0;
    }

    private static async Task<int> RunServicesPathAsync(string[] args, CancellationToken token)
    {
        var parse = CommandLineParser.ParseServices(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        var artifacts = await GeneratorArtifactWriter.CreateAsync(context, options.OutputDirectory, token);
        var hintName = HintNameBuilder.GetExtensionHint(context.Project);
        if (!artifacts.TryGetFile(hintName, out var path))
        {
            Console.WriteLine("No generated registration extension was produced for this project.");
            return 0;
        }

        Console.WriteLine(path);
        return 0;
    }
}
