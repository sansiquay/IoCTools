namespace IoCTools.Tools.Cli.CommandLine;

internal static class CommandLineParser
{
    internal static ParseResult<FieldsCommandOptions> ParseFields(string[] args)
    {
        if (!TryCollectOptions(args, out var map, out var error))
            return ParseResult<FieldsCommandOptions>.Fail(error);

        if (!map.TryGetValue("project", out var projectValues))
            return ParseResult<FieldsCommandOptions>.Fail("--project is required.");
        if (!map.TryGetValue("file", out var fileValues))
            return ParseResult<FieldsCommandOptions>.Fail("--file is required.");

        var common = BuildCommon(projectValues[^1], map);
        var file = NormalizePath(fileValues[^1]);
        var filters = map.TryGetValue("type", out var filterValues)
            ? filterValues.Select(v => v.Trim()).Where(v => v.Length > 0)
                .Distinct(StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
        var outputSource = map.ContainsKey("source");
        var output = map.TryGetValue("output", out var outputValues) ? NormalizePath(outputValues[^1]) : null;

        return ParseResult<FieldsCommandOptions>.Ok(new FieldsCommandOptions(common, file, filters, outputSource, output));
    }

    internal static ParseResult<FieldsPathCommandOptions> ParseFieldsPath(string[] args)
    {
        if (!TryCollectOptions(args, out var map, out var error))
            return ParseResult<FieldsPathCommandOptions>.Fail(error);

        if (!map.TryGetValue("project", out var projectValues))
            return ParseResult<FieldsPathCommandOptions>.Fail("--project is required.");
        if (!map.TryGetValue("file", out var fileValues))
            return ParseResult<FieldsPathCommandOptions>.Fail("--file is required.");
        if (!map.TryGetValue("type", out var typeValues))
            return ParseResult<FieldsPathCommandOptions>.Fail("--type is required for fields-path.");

        var typeName = typeValues[^1].Trim();
        if (typeName.Length == 0)
            return ParseResult<FieldsPathCommandOptions>.Fail("--type must specify a class name.");

        var common = BuildCommon(projectValues[^1], map);
        var file = NormalizePath(fileValues[^1]);
        var output = map.TryGetValue("output", out var outputValues) ? NormalizePath(outputValues[^1]) : null;

        return ParseResult<FieldsPathCommandOptions>.Ok(new FieldsPathCommandOptions(common, file, typeName, output));
    }

    internal static ParseResult<ServicesCommandOptions> ParseServices(string[] args)
    {
        if (!TryCollectOptions(args, out var map, out var error))
            return ParseResult<ServicesCommandOptions>.Fail(error);

        if (!map.TryGetValue("project", out var projectValues))
            return ParseResult<ServicesCommandOptions>.Fail("--project is required.");

        var common = BuildCommon(projectValues[^1], map);
        var output = map.TryGetValue("output", out var outputValues) ? NormalizePath(outputValues[^1]) : null;
        var outputSource = map.ContainsKey("source");
        var typeFilter = map.TryGetValue("type", out var typeValues) ? typeValues[^1] : null;
        return ParseResult<ServicesCommandOptions>.Ok(new ServicesCommandOptions(common, output, outputSource, typeFilter));
    }

    internal static ParseResult<ExplainCommandOptions> ParseExplain(string[] args)
    {
        if (!TryCollectOptions(args, out var map, out var error))
            return ParseResult<ExplainCommandOptions>.Fail(error);

        if (!map.TryGetValue("project", out var projectValues))
            return ParseResult<ExplainCommandOptions>.Fail("--project is required.");
        if (!map.TryGetValue("type", out var typeValues))
            return ParseResult<ExplainCommandOptions>.Fail("--type is required.");

        var common = BuildCommon(projectValues[^1], map);
        var output = map.TryGetValue("output", out var outputValues) ? NormalizePath(outputValues[^1]) : null;
        return ParseResult<ExplainCommandOptions>.Ok(new ExplainCommandOptions(common, typeValues[^1], output));
    }

    internal static ParseResult<GraphCommandOptions> ParseGraph(string[] args)
    {
        if (!TryCollectOptions(args, out var map, out var error))
            return ParseResult<GraphCommandOptions>.Fail(error);

        if (!map.TryGetValue("project", out var projectValues))
            return ParseResult<GraphCommandOptions>.Fail("--project is required.");

        var common = BuildCommon(projectValues[^1], map);
        var typeName = map.TryGetValue("type", out var typeValues) ? typeValues[^1] : null;
        var format = map.TryGetValue("format", out var fmtValues) ? fmtValues[^1].ToLowerInvariant() : "puml";
        var output = map.TryGetValue("output", out var outputValues) ? NormalizePath(outputValues[^1]) : null;
        return ParseResult<GraphCommandOptions>.Ok(new GraphCommandOptions(common, typeName, format, output));
    }

    internal static ParseResult<WhyCommandOptions> ParseWhy(string[] args)
    {
        if (!TryCollectOptions(args, out var map, out var error))
            return ParseResult<WhyCommandOptions>.Fail(error);

        if (!map.TryGetValue("project", out var projectValues))
            return ParseResult<WhyCommandOptions>.Fail("--project is required.");
        if (!map.TryGetValue("type", out var typeValues))
            return ParseResult<WhyCommandOptions>.Fail("--type is required.");
        if (!map.TryGetValue("dependency", out var depValues))
            return ParseResult<WhyCommandOptions>.Fail("--dependency is required.");

        var common = BuildCommon(projectValues[^1], map);
        var output = map.TryGetValue("output", out var outputValues) ? NormalizePath(outputValues[^1]) : null;
        return ParseResult<WhyCommandOptions>.Ok(new WhyCommandOptions(common, typeValues[^1], depValues[^1], output));
    }

    internal static ParseResult<DoctorCommandOptions> ParseDoctor(string[] args)
    {
        if (!TryCollectOptions(args, out var map, out var error))
            return ParseResult<DoctorCommandOptions>.Fail(error);

        if (!map.TryGetValue("project", out var projectValues))
            return ParseResult<DoctorCommandOptions>.Fail("--project is required.");

        var common = BuildCommon(projectValues[^1], map);
        var fixableOnly = map.ContainsKey("fixable-only") || map.ContainsKey("--fixable-only");
        var output = map.TryGetValue("output", out var outputValues) ? NormalizePath(outputValues[^1]) : null;
        return ParseResult<DoctorCommandOptions>.Ok(new DoctorCommandOptions(common, fixableOnly, output));
    }

    internal static ParseResult<CompareCommandOptions> ParseCompare(string[] args)
    {
        if (!TryCollectOptions(args, out var map, out var error))
            return ParseResult<CompareCommandOptions>.Fail(error);

        if (!map.TryGetValue("project", out var projectValues))
            return ParseResult<CompareCommandOptions>.Fail("--project is required.");
        if (!map.TryGetValue("output", out var outputValues))
            return ParseResult<CompareCommandOptions>.Fail("--output <dir> is required for compare.");

        var baseline = map.TryGetValue("baseline", out var baseValues) ? NormalizePath(baseValues[^1]) : null;
        var common = BuildCommon(projectValues[^1], map);
        return ParseResult<CompareCommandOptions>.Ok(new CompareCommandOptions(common, NormalizePath(outputValues[^1]),
            baseline));
    }

    internal static ParseResult<ProfileCommandOptions> ParseProfile(string[] args)
    {
        if (!TryCollectOptions(args, out var map, out var error))
            return ParseResult<ProfileCommandOptions>.Fail(error);

        if (!map.TryGetValue("project", out var projectValues))
            return ParseResult<ProfileCommandOptions>.Fail("--project is required.");

        var common = BuildCommon(projectValues[^1], map);
        var typeName = map.TryGetValue("type", out var typeValues) ? typeValues[^1] : null;
        return ParseResult<ProfileCommandOptions>.Ok(new ProfileCommandOptions(common, typeName));
    }

    internal static ParseResult<ConfigAuditCommandOptions> ParseConfigAudit(string[] args)
    {
        if (!TryCollectOptions(args, out var map, out var error))
            return ParseResult<ConfigAuditCommandOptions>.Fail(error);

        if (!map.TryGetValue("project", out var projectValues))
            return ParseResult<ConfigAuditCommandOptions>.Fail("--project is required.");

        var settings = map.TryGetValue("settings", out var settingsValues) ? NormalizePath(settingsValues[^1]) : null;
        var common = BuildCommon(projectValues[^1], map);
        return ParseResult<ConfigAuditCommandOptions>.Ok(new ConfigAuditCommandOptions(common, settings));
    }

    private static bool TryCollectOptions(string[] args,
        out Dictionary<string, List<string>> map,
        out string? error)
    {
        error = null;
        map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!TryParseToken(token, out var key, out var value, out error))
                return false;

            key = NormalizeKey(key);
            if (!IsValidKey(key, out error))
                return false;

            if (!TryCollectValue(ref i, args, key, value, map, out error))
                return false;
        }

