namespace IoCTools.Generator.Shared;

using System;

/// <summary>
/// Kinds of resolver-internal diagnostic signals. The resolver cannot report diagnostics directly
/// (Location is not value-equatable on netstandard2.0), so it emits these signals alongside the
/// resolved entries and the generator's diagnostics pipeline translates them into
/// <see cref="Microsoft.CodeAnalysis.Diagnostic"/> instances with proper locations.
/// </summary>
public enum AutoDepDiagnosticKind
{
    None = 0,

    /// <summary>IOC096: a NoAutoDep/NoAutoDepOpen target is not in the pre-opt-out set.</summary>
    StaleOptOut = 96,

    /// <summary>IOC098: a bare [DependsOn&lt;T&gt;] slot overlaps with an active auto-dep.</summary>
    DependsOnOverlap = 98,

    /// <summary>IOC102: AutoDepOpen closure violates a type-parameter constraint.</summary>
    OpenGenericConstraint = 102,

    /// <summary>IOC105: a service is attached to a profile via multiple paths.</summary>
    RedundantProfile = 105
}

/// <summary>
/// Value-equatable signal emitted by the resolver to describe a diagnostic condition it detected
/// during resolution of a single service. Deliberately string-typed -- Location/ITypeSymbol are
/// resolved by the validator using the live compilation, since neither participates in
/// value-equality for the incremental pipeline.
/// </summary>
public readonly struct AutoDepDiagnosticSignal : IEquatable<AutoDepDiagnosticSignal>
{
    public AutoDepDiagnosticSignal(
        AutoDepDiagnosticKind kind,
        string arg0,
        string? arg1 = null,
        string? arg2 = null,
        string? arg3 = null)
    {
        Kind = kind;
        Arg0 = arg0;
        Arg1 = arg1;
        Arg2 = arg2;
        Arg3 = arg3;
    }

    public AutoDepDiagnosticKind Kind { get; }
    public string Arg0 { get; }
    public string? Arg1 { get; }
    public string? Arg2 { get; }
    public string? Arg3 { get; }

    public bool Equals(AutoDepDiagnosticSignal other) =>
        Kind == other.Kind &&
        string.Equals(Arg0, other.Arg0, StringComparison.Ordinal) &&
        string.Equals(Arg1, other.Arg1, StringComparison.Ordinal) &&
        string.Equals(Arg2, other.Arg2, StringComparison.Ordinal) &&
        string.Equals(Arg3, other.Arg3, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AutoDepDiagnosticSignal other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int h = (int)Kind * 397;
            h = (h * 31) ^ (Arg0?.GetHashCode() ?? 0);
            h = (h * 31) ^ (Arg1?.GetHashCode() ?? 0);
            h = (h * 31) ^ (Arg2?.GetHashCode() ?? 0);
            h = (h * 31) ^ (Arg3?.GetHashCode() ?? 0);
            return h;
        }
    }

    public static bool operator ==(AutoDepDiagnosticSignal a, AutoDepDiagnosticSignal b) => a.Equals(b);
    public static bool operator !=(AutoDepDiagnosticSignal a, AutoDepDiagnosticSignal b) => !a.Equals(b);
}
