namespace IoCTools.Tools.Cli;

using System.Diagnostics;

using CommandLine;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_,
            eventArgs) =>
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
                "explain" => await RunExplainAsync(remaining, cts.Token),
                "graph" => await RunGraphAsync(remaining, cts.Token),
                "why" => await RunWhyAsync(remaining, cts.Token),
                "doctor" => await RunDoctorAsync(remaining, cts.Token),
                "compare" => await RunCompareAsync(remaining, cts.Token),
                "profile" => await RunProfileAsync(remaining, cts.Token),
                "profiles" => await RunProfilesAsync(remaining, cts.Token),
                "config-audit" => await RunConfigAuditAsync(remaining, cts.Token),
                "evidence" => await RunEvidenceAsync(remaining, cts.Token),
                "suppress" => await RunSuppressAsync(remaining, cts.Token),
                "validators" => await RunValidatorsAsync(remaining, cts.Token),
                "validator-graph" => await RunValidatorGraphAsync(remaining, cts.Token),
                "migrate-inject" => await RunMigrateInjectAsync(remaining, cts.Token),
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

    private static async Task<int> RunFieldsAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseFields(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");
        var inspector = new ServiceFieldInspector(context.Project);
        var reports = await inspector.GetFieldReportsAsync(options.FilePath, options.TypeFilters, token);

        if (reports.Count == 0)
        {
            Console.WriteLine("No IoCTools-enabled services found in file.");
            if (options.TypeFilters.Count > 0)
            {
                // Get all service types in the file for fuzzy suggestions
                var allReports = await inspector.GetFieldReportsAsync(options.FilePath, Array.Empty<string>(), token);
                var allTypes = allReports.Select(r => r.TypeName);
                foreach (var filter in options.TypeFilters)
                    FuzzySuggestionUtility.PrintSuggestions(output, filter, allTypes);
            }
            return 0;
        }

        if (options.OutputSource)
        {
            // Output the generated constructor source code for matching types
            var artifacts = await GeneratorArtifactWriter.CreateAsync(context, options.OutputDirectory, token);
            var foundAny = false;

            foreach (var report in reports)
            {
                var symbol = await inspector.FindServiceSymbolAsync(options.FilePath, report.TypeName, token);
                if (symbol == null) continue;

                var hintName = HintNameBuilder.GetConstructorHint(symbol);
                if (artifacts.TryGetFile(hintName, out var path))
                {
                    if (foundAny) Console.WriteLine();
                    Console.WriteLine($"// {report.TypeName}");
                    var source = await File.ReadAllTextAsync(path!, token);
                    Console.WriteLine(source);
                    foundAny = true;
                }
            }

            if (!foundAny)
            {
                Console.WriteLine("// No generated constructor source found for the specified types.");
            }

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
                    Console.WriteLine(
                        $"    - {field.TypeName} => {field.FieldName}{(field.IsExternal ? " (external)" : string.Empty)}");
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
                    Console.WriteLine(
                        $"    - {field.TypeName} => {field.FieldName} (key: {configKey}, {requirement}{reload})");
                }
            }
            else
            {
                Console.WriteLine("  Generated Config Fields: (none)");
            }

            Console.WriteLine();
        }

        output.ReportTiming("Command completed");
        return 0;
    }

    private static async Task<int> RunFieldsPathAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseFieldsPath(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");
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
        output.ReportTiming("Command completed");
        return 0;
    }

    private static async Task<int> RunServicesAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseServices(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");
        var artifacts = await GeneratorArtifactWriter.CreateAsync(context, options.OutputDirectory, token);
        var hintName = HintNameBuilder.GetExtensionHint(context.Project);
        if (!artifacts.TryGetFile(hintName, out var path))
        {
            Console.WriteLine("No generated registration extension was produced for this project.");
            return 0;
        }

        if (options.OutputSource)
        {
            if (options.TypeFilter != null)
            {
                var summary = RegistrationSummaryBuilder.Build(path!);
                var filtered = RegistrationSummaryBuilder.FilterByType(summary, options.TypeFilter);
                if (filtered.Records.Count > 0)
                {
                    foreach (var record in filtered.Records)
                        Console.WriteLine(record.RawExpression);
                }
                else
                {
                    Console.WriteLine($"// No registrations found for type '{options.TypeFilter}'");
                    // Suggest close matches
                    var allTypes = summary.Records
                        .Select(r => r.ServiceType)
                        .Concat(summary.Records.Select(r => r.ImplementationType))
                        .Where(t => t != null)
                        .Distinct()!
                        .OfType<string>();
                    FuzzySuggestionUtility.PrintSuggestions(output, options.TypeFilter, allTypes);
                }
            }
            else
            {
                var source = await File.ReadAllTextAsync(path!, token);
                Console.WriteLine(source);
            }
        }
        else
        {
            var summary = RegistrationSummaryBuilder.Build(path!);
            RegistrationPrinter.Write(summary, output);
        }
        output.ReportTiming("Command completed");
        return 0;
    }

    private static async Task<int> RunServicesPathAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseServices(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");
        var artifacts = await GeneratorArtifactWriter.CreateAsync(context, options.OutputDirectory, token);
        var hintName = HintNameBuilder.GetExtensionHint(context.Project);
        if (!artifacts.TryGetFile(hintName, out var path))
        {
            Console.WriteLine("No generated registration extension was produced for this project.");
            return 0;
        }

        Console.WriteLine(path);
        output.ReportTiming("Command completed");
        return 0;
    }

    private static async Task<int> RunExplainAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseExplain(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");
        var inspector = new ServiceFieldInspector(context.Project);
        var reports = await inspector.GetFieldReportsAsync(null, new[] { options.TypeName }, token);
        var target = reports.FirstOrDefault(r => string.Equals(r.TypeName, options.TypeName, StringComparison.Ordinal));
        if (target == null)
        {
            Console.Error.WriteLine($"Type '{options.TypeName}' not found or not IoCTools-enabled.");
            var allTypes = reports.Select(r => r.TypeName);
            FuzzySuggestionUtility.PrintSuggestions(output, options.TypeName, allTypes);
            return 1;
        }

        ExplainPrinter.Write(target, output, options.AutoDepsFlags);
        output.ReportTiming("Command completed");
        return 0;
    }

    private static async Task<int> RunGraphAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseGraph(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");
        var artifacts = await GeneratorArtifactWriter.CreateAsync(context, options.OutputDirectory, token);
        var hint = HintNameBuilder.GetExtensionHint(context.Project);
        if (!artifacts.TryGetFile(hint, out var path))
        {
            Console.WriteLine("No generated registration extension was produced for this project.");
            return 0;
        }

        var summary = RegistrationSummaryBuilder.Build(path!);
        var autoDepRows = await BuildAutoDepGraphRowsAsync(context, options.TypeName, token);
        GraphPrinter.Write(summary, options.Format, options.TypeName, output, autoDepRows, options.AutoDepsFlags);
        output.ReportTiming("Command completed");
        return 0;
    }

    private static async Task<IReadOnlyList<AutoDepGraphRow>> BuildAutoDepGraphRowsAsync(ProjectContext context,
        string? typeFilter,
        CancellationToken token)
    {
        var inspector = new ServiceFieldInspector(context.Project);
        var reports = await inspector.GetFieldReportsAsync(null, Array.Empty<string>(), token);
        var rows = new List<AutoDepGraphRow>();
        foreach (var report in reports)
        {
            if (!string.IsNullOrWhiteSpace(typeFilter) &&
                !TypeFilterUtility.Matches(report.TypeName, typeFilter!))
                continue;
            foreach (var dep in report.DependencyFields)
            {
                if (dep.Attribution is not { } attribution) continue;
                if (attribution.Kind == Generator.Shared.AutoDepSourceKind.Explicit) continue;
                rows.Add(new AutoDepGraphRow(report.TypeName, dep.TypeName, attribution));
            }
        }

        return rows;
    }

    private static async Task<int> RunWhyAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseWhy(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");
        var inspector = new ServiceFieldInspector(context.Project);
        var reports = await inspector.GetFieldReportsAsync(null, new[] { options.TypeName }, token);
        var target = reports.FirstOrDefault(r => string.Equals(r.TypeName, options.TypeName, StringComparison.Ordinal));
        if (target == null)
            return UsagePrinter.ExitWithError($"Type '{options.TypeName}' not found or not IoCTools-enabled.");

        WhyPrinter.Write(target, options.Dependency, output, options.AutoDepsFlags);
        output.ReportTiming("Command completed");
        return 0;
    }

    private static async Task<int> RunDoctorAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseDoctor(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");

        var diagnostics = await DiagnosticRunner.RunAsync(context, token);
        var preflight = await DoctorPreflight.RunAsync(context, token);
        DoctorPrinter.Write(diagnostics, options.FixableOnly, output, preflight);
        output.ReportTiming("Command completed");
        return diagnostics.Any(d => d.Severity == "Error") ? 1 : 0;
    }

    private static async Task<int> RunCompareAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseCompare(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");
        var artifacts = await GeneratorArtifactWriter.CreateAsync(context, options.OutputDirectory, token);
        CompareRunner.WriteSnapshot(artifacts, options.OutputDirectory);
        if (options.BaselineDirectory != null)
            CompareRunner.Compare(options.BaselineDirectory, options.OutputDirectory);
        output.ReportTiming("Command completed");
        return 0;
    }

    private static async Task<int> RunProfileAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseProfile(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        var sw = Stopwatch.StartNew();
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        sw.Stop();

        // Get service counts from the generated registration extension
        var artifacts = await GeneratorArtifactWriter.CreateAsync(context, null, token);
        var hint = HintNameBuilder.GetExtensionHint(context.Project);
        int serviceCount = 0;
        int configurationCount = 0;
        if (artifacts.TryGetFile(hint, out var path))
        {
            var summary = RegistrationSummaryBuilder.Build(path!);
            serviceCount = summary.Records.Count(r => r.Kind == RegistrationKind.Service);
            configurationCount = summary.Records.Count(r => r.Kind == RegistrationKind.Configuration);
        }

        ProfilePrinter.Write(sw.Elapsed, context.Project.FilePath ?? "<unknown>", options.TypeName, serviceCount, configurationCount, output);
        output.ReportTiming("Command completed");
        return 0;
    }

    private static async Task<int> RunProfilesAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseProfiles(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        // The plural `profiles` subcommand obeys --json via --format json for its dedicated
        // output shape; the common --json flag routes through OutputContext as normal but is
        // not the documented channel for this command.
        var useJson = string.Equals(options.Format, "json", System.StringComparison.OrdinalIgnoreCase) ||
                      options.Common.Json;
        var output = OutputContext.Create(useJson, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");

        // Strip source-generator output so we see only user-authored declarations. Without this
        // step generator-emitted attributes could confuse the profile/attachment scans.
        var stripped = StripGeneratedTreesForProfiles(context.Compilation);
        var result = ProfilesPrinter.Print(stripped, options, output);
        output.ReportTiming("profiles command completed");
        return result;
    }

    private static Microsoft.CodeAnalysis.CSharp.CSharpCompilation StripGeneratedTreesForProfiles(
        Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation)
    {
        // Mirrors AutoDepsAttributionResolver.StripGeneratedTrees (private there).
        var generatedTrees = compilation.SyntaxTrees
            .Where(t => !string.IsNullOrEmpty(t.FilePath) &&
                        (t.FilePath.EndsWith(".g.cs", System.StringComparison.OrdinalIgnoreCase) ||
                         t.FilePath.Contains("/generated/", System.StringComparison.OrdinalIgnoreCase) ||
                         t.FilePath.Contains("\\generated\\", System.StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (generatedTrees.Length == 0) return compilation;
        return (Microsoft.CodeAnalysis.CSharp.CSharpCompilation)compilation.RemoveSyntaxTrees(generatedTrees);
    }

    private static async Task<int> RunConfigAuditAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseConfigAudit(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");
        var inspector = new ServiceFieldInspector(context.Project);
        var reports = await inspector.GetFieldReportsAsync(null, Array.Empty<string>(), token);
        ConfigAuditPrinter.Write(reports, options.SettingsPath, output);
        output.ReportTiming("Command completed");
        return 0;
    }

    private static async Task<int> RunEvidenceAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseEvidence(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");

        var bundle = await EvidencePrinter.BuildAsync(context, options, token);
        if (options.TypeName != null && bundle.typeEvidence == null)
            return UsagePrinter.ExitWithError($"Type '{options.TypeName}' not found or not IoCTools-enabled.");

        EvidencePrinter.Write(bundle, output);
        output.ReportTiming("evidence command completed");
        return 0;
    }

    private static async Task<int> RunSuppressAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseSuppress(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");

        IReadOnlyList<string>? liveDiagnosticIds = null;

        if (options.Live)
        {
            output.Verbose("Running generator for --live diagnostic detection...");
            await using var context = await ProjectContext.CreateAsync(options.Common, token);
            output.Verbose($"Project loaded: {context.Project.FilePath}");

            // Use DiagnosticRunner to get actual firing diagnostics
            var diagnostics = await DiagnosticRunner.RunAsync(context, token);
            liveDiagnosticIds = diagnostics
                .Where(d => d.Id.StartsWith("IOC", StringComparison.OrdinalIgnoreCase))
                .Select(d => d.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            output.Verbose($"Found {liveDiagnosticIds.Count} live IoCTools diagnostics");
        }

        var result = SuppressPrinter.Write(options, output, liveDiagnosticIds);
        output.ReportTiming("suppress command completed");
        return result;
    }

    private static async Task<int> RunValidatorsAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseValidators(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");

        var validators = ValidatorInspector.DiscoverValidators(context.Compilation);
        ValidatorPrinter.WriteList(validators, options.Filter, output);
        output.ReportTiming("validators command completed");
        return 0;
    }

    private static async Task<int> RunMigrateInjectAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseMigrateInject(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        return await MigrateInjectRunner.RunAsync(parse.Value!, token);
    }

    private static async Task<int> RunValidatorGraphAsync(string[] args,
        CancellationToken token)
    {
        var parse = CommandLineParser.ParseValidatorGraph(args);
        if (!parse.Success)
            return UsagePrinter.ExitWithError(parse.Error);

        var options = parse.Value!;
        var output = OutputContext.Create(options.Common.Json, options.Common.Verbose);
        output.Verbose($"Project: {options.Common.ProjectPath}");
        await using var context = await ProjectContext.CreateAsync(options.Common, token);
        output.Verbose($"Project loaded: {context.Project.FilePath}");

        var validators = ValidatorInspector.DiscoverValidators(context.Compilation);

        if (options.WhyValidator != null)
        {
            ValidatorPrinter.WriteWhy(options.WhyValidator, validators, output);
        }
        else
        {
            ValidatorPrinter.WriteGraph(validators, output);
        }

        output.ReportTiming("validator-graph command completed");
        return 0;
    }
}
