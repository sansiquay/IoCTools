namespace IoCTools.Abstractions.Annotations;

using System;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class AutoDepOpenAttribute : Attribute
{
    public AutoDepOpenAttribute(Type unboundGenericType)
    {
        UnboundGenericType = unboundGenericType ?? throw new ArgumentNullException(nameof(unboundGenericType));
    }

    public Type UnboundGenericType { get; }
    public AutoDepScope Scope { get; set; } = AutoDepScope.Assembly;
}
