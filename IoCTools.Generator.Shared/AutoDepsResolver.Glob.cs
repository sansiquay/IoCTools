namespace IoCTools.Generator.Shared;

using System.Text;
using System.Text.RegularExpressions;

public static partial class AutoDepsResolver
{
    /// <summary>
    /// Matches <paramref name="input"/> against a glob <paramref name="pattern"/> used by
    /// <c>AutoDepsApplyGlob&lt;TProfile&gt;("pattern")</c>.
    /// Grammar: <c>*</c> matches any sequence (including empty), <c>?</c> matches a single character,
    /// <c>.</c> matches a literal dot, and regex character classes (<c>[abc]</c>, <c>[a-z]</c>) are
    /// supported by pass-through. The pattern is anchored (full-string match).
    /// </summary>
    /// <remarks>
    /// Deviation from the plan snippet: this implementation intentionally passes a small set of
    /// regex metacharacters through (brackets, braces, parens, backslash, <c>^</c>, <c>$</c>, <c>|</c>, <c>+</c>)
    /// rather than escaping them. This keeps character classes functional and — importantly — allows
    /// malformed patterns (e.g. <c>[unterminated</c>) to throw during Regex construction so IOC103
    /// ("unterminated character classes") can fire. The <c>.</c> character remains regex-escaped so
    /// it matches a literal dot, which is the common case for namespace glob patterns.
    /// </remarks>
    internal static bool GlobMatch(string input, string pattern, out bool patternIsInvalid)
    {
        patternIsInvalid = false;
        if (string.IsNullOrEmpty(pattern))
        {
            patternIsInvalid = true;
            return false;
        }

        try
        {
            var regex = GlobToRegex(pattern);
            return regex.IsMatch(input ?? string.Empty);
        }
        catch
        {
            patternIsInvalid = true;
            return false;
        }
    }

    private static Regex GlobToRegex(string pattern)
    {
        var sb = new StringBuilder();
        sb.Append('^');
        foreach (var c in pattern)
        {
            switch (c)
            {
                case '*':
                    sb.Append(".*");
                    break;
                case '?':
                    sb.Append('.');
                    break;
                case '[':
                case ']':
                case '\\':
                case '{':
                case '}':
                case '(':
                case ')':
                case '+':
                case '|':
                case '^':
                case '$':
                    // Pass regex metachars through so character classes work and malformed
                    // patterns (e.g. unterminated '[') throw during Regex construction.
                    sb.Append(c);
                    break;
                default:
                    // Everything else — including '.' — is escaped so it matches literally.
                    sb.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }
}
