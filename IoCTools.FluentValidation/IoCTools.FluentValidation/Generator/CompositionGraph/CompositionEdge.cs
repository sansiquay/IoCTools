namespace IoCTools.FluentValidation.Generator.CompositionGraph;

using System;

using Microsoft.CodeAnalysis;

/// <summary>
/// Represents a composition relationship between a parent and child validator.
/// Tracks whether the child is directly instantiated (new) or injected via DI.
/// </summary>
internal readonly struct CompositionEdge : IEquatable<CompositionEdge>
{
    public CompositionEdge(
        string parentValidatorName,
        string childValidatorName,
        string childValidatorTypeName,
        CompositionType compositionType,
        bool isDirectInstantiation,
        Location? location)
    {
        ParentValidatorName = parentValidatorName;
        ChildValidatorName = childValidatorName;
        ChildValidatorTypeName = childValidatorTypeName;
        CompositionType = compositionType;
        IsDirectInstantiation = isDirectInstantiation;
        Location = location;
    }

    /// <summary>
    /// Fully qualified name of the parent validator.
    /// </summary>
    public string ParentValidatorName { get; }

    /// <summary>
    /// Fully qualified name of the child validator type.
    /// </summary>
    public string ChildValidatorName { get; }

    /// <summary>
    /// Simple type name of the child validator (for display in diagnostics).
    /// </summary>
    public string ChildValidatorTypeName { get; }

    /// <summary>
    /// How the child is composed into the parent.
    /// </summary>
    public CompositionType CompositionType { get; }

    /// <summary>
    /// True if the child is created via 'new ChildValidator()', false if injected.
    /// </summary>
    public bool IsDirectInstantiation { get; }

    /// <summary>
    /// Source location of the composition call for diagnostic reporting.
    /// </summary>
    public Location? Location { get; }

    public bool Equals(CompositionEdge other) =>
        ParentValidatorName == other.ParentValidatorName &&
        ChildValidatorName == other.ChildValidatorName &&
        CompositionType == other.CompositionType &&
        IsDirectInstantiation == other.IsDirectInstantiation;

    public override bool Equals(object? obj) =>
        obj is CompositionEdge other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 397 + (ParentValidatorName?.GetHashCode() ?? 0);
            hash = hash * 397 + (ChildValidatorName?.GetHashCode() ?? 0);
            hash = hash * 397 + (int)CompositionType;
            hash = hash * 397 + IsDirectInstantiation.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(CompositionEdge left, CompositionEdge right) =>
        left.Equals(right);

    public static bool operator !=(CompositionEdge left, CompositionEdge right) =>
        !left.Equals(right);
}
