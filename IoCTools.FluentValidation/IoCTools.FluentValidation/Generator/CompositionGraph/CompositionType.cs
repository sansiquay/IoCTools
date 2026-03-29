namespace IoCTools.FluentValidation.Generator.CompositionGraph;

/// <summary>
/// Identifies the FluentValidation API used to compose a child validator into a parent.
/// </summary>
internal enum CompositionType
{
    /// <summary>
    /// The child validator is invoked via <c>RuleFor(x => x.Prop).SetValidator(...)</c>.
    /// </summary>
    SetValidator,

    /// <summary>
    /// The child validator rules are merged via <c>Include(...)</c>.
    /// </summary>
    Include,

    /// <summary>
    /// The child validator is registered via <c>RuleFor(x => x.Prop).SetInheritanceValidator(v => v.Add&lt;T&gt;(...))</c>.
    /// </summary>
    SetInheritanceValidator
}
