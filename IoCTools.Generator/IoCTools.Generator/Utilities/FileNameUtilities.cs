namespace IoCTools.Generator.Utilities;

using System;
using System.Collections.Generic;
using System.Text;

internal static class FileNameUtilities
{
    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    // Combined invalid chars from Windows spec and existing code patterns
    private static readonly char[] InvalidChars =
    {
        '<', '>', ':', '"', '/', '\\', '|', '?', '*',
        '.', ',', ' ' // Existing replacements in ConstructorEmitter
    };

    public static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "_";

        // 1. Trim trailing periods and spaces
        var sanitized = input.TrimEnd('.', ' ');

        // 2. If empty after trim, replace with underscore
        if (string.IsNullOrEmpty(sanitized))
            return "_";

        // 3. Check for reserved Windows names
        if (ReservedWindowsNames.Contains(sanitized))
        {
            sanitized = "_" + sanitized;
        }

        // 4. Replace hardcoded Windows-invalid chars and control chars
        var builder = new StringBuilder(sanitized.Length);
        foreach (var c in sanitized)
        {
            if (c < 32 || IsInvalidChar(c))
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static bool IsInvalidChar(char c)
    {
        for (int i = 0; i < InvalidChars.Length; i++)
        {
            if (c == InvalidChars[i]) return true;
        }
        return false;
    }
}
