namespace IoCTools.FluentValidation.Generator.CompositionGraph;

/// <summary>
/// Describes how a child validator is composed into a parent validator.
/// </summary>
internal enum CompositionType
{
    /// <summary>
    /// Child validator set via RuleFor(...).SetValidator(...).
    /// </summary>
    SetValidator,

    /// <summary>
    /// Child validator included via Include(...).
    /// </summary>
    Include,

    /// <summary>
    /// Child validator added via SetInheritanceValidator(v => v.Add&lt;T&gt;(...)).
    /// </summary>
    SetInheritanceValidator
}
