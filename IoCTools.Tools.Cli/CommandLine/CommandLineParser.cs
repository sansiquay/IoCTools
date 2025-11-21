namespace IoCTools.Tools.Cli.CommandLine;

using System.Collections.Generic;
using System.Linq;

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

        return ParseResult<FieldsCommandOptions>.Ok(new FieldsCommandOptions(common, file, filters));
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
        return ParseResult<ServicesCommandOptions>.Ok(new ServicesCommandOptions(common, output));
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
            if (!token.StartsWith('-'))
            {
                error = $"Unexpected argument '{token}'.";
                return false;
            }

            string? value = null;
            var separatorIndex = token.IndexOf('=');
            if (separatorIndex >= 0)
            {
                value = token[(separatorIndex + 1)..];
                token = token[..separatorIndex];
            }

            var key = token switch
            {
                "--project" or "-p" => "project",
                "--configuration" or "-c" => "configuration",
                "--framework" or "-f" => "framework",
                "--file" => "file",
                "--type" or "-t" or "--class" => "type",
                "--output" or "-o" => "output",
                _ => token
            };

            if (key.StartsWith("-", StringComparison.Ordinal))
            {
                error = $"Unknown option '{token}'.";
                return false;
            }

            if (value == null)
            {
                if (i + 1 >= args.Length)
                {
                    error = $"Missing value for '{token}'.";
                    return false;
                }

                value = args[++i];
            }

            if (!map.TryGetValue(key, out var list))
            {
                list = new List<string>();
                map[key] = list;
            }

            list.Add(value);
        }

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

        return new CommonOptions(NormalizePath(projectPath), configuration, framework);
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

internal sealed record CommonOptions(string ProjectPath, string Configuration, string? Framework);

internal sealed record FieldsCommandOptions(CommonOptions Common, string FilePath, IReadOnlyList<string> TypeFilters);

internal sealed record FieldsPathCommandOptions(CommonOptions Common, string FilePath, string TypeName, string? OutputDirectory);

internal sealed record ServicesCommandOptions(CommonOptions Common, string? OutputDirectory);
