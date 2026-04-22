namespace IoCTools.Tools.Cli.CommandLine;

using System;
using System.Collections.Generic;

/// <summary>
/// Cross-command parser helper for the <c>--hide-auto-deps</c> and <c>--only-auto-deps</c>
/// flags. Recognized on <c>graph</c>, <c>why</c>, <c>explain</c>, and <c>evidence</c>.
/// Passing both is mutually exclusive and produces a parser error.
/// </summary>
internal sealed class CommonAutoDepsOptions
{
    private CommonAutoDepsOptions(bool hideAutoDeps,
        bool onlyAutoDeps)
    {
        HideAutoDeps = hideAutoDeps;
        OnlyAutoDeps = onlyAutoDeps;
    }

    public bool HideAutoDeps { get; }

    public bool OnlyAutoDeps { get; }

    public static CommonAutoDepsOptions Empty { get; } = new(false, false);

    /// <summary>
    /// Scans <paramref name="args"/> for <c>--hide-auto-deps</c> and <c>--only-auto-deps</c>
    /// flags, returns the leftover tokens via <paramref name="remaining"/>, and sets
    /// <paramref name="error"/> when both flags are passed (mutually exclusive).
    /// </summary>
    public static CommonAutoDepsOptions TryExtract(IReadOnlyList<string> args,
        out IReadOnlyList<string> remaining,
        out string? error)
    {
        error = null;
        var hide = false;
        var only = false;
        var kept = new List<string>(args.Count);

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--hide-auto-deps", StringComparison.Ordinal))
                hide = true;
            else if (string.Equals(arg, "--only-auto-deps", StringComparison.Ordinal))
                only = true;
            else
                kept.Add(arg);
        }

        remaining = kept;

        if (hide && only)
        {
            error = "--hide-auto-deps and --only-auto-deps are mutually exclusive.";
            return Empty;
        }

        if (!hide && !only)
            return Empty;

        return new CommonAutoDepsOptions(hide, only);
    }
}