        return true;
    }

    private static bool TryParseToken(string token, out string key, out string? value, out string? error)
    {
        value = null;
        error = null;

        if (!token.StartsWith('-'))
        {
            key = token;
            error = $"Unexpected argument '{token}'.";
            return false;
        }

        var separatorIndex = token.IndexOf('=');
        if (separatorIndex >= 0)
        {
            value = token[(separatorIndex + 1)..];
            key = token[..separatorIndex];
        }
        else
        {
            key = token;
        }

        return true;
    }

    private static string NormalizeKey(string key)
    {
        return key switch
        {
            "--project" or "-p" => "project",
            "--configuration" or "-c" => "configuration",
            "--framework" or "-f" => "framework",
            "--file" => "file",
            "--type" or "-t" or "--class" => "type",
            "--output" or "-o" => "output",
            "--dependency" or "-d" => "dependency",
            "--format" => "format",
            "--baseline" => "baseline",
            "--settings" => "settings",
            "--fixable-only" => "fixable-only",
            "--source" or "-s" => "source",
            "--json" => "json",
            "--verbose" or "-v" => "verbose",
            _ => key
        };
    }

    private static bool IsValidKey(string key, out string? error)
    {
        error = null;
        if (key.StartsWith("-", StringComparison.Ordinal))
        {
            error = $"Unknown option '{key}'.";
            return false;
        }
        return true;
    }

    private static bool IsFlag(string key) => key is "fixable-only" or "source" or "json" or "verbose";

    private static bool TryCollectValue(
        ref int index,
        string[] args,
        string key,
        string? value,
        Dictionary<string, List<string>> map,
        out string? error)
    {
        error = null;
        var isFlag = IsFlag(key);

        if (value == null && !isFlag)
        {
            if (index + 1 >= args.Length)
            {
                error = $"Missing value for '{key}'.";
                return false;
            }

            value = args[++index];
        }

        if (isFlag && value == null)
            value = "true";

        if (!map.TryGetValue(key, out var list))
        {
            list = new List<string>();
            map[key] = list;
        }

        list.Add(value ?? string.Empty);
        return true;
    }

    private static CommonOptions BuildCommon(string projectPath,
        Dictionary<string, List<string>> map)
    {
        var configuration = map.TryGetValue("configuration", out var configValues)
            ? configValues[^1]
            : "Debug";
        var framework = map.TryGetValue("framework", out var frameworkValues)
            ? frameworkValues[^1]
            : null;
        var json = map.ContainsKey("json");
        var verbose = map.ContainsKey("verbose");

        return new CommonOptions(NormalizePath(projectPath), configuration, framework, json, verbose);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.");

        return Path.GetFullPath(path);
    }
}

