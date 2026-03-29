namespace IoCTools.FluentValidation.Generator.CompositionGraph;

using System;

using Microsoft.CodeAnalysis;

/// <summary>
/// Represents a directed edge in the validator composition graph.
/// An edge connects a parent validator to a child validator via SetValidator, Include,
/// or SetInheritanceValidator invocation.
/// </summary>
internal readonly struct CompositionEdge : IEquatable<CompositionEdge>
{
    /// <summary>
    /// Initializes a new instance of <see cref="CompositionEdge"/>.
    /// </summary>
    /// <param name="parentValidatorName">Fully-qualified name of the parent validator.</param>
    /// <param name="childValidatorName">Fully-qualified name of the child validator.</param>
    /// <param name="childValidatorTypeName">Short name for diagnostic messages.</param>
    /// <param name="compositionType">The FluentValidation API used.</param>
    /// <param name="isDirectInstantiation">True if the child is created via <c>new</c>, false if injected.</param>
    /// <param name="location">Syntax location for diagnostic reporting.</param>
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
    /// Fully-qualified name of the parent validator.
    /// </summary>
    public string ParentValidatorName { get; }

    /// <summary>
    /// Fully-qualified name of the child validator.
    /// </summary>
    public string ChildValidatorName { get; }

    /// <summary>
    /// Short type name of the child validator for use in diagnostic messages.
    /// </summary>
    public string ChildValidatorTypeName { get; }

    /// <summary>
    /// The FluentValidation API that creates this composition relationship.
    /// </summary>
    public CompositionType CompositionType { get; }

    /// <summary>
    /// True if the child validator is instantiated directly (e.g., <c>new ChildValidator()</c>),
    /// false if it is injected via a field, parameter, or property.
    /// </summary>
    public bool IsDirectInstantiation { get; }

    /// <summary>
    /// The syntax location of the composition invocation for diagnostic reporting.
    /// </summary>
    public Location? Location { get; }

    /// <inheritdoc/>
    public bool Equals(CompositionEdge other) =>
        ParentValidatorName == other.ParentValidatorName &&
        ChildValidatorName == other.ChildValidatorName &&
        CompositionType == other.CompositionType &&
        IsDirectInstantiation == other.IsDirectInstantiation;

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is CompositionEdge other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 397 + (ParentValidatorName?.GetHashCode() ?? 0);
            hash = hash * 397 + (ChildValidatorName?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public static bool operator ==(CompositionEdge left, CompositionEdge right) =>
        left.Equals(right);

    public static bool operator !=(CompositionEdge left, CompositionEdge right) =>
        !left.Equals(right);
}
