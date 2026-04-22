namespace IoCTools.Generator.Shared;

using System;

public enum AutoDepSourceKind
{
    Explicit = 0,
    AutoUniversal = 1,
    AutoOpenUniversal = 2,
    AutoProfile = 3,
    AutoTransitive = 4,
    AutoBuiltinILogger = 5
}

public readonly struct AutoDepAttribution : IEquatable<AutoDepAttribution>
{
    public AutoDepAttribution(AutoDepSourceKind kind, string? sourceName, string? assemblyName)
    {
        Kind = kind;
        SourceName = sourceName;
        AssemblyName = assemblyName;
    }

    public AutoDepSourceKind Kind { get; }
    public string? SourceName { get; }
    public string? AssemblyName { get; }

    public bool Equals(AutoDepAttribution other) =>
        Kind == other.Kind &&
        string.Equals(SourceName, other.SourceName, StringComparison.Ordinal) &&
        string.Equals(AssemblyName, other.AssemblyName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AutoDepAttribution other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int h = (int)Kind * 397;
            h = (h * 31) ^ (SourceName?.GetHashCode() ?? 0);
            h = (h * 31) ^ (AssemblyName?.GetHashCode() ?? 0);
            return h;
        }
    }

    public static bool operator ==(AutoDepAttribution a, AutoDepAttribution b) => a.Equals(b);
    public static bool operator !=(AutoDepAttribution a, AutoDepAttribution b) => !a.Equals(b);

    public string ToTag() => Kind switch
    {
        AutoDepSourceKind.Explicit => "explicit",
        AutoDepSourceKind.AutoUniversal => "auto-universal",
        AutoDepSourceKind.AutoOpenUniversal => "auto-universal",
        AutoDepSourceKind.AutoProfile => $"auto-profile:{SourceName}",
        AutoDepSourceKind.AutoTransitive => $"auto-transitive:{AssemblyName}",
        AutoDepSourceKind.AutoBuiltinILogger => "auto-builtin:ILogger",
        _ => "unknown"
    };
}
