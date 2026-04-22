namespace IoCTools.Generator.Shared;

using System;
using System.Collections.Immutable;

public readonly struct AutoDepsResolverOutput : IEquatable<AutoDepsResolverOutput>
{
    public AutoDepsResolverOutput(ImmutableArray<AutoDepResolvedEntry> entries)
    {
        Entries = entries.IsDefault ? ImmutableArray<AutoDepResolvedEntry>.Empty : entries;
    }

    public ImmutableArray<AutoDepResolvedEntry> Entries { get; }

    public static AutoDepsResolverOutput Empty => new AutoDepsResolverOutput(ImmutableArray<AutoDepResolvedEntry>.Empty);

    public bool Equals(AutoDepsResolverOutput other)
    {
        if (Entries.Length != other.Entries.Length) return false;
        for (int i = 0; i < Entries.Length; i++)
            if (!Entries[i].Equals(other.Entries[i])) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is AutoDepsResolverOutput other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int h = 17;
            foreach (var e in Entries) h = (h * 31) ^ e.GetHashCode();
            return h;
        }
    }
}
