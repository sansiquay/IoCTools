namespace IoCTools.Generator.Shared;

using System;
using System.Collections.Immutable;

public readonly struct AutoDepResolvedEntry : IEquatable<AutoDepResolvedEntry>
{
    public AutoDepResolvedEntry(
        SymbolIdentity depType,
        ImmutableArray<AutoDepAttribution> sources)
    {
        DepType = depType;
        Sources = sources.IsDefault ? ImmutableArray<AutoDepAttribution>.Empty : sources;
    }

    public SymbolIdentity DepType { get; }
    public ImmutableArray<AutoDepAttribution> Sources { get; }

    public AutoDepAttribution PrimarySource
    {
        get
        {
            if (Sources.IsDefaultOrEmpty) return default;
            // Precedence order for display:
            // explicit > auto-profile > auto-universal > auto-transitive > auto-builtin
            foreach (var kind in new[] {
                AutoDepSourceKind.Explicit, AutoDepSourceKind.AutoProfile,
                AutoDepSourceKind.AutoUniversal, AutoDepSourceKind.AutoOpenUniversal,
                AutoDepSourceKind.AutoTransitive, AutoDepSourceKind.AutoBuiltinILogger })
            {
                foreach (var s in Sources) if (s.Kind == kind) return s;
            }
            return Sources[0];
        }
    }

    public bool Equals(AutoDepResolvedEntry other)
    {
        if (!DepType.Equals(other.DepType)) return false;
        if (Sources.Length != other.Sources.Length) return false;
        for (int i = 0; i < Sources.Length; i++)
            if (!Sources[i].Equals(other.Sources[i])) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is AutoDepResolvedEntry other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int h = DepType.GetHashCode();
            foreach (var s in Sources) h = (h * 31) ^ s.GetHashCode();
            return h;
        }
    }
}