internal sealed record ParseResult<T>(bool Success, T? Value, string? Error)
{
    public static ParseResult<T> Ok(T value) => new(true, value, null);
    public static ParseResult<T> Fail(string? error) => new(false, default, error ?? "Invalid arguments.");
}

internal sealed record CommonOptions(string ProjectPath, string Configuration, string? Framework, bool Json, bool Verbose);

internal sealed record FieldsCommandOptions(CommonOptions Common, string FilePath, IReadOnlyList<string> TypeFilters, bool OutputSource, string? OutputDirectory);

internal sealed record FieldsPathCommandOptions(
    CommonOptions Common,
    string FilePath,
    string TypeName,
    string? OutputDirectory);

internal sealed record ServicesCommandOptions(CommonOptions Common, string? OutputDirectory, bool OutputSource, string? TypeFilter);

internal sealed record ExplainCommandOptions(CommonOptions Common, string TypeName, string? OutputDirectory);

internal sealed record GraphCommandOptions(
    CommonOptions Common,
    string? TypeName,
    string Format,
    string? OutputDirectory);

internal sealed record WhyCommandOptions(
    CommonOptions Common,
    string TypeName,
    string Dependency,
    string? OutputDirectory);

internal sealed record DoctorCommandOptions(CommonOptions Common, bool FixableOnly, string? OutputDirectory);

internal sealed record CompareCommandOptions(CommonOptions Common, string OutputDirectory, string? BaselineDirectory);

internal sealed record ProfileCommandOptions(CommonOptions Common, string? TypeName);

internal sealed record ConfigAuditCommandOptions(CommonOptions Common, string? SettingsPath);
